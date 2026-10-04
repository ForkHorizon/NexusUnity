using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Capture;
using UnityMCP.Editor.Commands;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Locks Pipeline <c>[CliCommand]</c> help to the canonical descriptors.
    /// Removing, renaming, reordering, retyping, or retargeting a parameter fails this test.
    /// </summary>
    public class NexusCommandCliDriftTests
    {
        [Test]
        public void CliArgsMatchCanonicalDescriptors()
        {
            Type commands = FindType("UnityMCP.Editor.Pipeline", "UnityMCP.Editor.Pipeline.NexusPipelineCommands");
            Type cliCommand = FindType("Unity.Pipeline", "Unity.Pipeline.Commands.CliCommandAttribute");
            if (cliCommand == null)
            {
                Assert.Ignore("com.unity.pipeline is not loaded. CLI drift is checked when Pipeline is installed.");
                return;
            }

            Assert.IsNotNull(commands, "Pipeline is loaded but the Nexus CLI projection did not compile.");
            MethodInfo[] methods = commands.Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(method => AttributeNamed(method, "CliCommandAttribute") != null)
                .ToArray();

            Assert.AreEqual(NexusCommandRegistry.All.Count, methods.Length, "Each canonical command has one CLI wrapper.");
            var seen = new HashSet<string>();
            foreach (MethodInfo method in methods)
            {
                object commandAttribute = AttributeNamed(method, "CliCommandAttribute");
                string cliName = (string)Property(commandAttribute, "Name");
                string help = (string)Property(commandAttribute, "Description");
                Assert.IsTrue(seen.Add(cliName), "Duplicate CLI command " + cliName);

                INexusCommand canonical = NexusCommandRegistry.All.SingleOrDefault(
                    command => command.Descriptor.Aliases != null && command.Descriptor.Aliases.Length > 0 &&
                               command.Descriptor.Aliases[0] == cliName);
                Assert.IsNotNull(canonical, "CLI command " + cliName + " is not the primary alias of a canonical command.");
                Assert.AreEqual(canonical.Descriptor.Description, help, cliName + " help text drifted.");
                Assert.AreNotEqual(CaptureGameViewCommand.LookAlias, cliName);
                Assert.AreNotEqual(CaptureGameViewCommand.LookDotAlias, cliName);
                AssertParameters(cliName, method, canonical.Descriptor.Parameters);
            }

            CollectionAssert.AreEquivalent(
                new[] { ProjectMapCommand.Alias, GroupCompileErrorsCommand.Alias, CaptureGameViewCommand.Alias },
                seen.ToArray());
        }

        [Test]
        public void OmittedHttpArgumentsUseTheSameDefaults()
        {
            NexusCommandParameter[] look = new CaptureGameViewCommand().Descriptor.Parameters;
            Assert.AreEqual(0, DefaultOf(look, "width"));
            Assert.AreEqual(0, DefaultOf(look, "height"));
            Assert.AreEqual(0, DefaultOf(look, "instance_id"));
            Assert.AreEqual("jpg", DefaultOf(look, "format"));
            Assert.AreEqual(85, DefaultOf(look, "quality"));
            Assert.AreEqual(768, DefaultOf(look, "max_dimension"));
            Assert.AreEqual("game", DefaultOf(look, "view"));
            Assert.AreEqual("base64", DefaultOf(look, "delivery"));
            Assert.IsNull(DefaultOf(look, "target"));
            Assert.IsNull(DefaultOf(look, "request_id"));

            Assert.IsTrue(CaptureLookRequest.TryParse(new JObject(), 0, out CaptureLookCall call));
            Assert.IsNull(call.Rejected);
            Assert.AreEqual(DefaultOf(look, "width"), call.Request.RequestedWidth);
            Assert.AreEqual(DefaultOf(look, "height"), call.Request.RequestedHeight);
            Assert.AreEqual(DefaultOf(look, "quality"), call.Request.JpegQuality);
            Assert.AreEqual(DefaultOf(look, "max_dimension"), call.Request.MaxLongEdge);
            Assert.AreEqual(DefaultOf(look, "instance_id"), call.Request.TargetInstanceId);
            Assert.IsNull(call.Request.TargetPath);
            Assert.AreEqual(CaptureView.Game, call.Request.View);
            Assert.AreEqual(DefaultOf(look, "view"), CaptureGameViewCommand.DefaultView);
            Assert.IsFalse(call.WriteFile);
            Assert.AreEqual(DefaultOf(look, "delivery"), CaptureGameViewCommand.DefaultDelivery);
            Assert.IsNull(call.Format);
            Assert.AreEqual(DefaultOf(look, "format"), CaptureGameViewCommand.FormatOrDefault(call.Format));

            NexusCommandParameter[] compile = new GroupCompileErrorsCommand().Descriptor.Parameters;
            Assert.AreEqual(50, DefaultOf(compile, "max_logs"));
            ParameterInfo executeMaxLogs = typeof(GroupCompileErrorsCommand).GetMethod("Execute").GetParameters()[0];
            Assert.AreEqual(DefaultOf(compile, "max_logs"), executeMaxLogs.DefaultValue);
            Task<JToken> omitted = new GroupCompileErrorsCommand().ExecuteAsync(new JObject(), CancellationToken.None);
            Assert.IsTrue(omitted.IsCompleted);
            JObject fromHttp = (JObject)omitted.Result;
            JObject fromDefault = GroupCompileErrorsCommand.ExecuteEnveloped((int)DefaultOf(compile, "max_logs"), null).ToJson();
            fromHttp["meta"]["ms"] = 0;
            fromDefault["meta"]["ms"] = 0;
            fromHttp["data"]["execution_duration_ms"] = 0;
            fromDefault["data"]["execution_duration_ms"] = 0;
            Assert.AreEqual(fromDefault.ToString(Formatting.None), fromHttp.ToString(Formatting.None));
        }

        [Test]
        public void CanonicalSchemasStayFreeOfCliDefaultFields()
        {
            string asmdefPath = Path.Combine(PackageRoot(), "Tests", "Editor", "UnityMCP.Editor.Tests.asmdef");
            string asmdef = File.ReadAllText(asmdefPath);
            Assert.IsFalse(asmdef.Contains("Unity.Pipeline"), "The test assembly must not take a hard Pipeline reference.");
            Assert.IsFalse(asmdef.Contains("UnityMCP.Editor.Pipeline"));

            MCPServerMethods.Init();
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "list_tools",
                ["params"] = new JObject(),
                ["id"] = 1
            };
            JObject response = JObject.Parse(MCPServerMethods.ProcessJsonRpc(request.ToString(Formatting.None)));
            JArray tools = response["result"] as JArray;
            Assert.IsNotNull(tools);
            Assert.AreEqual(120, tools.Count);
            foreach (string id in new[] { ProjectMapCommand.Id, GroupCompileErrorsCommand.Id, CaptureGameViewCommand.Id })
            {
                JObject tool = tools.OfType<JObject>().Single(item => item["name"]?.ToString() == id);
                JObject properties = (JObject)tool["inputSchema"]["properties"];
                foreach (JProperty property in properties.Properties())
                {
                    Assert.IsFalse(((JObject)property.Value).ContainsKey("default"), id + "." + property.Name);
                }
            }
        }

        private static void AssertParameters(string command, MethodInfo method, NexusCommandParameter[] expected)
        {
            ParameterInfo[] actual = method.GetParameters();
            Assert.AreEqual(expected.Length, actual.Length, command + " parameter count drifted.");
            for (int i = 0; i < expected.Length; i++)
            {
                NexusCommandParameter spec = expected[i];
                ParameterInfo parameter = actual[i];
                object cliArg = AttributeNamed(parameter, "CliArgAttribute");
                Assert.IsNotNull(cliArg, command + " parameter " + parameter.Name + " is missing [CliArg].");
                Assert.AreEqual(spec.Name, (string)Property(cliArg, "Name"), command + " parameter " + i);
                Assert.AreEqual(spec.Description, (string)Property(cliArg, "Description"), command + " " + spec.Name);
                Assert.IsFalse((bool)Property(cliArg, "Required"), command + " " + spec.Name + " must stay optional.");
                Assert.AreEqual(ExactClrType(spec.JsonType), parameter.ParameterType, command + " " + spec.Name);
                Assert.IsTrue(spec.HasDefault, command + " " + spec.Name + " descriptor omitted its default.");
                Assert.IsTrue(parameter.HasDefaultValue, command + " " + spec.Name + " CLI parameter omitted its default.");
                object value = parameter.DefaultValue is DBNull ? null : parameter.DefaultValue;
                Assert.AreEqual(spec.DefaultValue, value, command + " " + spec.Name + " default drifted.");
            }
        }

        private static object DefaultOf(NexusCommandParameter[] parameters, string name)
        {
            return parameters.Single(parameter => parameter.Name == name).DefaultValue;
        }

        private static Type ExactClrType(string jsonType)
        {
            if (jsonType == "integer") return typeof(int);
            if (jsonType == "string") return typeof(string);
            if (jsonType == "boolean") return typeof(bool);
            if (jsonType == "number") return typeof(float);
            Assert.Fail("Unknown canonical JSON type " + jsonType);
            return null;
        }

        private static object AttributeNamed(ICustomAttributeProvider provider, string typeName)
        {
            return provider.GetCustomAttributes(false).FirstOrDefault(attribute => attribute.GetType().Name == typeName);
        }

        private static object Property(object attribute, string name)
        {
            return attribute.GetType().GetProperty(name).GetValue(attribute);
        }

        private static string CommandsDirectory()
        {
            string[] fallbacks =
            {
                Path.Combine(Application.dataPath, "NexusUnity", "Editor", "Commands"),
                Path.Combine(Application.dataPath, "..", "Packages", "com.forkhorizon.nexus.unity", "Editor", "Commands")
            };
            return fallbacks.First(Directory.Exists);
        }

        private static string PackageRoot()
        {
            return Path.GetDirectoryName(Path.GetDirectoryName(CommandsDirectory()));
        }

        private static Type FindType(string assemblyName, string fullName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => candidate.GetName().Name == assemblyName);
            return assembly == null ? null : assembly.GetType(fullName);
        }
    }
}
