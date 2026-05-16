using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    public class ToolRegistryConsistencyTests
    {
        private static readonly HashSet<string> InternalRpcMethods = new HashSet<string>
        {
            "initialize",
            "list_tools",
            "is_asset_import_idle",
            "is_editor_idle"
        };

        [SetUp]
        public void InitRegistry()
        {
            typeof(MCPServerMethods)
                .GetMethod("Init", BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, null);
        }

        [Test]
        public void ListTools_AllToolsHaveRegisteredHandlers()
        {
            var toolNames = GetListToolNames();
            var registeredNames = GetRegisteredMethodNames();

            var missingHandlers = toolNames.Except(registeredNames).OrderBy(n => n).ToArray();

            Assert.IsEmpty(missingHandlers, "Every public tool returned by list_tools must have a registered RPC handler.");
        }

        [Test]
        public void RegisteredHandlers_ArePublicOrExplicitlyInternal()
        {
            var toolNames = GetListToolNames();
            var registeredNames = GetRegisteredMethodNames();

            var hiddenHandlers = registeredNames
                .Except(toolNames)
                .Except(InternalRpcMethods)
                .OrderBy(n => n)
                .ToArray();

            Assert.IsEmpty(hiddenHandlers, "Registered RPC handlers should be listed publicly unless they are explicit internal protocol helpers.");
        }

        [Test]
        public void PythonBridgeStaticFallback_MatchesUnityToolCatalog()
        {
            var toolNames = GetListToolNames();
            var bridgeNames = GetBridgeStaticToolNames();

            CollectionAssert.AreEquivalent(toolNames, bridgeNames, "The Python bridge offline fallback must expose the same tool names as Unity list_tools.");
        }

        private static HashSet<string> GetRegisteredMethodNames()
        {
            var field = typeof(MCPServerMethods).GetField("_methods", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field, "Could not find MCPServerMethods._methods.");

            var methods = (IDictionary)field.GetValue(null);
            return new HashSet<string>(methods.Keys.Cast<object>().Select(k => k.ToString()));
        }

        private static HashSet<string> GetListToolNames()
        {
            var listTools = typeof(MCPServerMethods).GetMethod("ListTools", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(listTools, "Could not find MCPServerMethods.ListTools.");

            string json = listTools.Invoke(null, new object[] { null }).ToString();
            return ExtractJsonNames(json, stripUnityPrefix: false);
        }

        private static HashSet<string> GetBridgeStaticToolNames()
        {
            string bridgePath = Path.Combine(Application.dataPath, "NexusUnity", "Editor", "nexus_unity_bridge.py");
            Assert.IsTrue(File.Exists(bridgePath), $"Bridge file not found: {bridgePath}");

            string source = File.ReadAllText(bridgePath);
            return ExtractJsonNames(source, stripUnityPrefix: true);
        }

        private static HashSet<string> ExtractJsonNames(string text, bool stripUnityPrefix)
        {
            var names = new HashSet<string>();
            foreach (Match match in Regex.Matches(text, "\"name\"\\s*:\\s*\"([^\"]+)\""))
            {
                string name = match.Groups[1].Value;
                if (stripUnityPrefix)
                {
                    if (!name.StartsWith("unity_", StringComparison.Ordinal))
                        continue;
                    name = name.Substring("unity_".Length);
                }
                names.Add(name);
            }

            return names;
        }
    }
}
