using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Commands;

namespace UnityMCP.Editor.Tests
{
    public class OpenSourceApiContractTests
    {
        private static readonly HashSet<string> InternalRegisteredMethods = new HashSet<string>
        {
            "initialize",
            "list_tools",
            "is_asset_import_idle",
            "is_editor_idle"
        };

        private static readonly HashSet<string> ManagerTools = new HashSet<string>
        {
            "scene_manager",
            "hierarchy_manager",
            "component_manager",
            "search_manager",
            "asset_manager",
            "editor_controller",
            "ui_automation",
            "wait",
            "playerprefs_manager"
        };

        private static readonly HashSet<string> BridgeOnlyTools = new HashSet<string>
        {
            "write_and_compile"
        };

        [SetUp]
        public void InitRegistry()
        {
            MCPServerMethods.Init();
        }

        [Test]
        public void RawListToolsMatchesDispatchRegistry()
        {
            HashSet<string> listed = GetRawToolNames();
            HashSet<string> registered = GetRegisteredMethodNames();

            var dispatchOnlyAliases = new HashSet<string>
            {
                ProjectMapCommand.Alias,
                GroupCompileErrorsCommand.Alias,
                CaptureGameViewCommand.Alias
            };
            CollectionAssert.IsEmpty(listed.Except(registered).OrderBy(x => x).ToArray(), "Every raw listed tool must be dispatchable.");
            CollectionAssert.IsEmpty(
                registered.Except(InternalRegisteredMethods).Except(listed).Except(dispatchOnlyAliases).OrderBy(x => x).ToArray(),
                "Every public registered raw method must be listed unless it is a canonical transport alias.");
            Assert.AreEqual(120, listed.Count, "Open source API docs assume 117 raw tools plus 3 canonical Nexus command ids.");
        }

        [Test]
        public void BridgeStaticToolsMatchPublicMcpSurface()
        {
            HashSet<string> bridgeTools = GetBridgeStaticToolNames();
            var expected = new HashSet<string>(ManagerTools.Concat(BridgeOnlyTools).Concat(new[]
            {
                "invoke_method",
                "dump_scene_graph",
                "get_scene_dependencies",
                "lint_project"
            }));

            CollectionAssert.AreEquivalent(expected.OrderBy(x => x).ToArray(), bridgeTools.OrderBy(x => x).ToArray());
            Assert.AreEqual(14, bridgeTools.Count, "Open source MCP bridge surface must stay intentionally compact.");
        }

        [Test]
        public void BridgeRoutingCallsRegisteredRawMethods()
        {
            HashSet<string> registered = GetRegisteredMethodNames();
            string routing = File.ReadAllText(FindPackageFile("Editor/nexus_bridge/routing.py"));
            var routedMethods = Regex.Matches(routing, @"call_unity\(""([^""]+)""")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            CollectionAssert.IsEmpty(routedMethods.Except(registered).OrderBy(x => x).ToArray(), "Every call_unity target in routing.py must be registered by Unity.");
        }

        [Test]
        public void BridgeDirectToolsAreRegisteredOrBridgeMacros()
        {
            HashSet<string> registered = GetRegisteredMethodNames();
            HashSet<string> bridgeTools = GetBridgeStaticToolNames();
            var directTools = bridgeTools.Except(ManagerTools).Except(BridgeOnlyTools);

            CollectionAssert.IsEmpty(directTools.Except(registered).OrderBy(x => x).ToArray(), "Every direct MCP bridge tool must map to a registered raw Unity method unless it is an explicit bridge macro.");
        }

        [Test]
        public void ApiReferenceDocumentsRawAndBridgeSurfaces()
        {
            string api = File.ReadAllText(FindPackageFile("API_REFERENCE.MD"));
            HashSet<string> rawTools = GetRawToolNames();
            HashSet<string> bridgeTools = GetBridgeStaticToolNames();

            foreach (string raw in rawTools)
            {
                StringAssert.Contains($"`{raw}` / `unity_{raw}`", api, $"API_REFERENCE.MD must document raw tool {raw}.");
            }

            foreach (string bridge in bridgeTools)
            {
                StringAssert.Contains($"### `unity_{bridge}`", api, $"API_REFERENCE.MD must document MCP bridge tool unity_{bridge}.");
            }
        }

