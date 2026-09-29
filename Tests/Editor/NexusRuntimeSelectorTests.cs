using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Runtime;

namespace UnityMCP.Editor.Tests
{
    public class NexusRuntimeSelectorTests
    {
        [Test]
        public void ExplicitLegacyAlwaysWins()
        {
            Assert.AreEqual(NexusRuntimeMode.Legacy, NexusRuntimeSelector.Effective(NexusRuntimeMode.Legacy, EligibleSnapshot()));
            Assert.AreEqual("legacy", NexusRuntimeSelector.ToWireName(NexusRuntimeMode.Legacy));
            Assert.AreEqual("auto", NexusRuntimeSelector.ToWireName(NexusRuntimeMode.Auto));
            Assert.IsFalse(NexusRuntimeSelector.CanSkipLegacyHttpBind(NexusRuntimeMode.Legacy, EligibleSnapshot()));
            Assert.IsFalse(NexusRuntimeCapabilities.IsPidAlive(-1));
            Assert.IsFalse(NexusRuntimeCapabilities.IsPidAlive(99999999));
        }

        [Test]
        public void RequestedRuntimeModeCacheIsVisibleOffMainThread()
        {
            NexusRuntimeMode previous = MCPSettings.RuntimeMode;
            try
            {
                MCPSettings.RuntimeMode = NexusRuntimeMode.Legacy;
                NexusRuntimeMode seen = NexusRuntimeMode.Auto;
                var thread = new Thread(() => { seen = MCPSettings.RuntimeMode; });
                thread.Start();
                thread.Join();
                Assert.AreEqual(NexusRuntimeMode.Legacy, seen);
                Assert.AreEqual("legacy", NexusRuntimeHost.ToStatusJson()["requested"]?.ToString());
            }
            finally
            {
                MCPSettings.RuntimeMode = previous;
            }
        }

        [Test]
        public void AutoPrefersPipelineWhenEligible()
        {
            RuntimeCapabilitySnapshot eligible = EligibleSnapshot();
            Assert.IsTrue(NexusRuntimeSelector.IsEligible(eligible));
            Assert.AreEqual(NexusRuntimeMode.Pipeline, NexusRuntimeSelector.Effective(NexusRuntimeMode.Auto, eligible));
            Assert.AreEqual(NexusRuntimeMode.Pipeline, NexusRuntimeSelector.Effective(NexusRuntimeMode.Pipeline, eligible));
            Assert.IsTrue(NexusRuntimeSelector.CanSkipLegacyHttpBind(NexusRuntimeMode.Auto, eligible));
        }

        [Test]
        public void AutoAndPipelineFallBackToLegacyWhenNotEligible()
        {
            var unknown = EligibleSnapshot();
            unknown.HealthUnknown = true;
            unknown.PipelineHealthy = false;
            var broken = EligibleSnapshot();
            broken.PipelineHealthy = false;
            var missingCommands = EligibleSnapshot();
            missingCommands.NexusCommandsRegistered = false;
            var oldUnity = EligibleSnapshot();
            oldUnity.UnityVersionSupported = false;

            Assert.AreEqual(NexusRuntimeMode.Legacy, NexusRuntimeSelector.Effective(NexusRuntimeMode.Auto, unknown));
            Assert.AreEqual(NexusRuntimeMode.Legacy, NexusRuntimeSelector.Effective(NexusRuntimeMode.Auto, broken));
            Assert.AreEqual(NexusRuntimeMode.Pipeline, NexusRuntimeSelector.Effective(NexusRuntimeMode.Pipeline, broken));
            Assert.AreEqual(NexusRuntimeMode.Legacy, NexusRuntimeSelector.Effective(NexusRuntimeMode.Auto, missingCommands));
            Assert.AreEqual(NexusRuntimeMode.Legacy, NexusRuntimeSelector.Effective(NexusRuntimeMode.Auto, oldUnity));
            Assert.IsFalse(NexusRuntimeSelector.CanSkipLegacyHttpBind(NexusRuntimeMode.Auto, broken));
            Assert.IsTrue(NexusRuntimeSelector.CanSkipLegacyHttpBind(NexusRuntimeMode.Pipeline, broken));
        }

        [Test]
        public void LegacySunsetIsAnnouncedButNotFullyDeprecated()
        {
            Assert.IsFalse(NexusLegacyDeprecation.IsFullyDeprecated);
            Assert.IsTrue(NexusLegacyDeprecation.StillSupported);
            Assert.IsTrue(NexusLegacyDeprecation.IsPreviewVersion("0.7.0-exp.1"));
            Assert.IsTrue(NexusLegacyDeprecation.IsPreviewVersion("1.0.0-beta.10"));
            Assert.IsFalse(NexusLegacyDeprecation.IsPreviewVersion("1.0.0"));

            var experimental = EligibleSnapshot();
            experimental.PipelinePackageVersion = "0.7.0-exp.1";
            experimental.PipelineExperimental = true;
            var json = NexusLegacyDeprecation.ToStatusJson(experimental);
            Assert.AreEqual(false, json["deprecated"]?.ToObject<bool>());
            Assert.AreEqual(true, json["still_supported"]?.ToObject<bool>());
            Assert.AreEqual("announced", json["sunset_status"]?.ToString());
            Assert.AreEqual("not_scheduled", json["removal"]?.ToString());
            Assert.AreEqual(false, json["gates"]?["unity_cli_stable"]?["passed"]?.ToObject<bool>());
            Assert.AreEqual(false, json["gates"]?["pipeline_non_experimental"]?["passed"]?.ToObject<bool>());
            Assert.AreEqual(false, json["gates"]?["stable_release_cycle_after_m4"]?["passed"]?.ToObject<bool>());
            Assert.AreEqual(true, json["gates"]?["capture_nexus_owned"]?["passed"]?.ToObject<bool>());
        }

