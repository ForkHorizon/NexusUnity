// EditMode tests that lock Pipeline CLI parameters to the canonical descriptors.
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
using UnityMCP.Editor;
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
        private const string CliCommandFullName = "Unity.Pipeline.Commands.CliCommandAttribute";
        private const string CliArgFullName = "Unity.Pipeline.Commands.CliArgAttribute";

        /// <summary>Pipeline 1.0.0 executor options, read before any command parameter binds. No Nexus flag may use one.</summary>
        private static readonly string[] ReservedPipelineOptions = { "format", "json", "fields", "verbose", "omit-nulls", "log-level", "log-format" };

        [Test]
        public void CliArgsMatchCanonicalDescriptors()
        {
            const string attributeName = CliCommandFullName;
            bool pipelineLoaded = AssemblyLoaded("Unity.Pipeline") || AssemblyLoaded("Unity.Pipeline.Attributes");
            Type cliCommand = FindType("Unity.Pipeline", attributeName) ?? FindType("Unity.Pipeline.Attributes", attributeName);
            if (!pipelineLoaded)
            {
                Assert.Ignore("com.unity.pipeline is not loaded. CLI drift is checked when Pipeline is installed.");
                return;
            }

            Assert.IsNotNull(cliCommand, "Pipeline is loaded but CliCommandAttribute is not in Unity.Pipeline or Unity.Pipeline.Attributes.");
            Type commands = FindType("UnityMCP.Editor.Pipeline", "UnityMCP.Editor.Pipeline.NexusPipelineCommands");
            Assert.IsNotNull(commands, "Pipeline is loaded but the Nexus CLI projection did not compile.");
            MethodInfo[] methods = commands.Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(method => AttributeNamed(method, CliCommandFullName) != null)
                .ToArray();

            Assert.AreEqual(NexusCommandRegistry.All.Count, methods.Length, "Each canonical command has one CLI wrapper.");
            var seen = new HashSet<string>();
            foreach (MethodInfo method in methods)
            {
                object commandAttribute = AttributeNamed(method, CliCommandFullName);
                Assert.AreSame(cliCommand, commandAttribute.GetType(), "A decoy attribute with the same short name is not the Pipeline CliCommand.");
                string cliName = CliAlias(commandAttribute);
                AssertGroupAndEffect(cliName, commandAttribute);
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
                new[] { ProjectMapCommand.Alias, GroupCompileErrorsCommand.Alias, CaptureGameViewCommand.Alias, AssetImpactCommand.Alias },
                seen.ToArray());
            Assert.AreEqual("nexus_look", CaptureGameViewCommand.LookAlias);
            Assert.AreEqual("nexus.look", CaptureGameViewCommand.LookDotAlias);
            AssertCliCompileBodyUsesItsMaxLogsArgument(methods);
            AssertCliCaptureBodyReachesTheGateway(methods);
            AssertCliCaptureForwardsViewAndDelivery(methods);
        }

        /// <summary>
        /// The Nexus alias a CLI command answers to: <c>nexus capture_game_view</c> on Pipeline 1.0.0+ maps to
        /// <c>nexus_capture_game_view</c>; older Pipelines register that global name directly.
        /// </summary>
        private static string CliAlias(object commandAttribute)
        {
            PropertyInfo group = commandAttribute.GetType().GetProperty("Group");
            string name = (string)Property(commandAttribute, "Name");
            string groupName = group == null ? null : (string)group.GetValue(commandAttribute);
            return groupName == null || groupName == "global" ? name : groupName + "_" + name;
        }

        /// <summary>Pipeline 1.0.0+ exposes Group and Effect: every Nexus command is grouped and read-only there.</summary>
        private static void AssertGroupAndEffect(string cliName, object commandAttribute)
        {
            PropertyInfo effect = commandAttribute.GetType().GetProperty("Effect");
            if (effect == null) return;
            Assert.AreEqual("ReadOnly", effect.GetValue(commandAttribute).ToString(), cliName + " must declare CommandEffect.ReadOnly.");
            Assert.AreEqual("nexus", (string)Property(commandAttribute, "Group"), cliName + " must be in the nexus group.");
        }

        private static MethodInfo CliMethod(MethodInfo[] methods, string alias)
        {
            return methods.Single(method => CliAlias(AttributeNamed(method, CliCommandFullName)) == alias);
        }

        private static void AssertCliCompileBodyUsesItsMaxLogsArgument(MethodInfo[] methods)
        {
            MethodInfo compile = CliMethod(methods, GroupCompileErrorsCommand.Alias);
            int seen = -1;
            Func<int, List<LogEntry>> previous = GroupCompileErrorsCommand.ReadErrorLogs;
            try
            {
                GroupCompileErrorsCommand.ReadErrorLogs = count =>
                {
                    seen = count;
                    return new List<LogEntry>();
                };
                compile.Invoke(null, new object[] { 50, null });
                Assert.AreEqual(50, seen, "CLI compile wrapper did not pass max_logs through.");
                compile.Invoke(null, new object[] { 7, null });
                Assert.AreEqual(7, seen, "CLI compile wrapper ignored its max_logs argument.");
                compile.Invoke(null, new object[] { 0, null });
                Assert.AreEqual(0, seen, "CLI max_logs 0 must stay 0, not fall back to the default 50.");
            }
            finally
            {
                GroupCompileErrorsCommand.ReadErrorLogs = previous;
            }
        }

        private static void AssertCliCaptureBodyReachesTheGateway(MethodInfo[] methods)
        {
            MethodInfo look = CliMethod(methods, CaptureGameViewCommand.Alias);
            ObserveCapture(delegate
            {
                Task omitted = (Task)look.Invoke(null, new object[] { 0, 0, "jpg", 85, 768, "game", null, 0, "base64", null });
                Assert.IsTrue(omitted.IsCompleted, "CLI omit did not reach the capture gateway.");
            }, Expect(CaptureFormat.Jpeg, 0, 0, 85, 768));
            ObserveCapture(delegate
            {
                Task specified = (Task)look.Invoke(null, new object[] { 12, 9, "png", 40, 100, "game", null, 0, "base64", null });
                Assert.IsTrue(specified.IsCompleted, "CLI arguments did not reach the capture gateway.");
            }, Expect(CaptureFormat.Png, 12, 9, 40, 100));
        }

        private static void AssertCliCaptureForwardsViewAndDelivery(MethodInfo[] methods)
        {
            MethodInfo look = CliMethod(methods, CaptureGameViewCommand.Alias);
            CaptureRequest seen = null;
            Func<CaptureRequest, CancellationToken, Task<CaptureResult>> previous = CaptureGameViewCommand.Capture;
            try
            {
                CaptureGameViewCommand.Capture = (request, token) =>
                {
                    seen = request;
                    return Task.FromResult(new CaptureResult { CompressedBytes = Array.Empty<byte>(), Format = request.Format });
                };
                var scene = (Task<NexusCommandEnvelope>)look.Invoke(null, new object[] { 0, 0, "jpg", 85, 768, "scene", "Main Camera", 0, "base64", null });
                Assert.IsTrue(scene.Result.Ok, "CLI scene view with a target must capture.");
                Assert.AreEqual(CaptureView.Scene, seen.View, "CLI wrapper dropped its view argument.");
                Assert.AreEqual("Main Camera", seen.TargetPath, "CLI wrapper dropped its target argument.");

                seen = null;
                AssertCliRejected(look, new object[] { 0, 0, "jpg", 85, 768, "sky", null, 0, "base64", null }, "view");
                AssertCliRejected(look, new object[] { 0, 0, "jpg", 85, 768, "game", null, 0, "inline", null }, "delivery");
                Assert.IsNull(seen, "A rejected view or delivery must not capture.");
            }
            finally
            {
                CaptureGameViewCommand.Capture = previous;
            }
        }

        private static void AssertCliRejected(MethodInfo look, object[] arguments, string argument)
        {
            var envelope = ((Task<NexusCommandEnvelope>)look.Invoke(null, arguments)).Result;
            Assert.IsFalse(envelope.Ok, "CLI wrapper did not forward " + argument + ".");
            Assert.AreEqual(NexusCommandCodes.InvalidArgument, envelope.Error.Code);
            StringAssert.Contains(argument, envelope.Error.Message);
        }

        [Test]
        public void OmittedHttpArgumentsUseTheSameDefaults()
        {
            NexusCommandParameter[] look = new CaptureGameViewCommand().Descriptor.Parameters;
            Assert.AreEqual(0, DefaultOf(look, "width"));
            Assert.AreEqual(0, DefaultOf(look, "height"));
            Assert.AreEqual(0, DefaultOf(look, "instance_id"));
            Assert.AreEqual("jpg", DefaultOf(look, "encoding"));
            Assert.IsFalse(look.Any(parameter => parameter.Name == "format"), "format is reserved by Pipeline; the descriptor uses encoding.");
            Assert.AreEqual(85, DefaultOf(look, "quality"));
            Assert.AreEqual(768, DefaultOf(look, "max_dimension"));
            Assert.AreEqual("game", DefaultOf(look, "view"));
            Assert.AreEqual("base64", DefaultOf(look, "delivery"));
            Assert.IsNull(DefaultOf(look, "target"));
            Assert.IsNull(DefaultOf(look, "request_id"));

            Assert.IsTrue(CaptureGameViewCommand.TryPrepareLook(new JObject(), 0, out CaptureLookCall call));
            Assert.IsNull(call.Rejected);
            Assert.IsNull(call.Format);
            Assert.AreEqual(0, call.Request.RequestedWidth);
            Assert.AreEqual(0, call.Request.RequestedHeight);
            Assert.AreEqual(85, call.Request.JpegQuality);
            Assert.AreEqual(768, call.Request.MaxLongEdge);
            Assert.AreEqual(0, call.Request.TargetInstanceId);
            Assert.IsNull(call.Request.TargetPath);
            Assert.AreEqual(CaptureView.Game, call.Request.View);
            Assert.IsFalse(call.WriteFile);
            Assert.AreEqual(CaptureFormat.Jpeg, call.Request.Format);

            var explicitLook = new JObject { ["width"] = 12, ["encoding"] = "png", ["max_dimension"] = 100 };
            Assert.IsTrue(CaptureGameViewCommand.TryPrepareLook(explicitLook, 0, out CaptureLookCall specified));
            Assert.AreEqual(12, specified.Request.RequestedWidth);
            Assert.AreEqual(100, specified.Request.MaxLongEdge);
            Assert.AreEqual(CaptureFormat.Png, specified.Request.Format);

            JObject cliOmit = CaptureGameViewCommand.BuildCliArguments(new CaptureCliArguments { Quality = 85, MaxDimension = 768 });
            Assert.AreEqual(0, cliOmit.Value<int>("width"));
            Assert.AreEqual(0, cliOmit.Value<int>("height"));
            Assert.AreEqual(768, cliOmit.Value<int>("max_dimension"));
            Assert.IsNull(cliOmit["encoding"]);
            Assert.IsNull(cliOmit["format"]);
            Assert.IsTrue(CaptureGameViewCommand.TryPrepareLook(cliOmit, 0, out CaptureLookCall fromCli));
            Assert.AreEqual(CaptureFormat.Jpeg, fromCli.Request.Format);
            Assert.AreEqual(0, fromCli.Request.RequestedWidth);
            Assert.AreEqual(768, fromCli.Request.MaxLongEdge);

            string captureSource = File.ReadAllText(Path.Combine(CommandsDirectory(), "CaptureGameViewCommand.cs"));
            Assert.AreEqual(1, CountOf(captureSource, "call.Request.Format ="), "Format may be applied in exactly one place.");
            StringAssert.Contains("TryPrepareLook(", captureSource);
            StringAssert.Contains("BuildCliArguments(", captureSource);

            NexusCommandParameter[] compile = new GroupCompileErrorsCommand().Descriptor.Parameters;
            Assert.AreEqual(50, GroupCompileErrorsCommand.DefaultMaxLogs);
            Assert.AreEqual(50, DefaultOf(compile, "max_logs"));
            ParameterInfo executeMaxLogs = typeof(GroupCompileErrorsCommand).GetMethod("Execute").GetParameters()[0];
            Assert.AreEqual(50, executeMaxLogs.DefaultValue);
            Assert.IsTrue(GroupCompileErrorsCommand.TryReadCompileArguments(new JObject(), 0, out int omittedLogs, out string requestId, out NexusCommandEnvelope rejected));
            Assert.IsNull(rejected);
            Assert.IsNull(requestId);
            Assert.AreEqual(50, omittedLogs);
            Assert.IsTrue(GroupCompileErrorsCommand.TryReadCompileArguments(new JObject { ["max_logs"] = 7 }, 0, out int explicitLogs, out _, out _));
            Assert.AreEqual(7, explicitLogs);
            Assert.IsTrue(GroupCompileErrorsCommand.TryReadCompileArguments(new JObject { ["max_logs"] = 0 }, 0, out int zeroLogs, out _, out _));
            Assert.AreEqual(0, zeroLogs, "HTTP max_logs 0 must stay 0, not fall back to the default 50.");
            AssertDeprecatedCaptureAliases();
            AssertHttpCompileUsesTheResolvedWindow();
            AssertHttpCaptureOmitReachesTheGateway();
        }

        /// <summary>Locks the HTTP-only deprecated keys: each is read only when its canonical key is absent.</summary>
        private static void AssertDeprecatedCaptureAliases()
        {
            Assert.IsTrue(CaptureGameViewCommand.TryPrepareLook(new JObject { ["format"] = "png" }, 0, out CaptureLookCall legacyFormat));
            Assert.AreEqual(CaptureFormat.Png, legacyFormat.Request.Format, "Deprecated HTTP format must still select png.");
            Assert.IsTrue(CaptureGameViewCommand.TryPrepareLook(new JObject { ["encoding"] = "jpg", ["format"] = "png" }, 0, out CaptureLookCall encodingWins));
            Assert.AreEqual(CaptureFormat.Jpeg, encodingWins.Request.Format, "encoding must win over deprecated format.");
            Assert.IsTrue(CaptureGameViewCommand.TryPrepareLook(new JObject { ["max_long_edge"] = 512 }, 0, out CaptureLookCall legacyEdge));
            Assert.AreEqual(512, legacyEdge.Request.MaxLongEdge, "Deprecated HTTP max_long_edge must still apply.");
        }

        private static void AssertHttpCompileUsesTheResolvedWindow()
        {
            int seen = -1;
            Func<int, List<LogEntry>> previous = GroupCompileErrorsCommand.ReadErrorLogs;
            try
            {
                GroupCompileErrorsCommand.ReadErrorLogs = count =>
                {
                    seen = count;
                    return new List<LogEntry>();
                };
                Task<JToken> omitted = new GroupCompileErrorsCommand().ExecuteAsync(new JObject(), CancellationToken.None);
                Assert.IsTrue(omitted.IsCompleted);
                Assert.AreEqual(50, seen, "HTTP omit did not request the default console window.");
                Task<JToken> explicitWindow = new GroupCompileErrorsCommand().ExecuteAsync(new JObject { ["max_logs"] = 7 }, CancellationToken.None);
                Assert.IsTrue(explicitWindow.IsCompleted);
                Assert.AreEqual(7, seen, "HTTP max_logs did not reach the console read.");
                GroupCompileErrorsCommand.Execute();
                Assert.AreEqual(50, seen, "Execute() did not use its default window.");
                GroupCompileErrorsCommand.ReadErrorLogs = count => new List<LogEntry>
                {
                    new LogEntry { Message = "Assets/A.cs(3,1): error CS0103: x" },
                    new LogEntry { Message = "Assets/A.cs(4,1): error CS0103: y" },
                    new LogEntry { Message = "Assets/B.cs(1,1): warning CS0168: z" }
                };
                NexusCompileErrorsResult grouped = GroupCompileErrorsCommand.Execute(3);
                Assert.AreEqual(2, grouped.TotalErrors, "Execute must group the list ReadErrorLogs returned.");
                Assert.AreEqual(1, grouped.GroupCount);
                Assert.AreEqual("Assets/A.cs", grouped.Groups[0].File);
            }
            finally
            {
                GroupCompileErrorsCommand.ReadErrorLogs = previous;
            }
        }

        private static void AssertHttpCaptureOmitReachesTheGateway()
        {
            var command = new CaptureGameViewCommand();
            ObserveCapture(delegate
            {
                Task<JToken> omitted = command.ExecuteAsync(new JObject(), CancellationToken.None);
                Assert.IsTrue(omitted.IsCompleted, "HTTP omit did not reach the capture gateway.");
            }, Expect(CaptureFormat.Jpeg, 0, 0, 85, 768));
            ObserveCapture(delegate
            {
                Task<JToken> specified = command.ExecuteAsync(new JObject
                {
                    ["width"] = 12,
                    ["height"] = 9,
                    ["encoding"] = "png",
                    ["quality"] = 40,
                    ["max_dimension"] = 100
                }, CancellationToken.None);
                Assert.IsTrue(specified.IsCompleted, "HTTP arguments did not reach the capture gateway.");
            }, Expect(CaptureFormat.Png, 12, 9, 40, 100));
            ObserveCapture(delegate
            {
                Task<JToken> aliases = command.ExecuteAsync(new JObject
                {
                    ["w"] = 640,
                    ["h"] = 33,
                    ["jpeg_quality"] = 11
                }, CancellationToken.None);
                Assert.IsTrue(aliases.IsCompleted, "HTTP alias probe did not reach the capture gateway.");
            }, Expect(CaptureFormat.Jpeg, 0, 0, 85, 768));
            ObserveCapture(delegate
            {
                Task<NexusCommandEnvelope> cli = CaptureGameViewCommand.ExecuteFromCli(
                    new CaptureCliArguments { View = "game", Delivery = "base64" }, CancellationToken.None);
                Assert.IsTrue(cli.IsCompleted, "ExecuteFromCli omit did not reach the capture gateway.");
            }, Expect(CaptureFormat.Jpeg, 0, 0, 85, 768));
        }

        private static CaptureRequest Expect(CaptureFormat format, int width, int height, int quality, int edge)
        {
            return new CaptureRequest
            {
                Format = format,
                RequestedWidth = width,
                RequestedHeight = height,
                JpegQuality = quality,
                MaxLongEdge = edge
            };
        }

        private static void ObserveCapture(Action entry, CaptureRequest expected)
        {
            CaptureRequest seen = null;
            int calls = 0;
            Func<CaptureRequest, CancellationToken, Task<CaptureResult>> previous = CaptureGameViewCommand.Capture;
            try
            {
                CaptureGameViewCommand.Capture = (request, token) =>
                {
                    calls++;
                    seen = request;
                    return Task.FromResult(new CaptureResult
                    {
                        CompressedBytes = Array.Empty<byte>(),
                        Format = request.Format
                    });
                };
                entry();
                Assert.AreEqual(1, calls, "The capture entry point did not call the gateway once.");
                Assert.AreEqual(expected.Format, seen.Format);
                Assert.AreEqual(expected.RequestedWidth, seen.RequestedWidth);
                Assert.AreEqual(expected.RequestedHeight, seen.RequestedHeight);
                Assert.AreEqual(expected.JpegQuality, seen.JpegQuality);
                Assert.AreEqual(expected.MaxLongEdge, seen.MaxLongEdge);
            }
            finally
            {
                CaptureGameViewCommand.Capture = previous;
            }
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
            Assert.AreEqual(121, tools.Count);
            foreach (string id in new[] { ProjectMapCommand.Id, GroupCompileErrorsCommand.Id, CaptureGameViewCommand.Id, AssetImpactCommand.Id })
            {
                JObject tool = tools.OfType<JObject>().Single(item => item["name"]?.ToString() == id);
                JObject properties = (JObject)tool["inputSchema"]["properties"];
                JArray required = tool["inputSchema"]["required"] as JArray;
                Assert.IsTrue(required == null || required.Count == 0, id + " must stay optional.");
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
                object cliArg = AttributeNamed(parameter, CliArgFullName);
                Assert.IsNotNull(cliArg, command + " parameter " + parameter.Name + " is missing [CliArg].");
                string flag = (string)Property(cliArg, "Name");
                Assert.AreEqual(spec.Name.Replace('_', '-'), flag, command + " parameter " + i + " must be the kebab-case wire name.");
                CollectionAssert.DoesNotContain(ReservedPipelineOptions, flag, command + " --" + flag + " is reserved by Pipeline.");
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

        /// <summary>Matches by full name so a same-named decoy attribute in another namespace is not counted.</summary>
        private static object AttributeNamed(ICustomAttributeProvider provider, string typeName)
        {
            return provider.GetCustomAttributes(false).FirstOrDefault(attribute => attribute.GetType().FullName == typeName);
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

        private static int CountOf(string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static bool AssemblyLoaded(string assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Any(candidate => candidate.GetName().Name == assemblyName);
        }

        private static Type FindType(string assemblyName, string fullName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => candidate.GetName().Name == assemblyName);
            return assembly == null ? null : assembly.GetType(fullName);
        }
    }
}
