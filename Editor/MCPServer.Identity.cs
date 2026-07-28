using System;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    // Package version resolution and auth-token lifecycle. Split out of
    // MCPServer.cs to keep each file under the readability line budget; this is
    // the same partial MCPServer class.
    public static partial class MCPServer
    {
        private static string ReadPackageVersion()
        {
            try
            {
                var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MCPServer).Assembly);
                if (pkgInfo != null && !string.IsNullOrEmpty(pkgInfo.version))
                {
                    return pkgInfo.version;
                }
            }
            catch { }

            try
            {
                string packageJsonPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "NexusUnity", "package.json"));
                if (!File.Exists(packageJsonPath))
                {
                    packageJsonPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "package.json"));
                }
                if (File.Exists(packageJsonPath))
                {
                    var json = JObject.Parse(File.ReadAllText(packageJsonPath));
                    string ver = json["version"]?.ToString();
                    if (!string.IsNullOrEmpty(ver)) return ver;
                }
            }
            catch { }

            return "1.5.0";
        }

        internal static string AuthToken => EnsureAuthToken();

        private static string GetEditorPrefsTokenKey()
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                return "NexusUnity_AuthToken_" + Math.Abs(projectRoot.GetHashCode());
            }
            catch
            {
                return "NexusUnity_AuthToken_Default";
            }
        }

        private static string EnsureAuthToken()
        {
            if (!string.IsNullOrEmpty(_authToken)) return _authToken;

            if (_mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                return _authToken;
            }

            try
            {
                _authToken = SessionState.GetString(AuthSessionStateKey, string.Empty);

                if (string.IsNullOrEmpty(_authToken))
                {
                    _authToken = ReadTokenFile();
                }

                if (string.IsNullOrEmpty(_authToken))
                {
                    _authToken = EditorPrefs.GetString(GetEditorPrefsTokenKey(), string.Empty);
                }

                if (string.IsNullOrEmpty(_authToken))
                {
                    _authToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                }

                SessionState.SetString(AuthSessionStateKey, _authToken);
                EditorPrefs.SetString(GetEditorPrefsTokenKey(), _authToken);
                WriteTokenFile(_authToken);
            }
            catch { }

            return _authToken;
        }

        internal static string RotateAuthToken()
        {
            _authToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            try
            {
                SessionState.SetString(AuthSessionStateKey, _authToken);
                EditorPrefs.SetString(GetEditorPrefsTokenKey(), _authToken);
                WriteTokenFile(_authToken);
            }
            catch { }
            return _authToken;
        }

        private static void WriteTokenFile(string token)
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                string libraryDir = Path.Combine(projectRoot, "Library");
                if (Directory.Exists(libraryDir))
                {
                    File.WriteAllText(Path.Combine(libraryDir, "NexusUnityAuthToken.txt"), token);
                }
            }
            catch { }
        }

        internal static string ReadTokenFile()
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                string tokenPath = Path.Combine(projectRoot, "Library", "NexusUnityAuthToken.txt");
                if (File.Exists(tokenPath))
                {
                    return File.ReadAllText(tokenPath).Trim();
                }
            }
            catch { }
            return null;
        }
    }
}