        [Test]
        public void ParseUnknownRuntimeAsLegacy()
        {
            Assert.AreEqual(NexusRuntimeMode.Pipeline, NexusRuntimeSelector.Parse("pipeline"));
            Assert.AreEqual(NexusRuntimeMode.Auto, NexusRuntimeSelector.Parse("AUTO"));
            Assert.AreEqual(NexusRuntimeMode.Legacy, NexusRuntimeSelector.Parse("nope"));
            Assert.AreEqual(NexusRuntimeMode.Legacy, NexusRuntimeSelector.Parse(null));
        }

        [Test]
        public void ApplyPortFileReadsPipelineSessionMetadata()
        {
            string path = Path.Combine(Path.GetTempPath(), "nexus-pipeline-port-test.json");
            File.WriteAllText(path, "{\"pid\":12345,\"port\":7800,\"projectPath\":\"/tmp/demo\"}");
            try
            {
                var snapshot = new RuntimeCapabilitySnapshot();
                NexusRuntimeCapabilities.ApplyPortFile(snapshot, path);
                Assert.IsTrue(snapshot.PipelineSessionFilePresent);
                Assert.AreEqual(7800, snapshot.PipelinePort);
                Assert.AreEqual(12345, snapshot.PipelinePid);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void ProbeTcpDetectsListeningPortWithoutLongWait()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            try
            {
                Assert.IsTrue(NexusRuntimeCapabilities.ProbeTcp(port, 50));
            }
            finally
            {
                listener.Stop();
            }

            Assert.IsFalse(NexusRuntimeCapabilities.ProbeTcp(port, 50));
        }

        [Test]
        public void ProbeMarksStalePidUnhealthyWithoutTcp()
        {
            var seed = EligibleSnapshot();
            seed.PipelinePid = 99999999;
            seed.PipelinePort = 1;
            RuntimeCapabilitySnapshot result = NexusRuntimeCapabilities.Probe(seed);
            Assert.IsFalse(result.PipelineHealthy);
            StringAssert.Contains("stale port file", result.Detail);
        }

        [Test]
        public void RefreshPortFileClearsMissingSession()
        {
            string path = Path.Combine(Path.GetTempPath(), "nexus-missing-pipeline-port.json");
            if (File.Exists(path)) File.Delete(path);
            var snapshot = new RuntimeCapabilitySnapshot
            {
                PipelineSessionFilePresent = true,
                PipelinePort = 7800,
                PipelinePid = 12
            };
            NexusRuntimeCapabilities.RefreshPortFile(snapshot, path);
            Assert.IsFalse(snapshot.PipelineSessionFilePresent);
            Assert.IsNull(snapshot.PipelinePort);
            Assert.IsNull(snapshot.PipelinePid);
        }

        [Test]
        public void CurrentEditorPidLooksLikeUnity()
        {
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            Assert.IsTrue(NexusRuntimeCapabilities.IsLikelyUnityEditorProcess(pid));
        }

        [Test]
        public void ProbeMarksMissingPackageUnhealthy()
        {
            var seed = new RuntimeCapabilitySnapshot
            {
                PipelinePackagePresent = false,
                HealthUnknown = true
            };
            RuntimeCapabilitySnapshot result = NexusRuntimeCapabilities.Probe(seed);
            Assert.IsFalse(result.HealthUnknown);
            Assert.IsFalse(result.PipelineHealthy);
            StringAssert.Contains("not installed", result.Detail);
        }

        [Test]
        public void CommandsRemainIndependentOfTransportAdapters()
        {
            string commands = LocateCommands();
            Assert.IsNotNull(commands);
            foreach (string path in Directory.GetFiles(commands, "*.cs"))
            {
                string text = File.ReadAllText(path);
                Assert.IsFalse(text.Contains("HttpListener"), Path.GetFileName(path));
                Assert.IsFalse(text.Contains("Unity.Pipeline"), Path.GetFileName(path));
            }
        }

        private static RuntimeCapabilitySnapshot EligibleSnapshot()
        {
            return new RuntimeCapabilitySnapshot
            {
                UnityVersionSupported = true,
                PipelinePackagePresent = true,
                NexusCommandsRegistered = true,
                PipelineSessionFilePresent = true,
                PipelineHealthy = true,
                HealthUnknown = false
            };
        }

        private static string LocateCommands()
        {
            string[] fallbacks =
            {
                Path.Combine(UnityEngine.Application.dataPath, "NexusUnity", "Editor", "Commands"),
                Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "com.forkhorizon.nexus.unity", "Editor", "Commands")
            };
            foreach (string path in fallbacks)
            {
                if (Directory.Exists(path)) return path;
            }
            return null;
        }
    }
}
