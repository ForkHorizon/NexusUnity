using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServer
    {
        private static bool IsPortBusy(int port)
        {
            try {
                var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpListeners = ipProperties.GetActiveTcpListeners();
                if (tcpListeners != null && tcpListeners.Any(l => l.Port == port)) return true;
            } catch { }

            try
            {
                using (var tcp = new System.Net.Sockets.TcpClient()) {
                    var result = tcp.BeginConnect("127.0.0.1", port, null, null);
                    if (result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200))) {
                        tcp.EndConnect(result);
                        return true;
                    }
                }
            } catch { }
            return false;
        }


        private static string GetPortOwner(int port)
        {
            try
            {
                var p = new System.Diagnostics.Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;

                if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.LinuxEditor)
                {
                    p.StartInfo.FileName = "/bin/bash";
                    p.StartInfo.Arguments = $"-c \"lsof -i TCP:{port} -s TCP:LISTEN -P -n || /usr/sbin/lsof -i TCP:{port} -s TCP:LISTEN -P -n\"";
                    p.Start();
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();

                    string[] lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 1)
                    {
                        string[] parts = lines[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2) return $"{parts[0]} (PID: {parts[1]})";
                    }
                }
                else if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    p.StartInfo.FileName = "cmd.exe";
                    p.StartInfo.Arguments = $"/c netstat -a -n -o | findstr :{port}";
                    p.Start();
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();

                    string[] lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("LISTENING"))
                        {
                            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0)
                            {
                                string pid = parts[parts.Length - 1];
                                try
                                {
                                    var proc = System.Diagnostics.Process.GetProcessById(int.Parse(pid));
                                    return $"{proc.ProcessName} (PID: {pid})";
                                }
                                catch { return $"PID: {pid}"; }
                            }
                        }
                    }
                }
            }
            catch { }
            return "Unknown Process";
        }


        private static void ParseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--mcp-port" && int.TryParse(args[i + 1], out int p)) _cliPortOverride = p;
        }
    }
}
