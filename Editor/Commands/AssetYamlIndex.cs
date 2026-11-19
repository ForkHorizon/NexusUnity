// Read-only line scanner that indexes Force Text Unity YAML assets, .meta guids and C# scripts for asset impact queries.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Commands
{
    /// <summary>File, byte and time limits. Hitting one marks the index incomplete instead of failing.</summary>
    internal sealed class ScanBudget
    {
        public int MaxFiles = 50000;
        public long MaxBytes = 512L * 1024 * 1024;
        public long MaxMilliseconds = 30000;
    }

    /// <summary>
    /// Read-only index of a Unity project tree. Opens files for reading only and never writes, imports or reserializes.
    /// Folders Unity ignores (dot-prefixed, <c>~</c>-suffixed) are skipped exactly like the Asset Database does.
    /// </summary>
    internal sealed class AssetYamlIndex
    {
        internal static readonly string[] TextAssetExtensions =
        {
            ".prefab", ".unity", ".asset", ".anim", ".controller", ".overrideController", ".mat", ".playable",
            ".mask", ".preset", ".lighting", ".signal", ".mixer", ".physicMaterial", ".physicsMaterial2D", ".terrainlayer"
        };
        internal static readonly HashSet<string> BuiltinGuids = new HashSet<string>(StringComparer.Ordinal)
        {
            "0000000000000000e000000000000000", "0000000000000000f000000000000000", "0000000000000000d000000000000000",
            "00000000000000000000000000000000"
        };
        private static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?", RegexOptions.Compiled);
        private static readonly Regex GuidRef = new Regex(@"guid: ([0-9a-fA-F]{32})", RegexOptions.Compiled);
        private static readonly Regex MetaGuid = new Regex(@"^guid: ([0-9a-fA-F]{32})", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex Namespace = new Regex(@"^\s*namespace\s+([\w.]+)", RegexOptions.Compiled | RegexOptions.Multiline);

        public string Root;
        public readonly Dictionary<string, string> GuidToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _pathToGuid = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, ScriptInfo> Scripts = new Dictionary<string, ScriptInfo>(StringComparer.OrdinalIgnoreCase);
        public readonly List<YamlAsset> Assets = new List<YamlAsset>();
        public readonly Dictionary<string, YamlAsset> AssetsByGuid = new Dictionary<string, YamlAsset>(StringComparer.OrdinalIgnoreCase);
        public bool Complete = true;
        public string TruncatedReason;
        public int FilesRead;
        public long BytesRead;
        /// <summary>Optional resolver for guids outside the scanned roots (the Editor passes the Asset Database).</summary>
        public Func<string, bool> ExternalGuid;

        /// <summary>
        /// Scans <paramref name="scopes"/> (project-relative folders or files) under <paramref name="root"/>.
        /// <paramref name="guidRoots"/> are folders whose .meta files only feed the guid index (for example <c>Library/PackageCache</c>).
        /// </summary>
        public static AssetYamlIndex Build(string root, IEnumerable<string> scopes, IEnumerable<string> guidRoots, ScanBudget budget)
        {
            var index = new AssetYamlIndex { Root = Path.GetFullPath(root).Replace('\\', '/').TrimEnd('/') };
            var sw = Stopwatch.StartNew();
            var assetFiles = new List<string>();
            foreach (string scope in scopes.Concat(guidRoots).Distinct(StringComparer.Ordinal))
            {
                string full = index.Abs(scope);
                bool assetScope = scopes.Contains(scope);
                if (File.Exists(full)) { index.Visit(full, assetScope, assetFiles); continue; }
                if (!Directory.Exists(full)) continue;
                foreach (string file in Walk(full)) index.Visit(file, assetScope, assetFiles);
            }
            foreach (string file in assetFiles.Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (!index.WithinBudget(budget, sw)) break;
                index.ReadAsset(file);
            }
            return index;
        }

        private bool WithinBudget(ScanBudget budget, Stopwatch sw)
        {
            string reason = FilesRead >= budget.MaxFiles ? "max_files"
                : BytesRead >= budget.MaxBytes ? "max_bytes"
                : sw.ElapsedMilliseconds >= budget.MaxMilliseconds ? "max_ms" : null;
            if (reason == null) return true;
            Complete = false;
            TruncatedReason = reason;
            return false;
        }

        private string Abs(string relative) => Path.GetFullPath(Path.Combine(Root, relative)).Replace('\\', '/');
        /// <summary>Project-relative form of an absolute path under <see cref="Root"/>.</summary>
        public string Rel(string full) => full.Length > Root.Length ? full.Substring(Root.Length + 1) : full;

        private static IEnumerable<string> Walk(string dir)
        {
            var stack = new Stack<string>();
            stack.Push(dir);
            while (stack.Count > 0)
            {
                string current = stack.Pop();
                foreach (string file in Directory.GetFiles(current)) yield return file.Replace('\\', '/');
                foreach (string sub in Directory.GetDirectories(current))
                {
                    string name = Path.GetFileName(sub);
                    // ponytail: no symlink following; Unity itself does not import through symlinked folders reliably.
                    if (name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith("~", StringComparison.Ordinal)) continue;
                    if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) continue;
                    stack.Push(sub);
                }
            }
        }

        private void Visit(string file, bool assetScope, List<string> assetFiles)
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                string head = ReadHead(file);
                Match m = MetaGuid.Match(head);
                if (!m.Success) return;
                string assetPath = Rel(file.Substring(0, file.Length - 5));
                GuidToPath[m.Groups[1].Value] = assetPath;
                _pathToGuid[assetPath] = m.Groups[1].Value;
                return;
            }
            if (!assetScope) return;
            string ext = Path.GetExtension(file);
            if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase)) assetFiles.Add(file);
            else if (TextAssetExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) assetFiles.Add(file);
        }

        private static string ReadHead(string file)
        {
            using (var reader = new StreamReader(file))
            {
                var buffer = new char[512];
                int n = reader.Read(buffer, 0, buffer.Length);
                return new string(buffer, 0, n);
            }
        }

        private void ReadAsset(string file)
        {
            byte[] bytes = File.ReadAllBytes(file);
            FilesRead++;
            BytesRead += bytes.Length;
            string rel = Rel(file);
            _pathToGuid.TryGetValue(rel, out string guid);
            if (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                if (guid != null) AddScript(rel, guid, System.Text.Encoding.UTF8.GetString(bytes));
                return;
            }
            var asset = new YamlAsset { Path = rel, Guid = guid };
            Assets.Add(asset);
            if (guid != null) AssetsByGuid[guid] = asset;
            if (bytes.Length < 5 || bytes[0] != '%' || bytes[1] != 'Y' || bytes[2] != 'A' || bytes[3] != 'M' || bytes[4] != 'L')
            {
                asset.Status = "skipped";
                asset.Reason = "UNSUPPORTED_SERIALIZATION";
                return;
            }
            asset.Lines = System.Text.Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n").Split('\n');
            string error = YamlDocParser.Parse(asset);
            if (error == null) return;
            asset.Status = "unknown";
            asset.Reason = "YAML_PARSE_ERROR: " + error;
            asset.Docs.Clear();
        }

        private void AddScript(string rel, string guid, string source)
        {
            string className = Path.GetFileNameWithoutExtension(rel);
            Match ns = Namespace.Match(source);
            Match decl = Regex.Match(source, @"\b(partial\s+)?class\s+" + Regex.Escape(className) + @"\b[^:{]*(?::\s*([\w.]+))?");
            if (!decl.Success) return;
            string baseName = decl.Groups[2].Success ? decl.Groups[2].Value : null;
            Scripts[guid] = new ScriptInfo
            {
                Guid = guid,
                Path = rel,
                ClassName = className,
                FullName = ns.Success ? ns.Groups[1].Value + "." + className : className,
                BaseName = baseName?.Substring(baseName.LastIndexOf('.') + 1),
                Partial = decl.Groups[1].Success,
                Source = source
            };
        }

        /// <summary>True when the guid names an asset, a builtin resource, or something the external resolver knows.</summary>
        public bool KnowsGuid(string guid) =>
            BuiltinGuids.Contains(guid) || GuidToPath.ContainsKey(guid) || (ExternalGuid != null && ExternalGuid(guid));

        /// <summary>Extracts a <c>guid:</c> value from an inline <c>{fileID: n, guid: g, type: t}</c> reference.</summary>
        public static string GuidOf(string inline)
        {
            if (inline == null) return null;
            Match m = GuidRef.Match(inline);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>Extracts <c>fileID</c> from an inline reference, or 0.</summary>
        public static long FileIdOf(string inline)
        {
            if (inline == null) return 0;
            Match m = Regex.Match(inline, @"fileID: (-?\d+)");
            return m.Success && long.TryParse(m.Groups[1].Value, out long id) ? id : 0;
        }

        internal static Regex GuidPattern => GuidRef;
        internal static Regex HeaderPattern => Header;
    }
}
