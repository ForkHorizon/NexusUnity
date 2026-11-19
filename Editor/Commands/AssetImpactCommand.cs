// Canonical nexus.asset_impact handler: read-only static C# <-> Unity YAML impact (find_references, analyze_change_impact, validate_asset_integrity).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Canonical handler for Layer A asset impact. Reads text assets, .meta files and C# sources from disk and never writes,
    /// imports or reserializes anything.
    /// </summary>
    public sealed class AssetImpactCommand : INexusCommand
    {
        /// <summary>Canonical command id.</summary>
        public const string Id = "nexus.asset_impact";
        /// <summary>HTTP / Unity CLI alias.</summary>
        public const string Alias = "nexus_asset_impact";
        /// <summary>Shared command description for every projection.</summary>
        public const string Description =
            "Static C# <-> Unity YAML impact: find_references (type/field/method usages with stored per-asset values vs the C# default), " +
            "analyze_change_impact (rename_field, change_field_type, rename_method, delete_script, move_type) and validate_asset_integrity " +
            "(Missing Script, dangling guids, broken UnityEvent bindings). Read-only.";
        /// <summary>Parameter descriptions shared with the Pipeline projection.</summary>
        public const string OpDescription = "find_references, analyze_change_impact or validate_asset_integrity.";
        public const string ChangeDescription = "For analyze_change_impact: rename_field, change_field_type, rename_method, delete_script or move_type.";
        public const string KindDescription = "Symbol kind: type, field or method. Inferred from change when omitted.";
        public const string TypeDescription = "C# type, full (Game.Health) or simple (Health) name.";
        public const string NameDescription = "Field or method name. Omit for kind=type.";
        public const string NewNameDescription = "Optional new name, echoed in findings.";
        public const string PathsDescription = "Comma-separated project-relative folders or files to scan. Default Assets.";
        public const string ChangedScriptsDescription = "For validate_asset_integrity: comma-separated .cs paths; only assets using them are checked.";
        public const string MaxResultsDescription = "Maximum entries per result list (1-5000). Extra entries set complete=false.";
        /// <summary>Default and hard cap for <c>max_results</c>.</summary>
        public const int DefaultMaxResults = 500;
        private const int MaxResultsCap = 5000;

        /// <summary>Gets the canonical asset impact descriptor.</summary>
        public NexusCommandDescriptor Descriptor { get; } = new NexusCommandDescriptor
        {
            Id = Id,
            Aliases = new[] { Alias },
            Title = "Asset impact",
            Description = Description,
            Profiles = new[] { "core" },
            Parameters = new[]
            {
                NexusCommandParameter.Optional("op", "string", OpDescription, "find_references"),
                NexusCommandParameter.Optional("change", "string", ChangeDescription, null),
                NexusCommandParameter.Optional("kind", "string", KindDescription, null),
                NexusCommandParameter.Optional("type", "string", TypeDescription, null),
                NexusCommandParameter.Optional("name", "string", NameDescription, null),
                NexusCommandParameter.Optional("new_name", "string", NewNameDescription, null),
                NexusCommandParameter.Optional("paths", "string", PathsDescription, "Assets"),
                NexusCommandParameter.Optional("changed_scripts", "string", ChangedScriptsDescription, null),
                NexusCommandParameter.Optional("max_results", "integer", MaxResultsDescription, DefaultMaxResults),
                NexusCommandParameter.Optional("request_id", "string", NexusCommandCodes.RequestIdDescription, null)
            }
        };

        /// <summary>Flat request shared by HTTP, Pipeline and the legacy <c>find_references</c> symbol mode.</summary>
        public sealed class Request
        {
            public string Op = "find_references";
            public string Change;
            public string Kind;
            public string Type;
            public string Name;
            public string NewName;
            public string Paths;
            public string ChangedScripts;
            public int MaxResults = DefaultMaxResults;
            public string RequestId;
        }

        /// <summary>Executes from JSON parameters and returns the canonical envelope. Read-only.</summary>
        public Task<JToken> ExecuteAsync(JToken parameters, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            if (!NexusCommandArguments.TryOpen(parameters, sw.ElapsedMilliseconds, out NexusCommandArguments args))
                return Task.FromResult<JToken>(args.Rejected.ToJson());
            var request = new Request { RequestId = args.RequestId };
            if (!args.TryReadString("op", out string op) || !args.TryReadString("change", out request.Change) ||
                !args.TryReadString("kind", out request.Kind) || !args.TryReadString("type", out request.Type) ||
                !args.TryReadString("name", out request.Name) || !args.TryReadString("new_name", out request.NewName) ||
                !ReadList(parameters, args, "paths", out request.Paths) || !ReadList(parameters, args, "changed_scripts", out request.ChangedScripts) ||
                !args.TryReadInt("max_results", out int? max))
                return Task.FromResult<JToken>(args.Rejected.ToJson());
            request.Op = op ?? request.Op;
            request.MaxResults = max ?? DefaultMaxResults;
            return Task.FromResult<JToken>(ExecuteEnveloped(request).ToJson());
        }

        private static bool ReadList(JToken parameters, NexusCommandArguments args, string name, out string value)
        {
            if (parameters?[name] is JArray array && array.All(t => t.Type == JTokenType.String))
            {
                value = string.Join(",", array.Select(t => (string)t));
                return true;
            }
            return args.TryReadString(name, out value);
        }

        /// <summary>Runs one request against the open project and wraps the result. Read-only.</summary>
        public static NexusCommandEnvelope ExecuteEnveloped(Request request)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                string root = Path.GetDirectoryName(Application.dataPath);
                // ValidateAssetPath resolves symlinks/junctions and confines scopes to Assets/, Packages/ or ProjectSettings/.
                List<string> scopes = Split(request.Paths ?? "Assets").Select(ValidateScope).ToList();
                JObject data = Run(request, root, scopes, AssetDatabaseKnows);
                SerializationMode mode = EditorSettings.serializationMode;
                data["serialization_mode"] = mode.ToString();
                if (mode != SerializationMode.ForceText)
                {
                    data["complete"] = false;
                    ((JArray)data["warnings"]).Add(new JObject
                    {
                        ["code"] = "UNSUPPORTED_SERIALIZATION",
                        ["message"] = "Asset Serialization is " + mode + "; binary assets are listed under skipped and not analyzed. Switch to Force Text for full coverage."
                    });
                }
                return NexusCommandEnvelope.Success(data, request.RequestId, sw.ElapsedMilliseconds);
            }
            catch (ArgumentException ex)
            {
                return NexusCommandEnvelope.InvalidArgument(ex.Message, request.RequestId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return NexusCommandEnvelope.FromException(ex, request.RequestId, sw.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Core entry without editor state, used by tests against <c>Tests/Fixtures~/AssetImpact</c>.
        /// <paramref name="scopes"/> must already be validated project-relative paths.
        /// </summary>
        internal static JObject Run(Request request, string root, IList<string> scopes, Func<string, bool> externalGuid, ScanBudget budget = null)
        {
            if (request.MaxResults < 1 || request.MaxResults > MaxResultsCap)
                throw new ArgumentException("max_results must be between 1 and " + MaxResultsCap + ".");
            if (request.Op != "find_references" && request.Op != "analyze_change_impact" && request.Op != "validate_asset_integrity")
                throw new ArgumentException("op must be find_references, analyze_change_impact or validate_asset_integrity.");
            var sw = Stopwatch.StartNew();
            AssetYamlIndex index = AssetYamlIndex.Build(root, scopes, Array.Empty<string>(), budget ?? new ScanBudget());
            index.ExternalGuid = externalGuid;
            var analyzer = new AssetImpactAnalyzer(index, request.MaxResults);
            JObject data;
            switch (request.Op)
            {
                case "find_references":
                    data = analyzer.FindReferences(Symbol(request, request.Kind ?? (request.Name == null ? "type" : "field")));
                    break;
                case "analyze_change_impact":
                    if (string.IsNullOrEmpty(request.Change)) throw new ArgumentException("change is required for analyze_change_impact.");
                    string kind = request.Change.EndsWith("_field", StringComparison.Ordinal) || request.Change == "change_field_type" ? "field"
                        : request.Change == "rename_method" ? "method" : "type";
                    data = analyzer.AnalyzeChange(request.Change, Symbol(request, kind));
                    break;
                default:
                    data = analyzer.ValidateIntegrity(ChangedGuids(index, request.ChangedScripts));
                    break;
            }
            data["op"] = request.Op;
            data["scope"] = new JArray(scopes);
            data["scanned"]["ms"] = sw.ElapsedMilliseconds;
            return data;
        }

        private static ImpactSymbol Symbol(Request request, string kind)
        {
            if (kind != "type" && kind != "field" && kind != "method") throw new ArgumentException("kind must be type, field or method.");
            if (kind != "type" && string.IsNullOrEmpty(request.Name)) throw new ArgumentException("name is required for kind=" + kind + ".");
            return new ImpactSymbol { Kind = kind, Type = request.Type, Name = kind == "type" ? null : request.Name, NewName = request.NewName };
        }

        private static ISet<string> ChangedGuids(AssetYamlIndex index, string changedScripts)
        {
            if (string.IsNullOrEmpty(changedScripts)) return null;
            var wanted = new HashSet<string>(Split(changedScripts).Select(p => p.Replace('\\', '/')), StringComparer.Ordinal);
            return new HashSet<string>(index.GuidToPath.Where(kv => wanted.Contains(kv.Value)).Select(kv => kv.Key), StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> Split(string list) =>
            list.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0);

        private static string ValidateScope(string path)
        {
            try { return MCPServerMethods.ValidateAssetPath(path); }
            catch (Exception ex) { throw new ArgumentException("paths: " + ex.Message, ex); }
        }

        private static bool AssetDatabaseKnows(string guid) => !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid));
    }
}
