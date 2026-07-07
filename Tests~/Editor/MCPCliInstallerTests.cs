using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    public class MCPCliInstallerTests
    {
        [Test]
        public void CreateProcessStartInfoPreservesShellMetacharactersAsArguments()
        {
            var method = typeof(MCPCliInstaller).GetMethod(
                "CreateProcessStartInfo",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string[]) },
                null);

            var args = new[] { "mcp", "add", "nexus-unity", "--env", "TOKEN=a;$(touch bad)", "--", "/tmp/py thon", "/tmp/proj\";echo bad/nexus_unity_bridge.py" };
            var psi = (ProcessStartInfo)method.Invoke(null, new object[] { "/tmp/cli;echo bad", args });

            Assert.AreEqual("/tmp/cli;echo bad", psi.FileName);
            Assert.IsTrue(psi.Arguments.Contains("TOKEN=a;$(touch bad)"));
            Assert.IsTrue(psi.Arguments.Contains("/tmp/proj\";echo bad/nexus_unity_bridge.py"));
            Assert.IsFalse(psi.Arguments.Contains(" -c "));
            Assert.IsFalse(psi.UseShellExecute);
        }

        [Test]
        public void WindowsBatchArgumentsEscapeCmdMetacharacters()
        {
            var method = typeof(MCPCliInstaller).GetMethod(
                "BuildWindowsBatchArguments",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string[]) },
                null);

            var args = new[] { "mcp", "add", "nexus-unity", "a&b|c%PATH%" };
            string command = (string)method.Invoke(null, new object[] { @"C:\Tools\gemini.cmd", args });

            Assert.IsTrue(command.Contains(@"C:\Tools\gemini.cmd"));
            Assert.IsTrue(command.Contains("a^&b^|c^%PATH^%"));
            Assert.IsFalse(command.Contains(" a&b|c%PATH%"));
        }

        [Test]
        public void CreateProcessStartInfoPassesMetacharactersAsLiteralArgv()
        {
            if (!File.Exists("/bin/sh")) Assert.Ignore("Requires a Unix shell for the argv smoke test.");

            string root = Path.Combine(Path.GetTempPath(), "NexusCliInstallerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string script = Path.Combine(root, "argv.sh");
            string output = Path.Combine(root, "argv.txt");

            try
            {
                File.WriteAllText(script, "#!/bin/sh\nout=\"$1\"\nshift\nprintf '%s\\n' \"$@\" > \"$out\"\n");
                var method = typeof(MCPCliInstaller).GetMethod(
                    "CreateProcessStartInfo",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string[]) },
                    null);
                string[] args = { script, output, "TOKEN=a;$(touch bad)", "/tmp/py thon", "/tmp/proj\";echo bad" };
                var psi = (ProcessStartInfo)method.Invoke(null, new object[] { "/bin/sh", args });

                using (Process process = Process.Start(psi))
                {
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    Assert.AreEqual(0, process.ExitCode, error);
                }

                CollectionAssert.AreEqual(new[] { "TOKEN=a;$(touch bad)", "/tmp/py thon", "/tmp/proj\";echo bad" }, File.ReadAllLines(output));
                Assert.IsFalse(File.Exists(Path.Combine(root, "bad")));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
