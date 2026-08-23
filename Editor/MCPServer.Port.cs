using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServer
    {
        private static bool IsPortBusy(int port)
        {
            try
            {
                var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
                if (listeners != null && listeners.Any(listener => listener.Port == port)) return true;
            }
            catch { }

            try
            {
                using (var tcp = new System.Net.Sockets.TcpClient())
                {
                    var result = tcp.BeginConnect("127.0.0.1", port, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200))) return false;
                    tcp.EndConnect(result);
                    return true;
                }
            }
            catch { return false; }
        }

        private static string GetPortOwner(int port)
        {
            try
            {
                if (IsUnixEditor()) return GetUnixPortOwner(port);
                if (Application.platform == RuntimePlatform.WindowsEditor) return GetWindowsPortOwner(port);
            }
            catch { }
            return "Unknown Process";
        }

        private static bool IsUnixEditor()
        {
            return Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.LinuxEditor;
        }

        private static string GetUnixPortOwner(int port)
        {
            string arguments = $"-c \"lsof -i TCP:{port} -s TCP:LISTEN -P -n || /usr/sbin/lsof -i TCP:{port} -s TCP:LISTEN -P -n\"";
            var lines = SplitCommandOutput(RunPortCommand("/bin/bash", arguments));
            if (lines.Length <= 1) return "Unknown Process";

            var parts = lines[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? $"{parts[0]} (PID: {parts[1]})" : "Unknown Process";
        }

        private static string GetWindowsPortOwner(int port)
        {
            string output = RunPortCommand("cmd.exe", $"/c netstat -a -n -o | findstr :{port}");
            foreach (var line in SplitCommandOutput(output))
            {
                var owner = ParseWindowsPortOwner(line);
                if (owner != null) return owner;
            }
            return "Unknown Process";
        }

        private static string ParseWindowsPortOwner(string line)
        {
            if (!line.Contains("LISTENING")) return null;
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            string pid = parts[parts.Length - 1];
            try
            {
                using (var process = Process.GetProcessById(int.Parse(pid)))
                    return $"{process.ProcessName} (PID: {pid})";
            }
            catch { return $"PID: {pid}"; }
        }

        private static string RunPortCommand(string fileName, string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
        }

        private static string[] SplitCommandOutput(string output)
        {
            return output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--mcp-port" && int.TryParse(args[i + 1], out int port))
                    _cliPortOverride = port;
            }
        }
    }
}