        [Test]
        public void RawSchemasDocumentFlexibleComponentAndReflectionInputs()
        {
            JObject updateComponent = GetRawTool("update_component");
            JObject updateProps = (JObject)updateComponent["inputSchema"]["properties"];
            Assert.IsTrue(updateProps.ContainsKey("properties"), "update_component must document the preferred properties object input.");
            Assert.IsTrue(updateProps.ContainsKey("json_data"), "update_component must retain the legacy json_data input.");
            CollectionAssert.AreEquivalent(
                new[] { "instance_id", "component_name" },
                updateComponent["inputSchema"]["required"].Select(t => t.ToString()).ToArray(),
                "update_component should not require only one of its two supported data shapes."
            );

            JObject invokeMethod = GetRawTool("invoke_method");
            JToken argumentsSchema = invokeMethod["inputSchema"]["properties"]["arguments"];
            Assert.AreEqual(JTokenType.Object, argumentsSchema.Type, "invoke_method.arguments must be a schema object, not a raw array.");
            Assert.AreEqual("array", argumentsSchema["type"]?.ToString());
        }

        private static HashSet<string> GetRawToolNames()
        {
            string response = MCPServerMethods.ProcessJsonRpc("{\"jsonrpc\":\"2.0\",\"method\":\"list_tools\",\"params\":{},\"id\":1}");
            JObject parsed = JObject.Parse(response);
            return parsed["result"]
                .Select(t => t["name"]?.ToString())
                .Where(n => !string.IsNullOrEmpty(n))
                .ToHashSet();
        }

        private static JObject GetRawTool(string name)
        {
            string response = MCPServerMethods.ProcessJsonRpc("{\"jsonrpc\":\"2.0\",\"method\":\"list_tools\",\"params\":{},\"id\":1}");
            JObject parsed = JObject.Parse(response);
            var tool = parsed["result"].FirstOrDefault(t => t["name"]?.ToString() == name) as JObject;
            Assert.IsNotNull(tool, $"Could not find raw tool {name}.");
            return tool;
        }

        private static HashSet<string> GetRegisteredMethodNames()
        {
            FieldInfo field = typeof(MCPServerMethods).GetField("_methods", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "MCPServerMethods._methods field must exist.");
            var methods = (IDictionary)field.GetValue(null);
            var names = methods.Keys.Cast<string>().ToHashSet();
            FieldInfo asyncField = typeof(MCPServerMethods).GetField("_asyncMethods", BindingFlags.NonPublic | BindingFlags.Static);
            if (asyncField?.GetValue(null) is IDictionary asyncMethods)
            {
                foreach (string key in asyncMethods.Keys.Cast<string>())
                    names.Add(key);
            }
            return names;
        }

        private static HashSet<string> GetBridgeStaticToolNames()
        {
            string schemas = FindPackageFile("Editor/nexus_bridge/schemas.py");
            string directory = Path.GetDirectoryName(schemas);
            var names = new HashSet<string>();
            foreach (string file in Directory.GetFiles(directory, "*.py"))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"""name""\s*:\s*""unity_([^""]+)"""))
                    names.Add(match.Groups[1].Value);
            }

            return names;
        }

        private static string FindPackageFile(string relativePath)
        {
            string normalized = relativePath.Replace("\\", "/");
            string assetPath = AssetDatabase.GetAllAssetPaths()
                .FirstOrDefault(path => path.Replace("\\", "/").EndsWith(normalized, StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(assetPath))
                return Path.GetFullPath(assetPath);

            string[] roots =
            {
                Path.Combine(Application.dataPath, "NexusUnity"),
                Path.Combine(Application.dataPath, "..", "Packages", "com.forkhorizon.nexus.unity")
            };
            foreach (string root in roots)
            {
                string candidate = Path.Combine(root, normalized);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }

            Assert.Fail($"Could not find package file: {relativePath}");
            return null;
        }
    }
}
