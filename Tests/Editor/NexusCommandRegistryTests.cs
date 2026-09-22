using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Commands;

namespace UnityMCP.Editor.Tests
{
    public class NexusCommandRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            MCPServerMethods.Init();
        }

        [Test]
        public void RegistryExposesOnlyTheThreePocCommands()
        {
            string[] ids = NexusCommandRegistry.All.Select(c => c.Descriptor.Id).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { ProjectMapCommand.Id, GroupCompileErrorsCommand.Id, CaptureGameViewCommand.Id },
                ids);
        }

        [Test]
        public void CanonicalIdAndAliasShareTheSameHandlerInstance()
        {
            Assert.AreSame(
                NexusCommandRegistry.Get(ProjectMapCommand.Id),
                NexusCommandRegistry.Get(ProjectMapCommand.Alias));
            Assert.AreSame(
                NexusCommandRegistry.Get(GroupCompileErrorsCommand.Id),
                NexusCommandRegistry.Get(GroupCompileErrorsCommand.Alias));
            Assert.AreSame(
                NexusCommandRegistry.Get(CaptureGameViewCommand.Id),
                NexusCommandRegistry.Get(CaptureGameViewCommand.Alias));
        }

        [Test]
        public void ListToolsProjectsSharedDescriptionsAndKeepsLegacyScreenshot()
        {
            JObject response = Rpc("list_tools");
            JArray tools = response["result"] as JArray ?? (JArray)response["result"]?["tools"];
            Assert.IsNotNull(tools);

            JObject canonical = FindTool(tools, ProjectMapCommand.Id);
            Assert.IsNotNull(canonical);
            Assert.IsNull(FindTool(tools, ProjectMapCommand.Alias), "Transport aliases stay dispatchable but are not advertised twice.");
            Assert.AreEqual(ProjectMapCommand.Description, canonical["description"]?.ToString());
            Assert.AreEqual(
                CaptureGameViewCommand.Description,
                FindTool(tools, CaptureGameViewCommand.Id)["description"]?.ToString());
            Assert.IsNotNull(FindTool(tools, "capture_game_view_screenshot"));
            Assert.Less(tools.ToString().Length, 80_000, "Visible schema should stay well below Unity MCP's ~100KB dump.");
        }

        [Test]
        public void CoreProfileOmitsCaptureAndDoesNotDuplicateAliases()
        {
            JObject response = Rpc("list_tools", new JObject { ["profile"] = "core" });
            JArray tools = response["result"] as JArray;
            Assert.IsNotNull(tools);
            string[] names = tools.OfType<JObject>().Select(t => t["name"]?.ToString()).ToArray();
            CollectionAssert.Contains(names, ProjectMapCommand.Id);
            CollectionAssert.Contains(names, GroupCompileErrorsCommand.Id);
            CollectionAssert.DoesNotContain(names, CaptureGameViewCommand.Id);
            CollectionAssert.DoesNotContain(names, ProjectMapCommand.Alias);
        }

        [Test]
        public void VisualProfileListsCanonicalCaptureOnly()
        {
            JObject response = Rpc("list_tools", new JObject { ["profile"] = "visual" });
            JArray tools = response["result"] as JArray;
            Assert.IsNotNull(tools);
            string[] names = tools.OfType<JObject>().Select(t => t["name"]?.ToString()).ToArray();
            CollectionAssert.Contains(names, CaptureGameViewCommand.Id);
            CollectionAssert.Contains(names, "capture_game_view_screenshot");
            CollectionAssert.DoesNotContain(names, CaptureGameViewCommand.Alias);
            CollectionAssert.DoesNotContain(names, "capture_inspector_screenshot");
        }

        [Test]
        public void ProjectMapHandlerReturnsEditorProjectMetadata()
        {
            NexusProjectMapResult result = ProjectMapCommand.Execute();
            Assert.IsTrue(result.Success);
            Assert.AreEqual(Application.unityVersion, result.UnityVersion);
            Assert.IsFalse(string.IsNullOrEmpty(result.ProjectPath));
        }

        [Test]
        public void GroupCompileErrorsHandlerReturnsStructuredGroups()
        {
            NexusCompileErrorsResult result = GroupCompileErrorsCommand.Execute(20);
            Assert.IsTrue(result.Success);
            Assert.GreaterOrEqual(result.TotalErrors, 0);
            Assert.AreEqual(result.GroupCount, result.Groups?.Count ?? 0);
        }

        [Test]
        public void CanonicalCommandSourcesDoNotReferencePipelineOrHttpListener()
        {
            string directory = Locate("ProjectMapCommand");
            Assert.IsNotNull(directory);
            string[] banned = { "Unity.Pipeline", "HttpListener", "System.Net.HttpListener" };
            foreach (string path in Directory.GetFiles(directory, "*.cs"))
            {
                string text = File.ReadAllText(path);
                foreach (string token in banned)
                    Assert.IsFalse(text.Contains(token), $"{Path.GetFileName(path)} contains {token}");
            }
        }

        [Test]
        public void CoreEditorAssemblyHasNoHardPipelineReference()
        {
            string asmdef = File.ReadAllText(LocateAsmdef("UnityMCP.Editor.asmdef"));
            Assert.IsFalse(asmdef.Contains("Unity.Pipeline"));
            string pipeline = File.ReadAllText(LocateAsmdef("UnityMCP.Editor.Pipeline.asmdef"));
            StringAssert.Contains("Unity.Pipeline", pipeline);
            StringAssert.Contains("NEXUS_HAS_PIPELINE", pipeline);
        }

        private static JObject FindTool(JArray tools, string name)
        {
            return tools.OfType<JObject>().FirstOrDefault(t => t["name"]?.ToString() == name);
        }

        private static JObject Rpc(string method, JObject parameters = null)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters ?? new JObject(),
                ["id"] = 1
            };
            return JObject.Parse(MCPServerMethods.ProcessJsonRpc(request.ToString(Formatting.None)));
        }

        private static string Locate(string scriptName)
        {
            string[] fallbacks =
            {
                Path.Combine(Application.dataPath, "NexusUnity", "Editor", "Commands"),
                Path.Combine(Application.dataPath, "..", "Packages", "com.forkhorizon.nexus.unity", "Editor", "Commands")
            };
            return fallbacks.FirstOrDefault(Directory.Exists);
        }

        private static string LocateAsmdef(string fileName)
        {
            string commands = Locate("ProjectMapCommand");
            string editor = Path.GetDirectoryName(commands);
            if (fileName == "UnityMCP.Editor.asmdef")
                return Path.Combine(editor, fileName);
            return Path.Combine(editor, "Pipeline", fileName);
        }
    }
}
