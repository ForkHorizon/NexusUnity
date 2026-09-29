using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityMCP.Editor.Commands;

namespace UnityMCP.Editor.Runtime
{
    /// <summary>
    /// Detects optional Unity Pipeline without blocking Editor startup.
    /// Reads Unity package metadata and <c>Library/Pipeline/.unity-pipeline-port</c>, then TCP-probes the session port on a worker thread.
    /// </summary>
    public sealed class NexusRuntimeCapabilities
    {
        internal const string PipelinePackageName = "com.unity.pipeline";
        internal const string PipelineCommandsTypeName =
            "UnityMCP.Editor.Pipeline.NexusPipelineCommands, UnityMCP.Editor.Pipeline";
        internal const string LastHealthyKey = "Nexus_PipelineLastHealthy";
        internal const string FailStreakKey = "Nexus_PipelineFailStreak";

        private const int ProbeAttempts = 4;
        private const int ProbeRetryDelayMs = 1500;
        private static readonly object Sync = new object();
        private static int _probeToken;
        private RuntimeCapabilitySnapshot _current = new RuntimeCapabilitySnapshot { HealthUnknown = true };

        /// <summary>Shared capability source for this Editor domain.</summary>
        public static NexusRuntimeCapabilities Shared { get; } = new NexusRuntimeCapabilities();

        /// <summary>Gets a copy of the latest cached capability snapshot.</summary>
        public RuntimeCapabilitySnapshot Current
        {
            get { lock (Sync) return Clone(_current); }
        }

        /// <summary>
        /// Captures package/session metadata and starts a thread-pool TCP health probe.
        /// </summary>
        public void BeginHealthProbe()
        {
            int generation = MCPServer.SessionGeneration;
            RuntimeCapabilitySnapshot seed = CaptureMetadata(generation);
            lock (Sync) _current = seed;
            int token = Interlocked.Increment(ref _probeToken);
            Task.Run(() => ProbeAndStore(seed, token));
        }

        /// <summary>
        /// Returns whether <c>com.unity.pipeline</c> is registered or the optional Nexus Pipeline assembly loaded.
        /// </summary>
        internal static bool IsPipelinePackagePresent()
        {
            try
            {
                foreach (var package in PackageInfo.GetAllRegisteredPackages())
                {
                    if (package != null && package.name == PipelinePackageName) return true;
                }
            }
            catch (Exception)
            {
            }

            return Type.GetType(PipelineCommandsTypeName) != null;
        }

        internal static void ApplyPipelinePackageInfo(RuntimeCapabilitySnapshot snapshot)
        {
            if (snapshot == null) return;
            try
            {
                foreach (var package in PackageInfo.GetAllRegisteredPackages())
                {
                    if (package == null || package.name != PipelinePackageName) continue;
                    snapshot.PipelinePackageVersion = package.version;
                    snapshot.PipelineExperimental = NexusLegacyDeprecation.IsPreviewVersion(package.version);
                    return;
                }
            }
            catch (Exception)
            {
            }
        }

        internal static bool IsUnityVersionSupported()
        {
            string version = Application.unityVersion;
            if (string.IsNullOrEmpty(version)) return false;
            int dot = version.IndexOf('.');
            return int.TryParse(dot > 0 ? version.Substring(0, dot) : version, out int major) && major >= 6000;
        }

        internal static bool AreNexusPipelineCommandsRegistered()
        {
            if (Type.GetType(PipelineCommandsTypeName) == null) return false;
            NexusCommandRegistry.EnsureCreated();
            return NexusCommandRegistry.Contains(ProjectMapCommand.Id)
                && NexusCommandRegistry.Contains(GroupCompileErrorsCommand.Id)
                && NexusCommandRegistry.Contains(CaptureGameViewCommand.Id);
        }

        internal static string PipelinePortFilePath()
        {
            string project = Directory.GetCurrentDirectory();
            return Path.Combine(project, "Library", "Pipeline", ".unity-pipeline-port");
        }

        internal static RuntimeCapabilitySnapshot CaptureMetadata(int generation)
        {
            var snapshot = new RuntimeCapabilitySnapshot
            {
                SessionGeneration = generation,
                PipelinePackagePresent = IsPipelinePackagePresent(),
                UnityVersionSupported = IsUnityVersionSupported(),
                NexusCommandsRegistered = AreNexusPipelineCommandsRegistered(),
                HealthUnknown = true,
                Detail = "Pipeline health probe pending."
            };

            ApplyPipelinePackageInfo(snapshot);
            ApplyPortFile(snapshot, PipelinePortFilePath());
            SeedFromLastHealthy(snapshot);
            return snapshot;
        }

        /// <summary>
        /// Keeps Auto on Pipeline across a domain reload while the async probe runs, if last probe was healthy.
        /// </summary>
        internal static void SeedFromLastHealthy(RuntimeCapabilitySnapshot snapshot)
        {
            if (snapshot == null || !snapshot.PipelineSessionFilePresent) return;
            if (snapshot.PipelinePid != null && !IsPidAlive(snapshot.PipelinePid.Value)) return;
            if (!UnityEditor.SessionState.GetBool(LastHealthyKey, false)) return;
            snapshot.HealthUnknown = false;
            snapshot.PipelineHealthy = true;
            snapshot.Detail = "Holding last healthy Pipeline state while probe runs.";
        }

        internal static void ApplyPortFile(RuntimeCapabilitySnapshot snapshot, string path)
        {
            if (snapshot == null || string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                var json = JObject.Parse(File.ReadAllText(path));
                snapshot.PipelineSessionFilePresent = true;
                snapshot.PipelinePort = json.Value<int?>("port");
                snapshot.PipelinePid = json.Value<int?>("pid");
            }
            catch (Exception ex)
            {
                snapshot.Detail = "Pipeline session file could not be parsed: " + ex.Message;
            }
        }

        internal static bool IsPidAlive(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                var process = Process.GetProcessById(pid);
                return process != null && !process.HasExited;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// True when <paramref name="pid"/> is alive and the process name looks like a Unity Editor.
        /// </summary>
        internal static bool IsLikelyUnityEditorProcess(int pid)
        {
            if (!IsPidAlive(pid)) return false;
            try
            {
                string name = Process.GetProcessById(pid).ProcessName;
                return !string.IsNullOrEmpty(name) &&
                    name.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool ProbeTcp(int port, int timeoutMs)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    IAsyncResult ar = client.BeginConnect("127.0.0.1", port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs)) return false;
                    client.EndConnect(ar);
                    return client.Connected;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ProbeAndStore(RuntimeCapabilitySnapshot seed, int token)
        {
            try
            {
                var working = Clone(seed);
                RefreshPortFile(working);
                RuntimeCapabilitySnapshot result = Probe(working);
                // Pipeline often writes its port file / starts listening shortly after Editor init.
                // ponytail: fixed short retry, not a watcher; changing Mode in settings re-probes.
                for (int i = 1; i < ProbeAttempts && result.PipelinePackagePresent && !result.PipelineHealthy; i++)
                {
                    Thread.Sleep(ProbeRetryDelayMs);
                    if (Volatile.Read(ref _probeToken) != token) return; // superseded by a newer probe
                    lock (Sync)
                    {
                        if (_current.SessionGeneration != seed.SessionGeneration) return;
                    }

                    working = Clone(seed);
                    RefreshPortFile(working);
                    result = Probe(working);
                }

                lock (Sync)
                {
                    if (_current.SessionGeneration != result.SessionGeneration) return;
                    if (Volatile.Read(ref _probeToken) != token) return;
                    _current = result;
                }

                MCPServer.Enqueue(() =>
                {
                    PersistHealth(result);
                    NexusRuntimeHost.PublishSnapshot();
                });
            }
            catch (Exception ex)
            {
                lock (Sync)
                {
                    if (_current.SessionGeneration != seed.SessionGeneration) return;
                    _current.HealthUnknown = false;
                    _current.PipelineHealthy = false;
                    _current.Detail = "Pipeline health probe failed: " + ex.Message;
                }
            }
        }

        /// <summary>
        /// Stores last Pipeline health in SessionState so the next Editor generation can seed without flapping.
        /// </summary>
        internal static void PersistHealth(RuntimeCapabilitySnapshot result)
        {
            if (result == null) return;
            UnityEditor.SessionState.SetBool(LastHealthyKey, result.PipelineHealthy);
            int streak = result.PipelineHealthy ? 0 : UnityEditor.SessionState.GetInt(FailStreakKey, 0) + 1;
            UnityEditor.SessionState.SetInt(FailStreakKey, streak);
        }

        internal static void RefreshPortFile(RuntimeCapabilitySnapshot snapshot)
        {
            RefreshPortFile(snapshot, PipelinePortFilePath());
        }

        internal static void RefreshPortFile(RuntimeCapabilitySnapshot snapshot, string path)
        {
            if (snapshot == null) return;
            snapshot.PipelineSessionFilePresent = false;
            snapshot.PipelinePort = null;
            snapshot.PipelinePid = null;
            ApplyPortFile(snapshot, path);
        }

        internal static RuntimeCapabilitySnapshot Probe(RuntimeCapabilitySnapshot seed)
        {
            var result = Clone(seed);
            result.HealthUnknown = false;
            if (!result.PipelinePackagePresent)
            {
                result.PipelineHealthy = false;
                result.Detail = "com.unity.pipeline is not installed.";
                return result;
            }

            if (result.PipelinePort == null || result.PipelinePort.Value <= 0)
            {
                result.PipelineHealthy = false;
                result.Detail = "Pipeline package is present but no session port file was found.";
                return result;
            }

            if (result.PipelinePid != null && !IsPidAlive(result.PipelinePid.Value))
            {
                result.PipelineHealthy = false;
                result.Detail = "Pipeline session PID is not running (stale port file).";
                return result;
            }

            if (result.PipelinePid != null && !IsLikelyUnityEditorProcess(result.PipelinePid.Value))
            {
                result.PipelineHealthy = false;
                result.Detail = "Pipeline session PID is not a Unity Editor process (port may be owned by another process).";
                return result;
            }

            result.PipelineHealthy = ProbeTcp(result.PipelinePort.Value, 250);
            result.Detail = result.PipelineHealthy
                ? "Pipeline loopback port accepted a TCP connection."
                : "Pipeline session port did not accept a TCP connection.";
            return result;
        }

        private static RuntimeCapabilitySnapshot Clone(RuntimeCapabilitySnapshot source)
        {
            if (source == null) return new RuntimeCapabilitySnapshot { HealthUnknown = true };
            return new RuntimeCapabilitySnapshot
            {
                SessionGeneration = source.SessionGeneration,
                PipelinePackagePresent = source.PipelinePackagePresent,
                PipelineSessionFilePresent = source.PipelineSessionFilePresent,
                PipelinePort = source.PipelinePort,
                PipelinePid = source.PipelinePid,
                PipelineHealthy = source.PipelineHealthy,
                HealthUnknown = source.HealthUnknown,
                UnityVersionSupported = source.UnityVersionSupported,
                NexusCommandsRegistered = source.NexusCommandsRegistered,
                PipelinePackageVersion = source.PipelinePackageVersion,
                PipelineExperimental = source.PipelineExperimental,
                Detail = source.Detail
            };
        }
    }
}
