#if UNITY_EDITOR_OSX
using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Disables macOS App Nap for the Unity process while the MCP server is active.
    /// Uses P/Invoke to the Objective-C runtime to call
    /// [NSProcessInfo.processInfo beginActivityWithOptions:reason:].
    /// Also handles foregrounding Unity for autonomous compilation.
    /// </summary>
    public static class AppNapBypass
    {
        private static IntPtr _activity = IntPtr.Zero;
        private const string SessionKey = "NexusMCP_AppNapActivity";
        private const string PrevAppKey = "NexusMCP_PreviousApp";
        private const string PathKey = "NexusMCP_ApplicationPath";
        private static string _cachedApplicationPath;

        private static bool _isMainThread => MCPServer.MainThreadId == -1 || System.Threading.Thread.CurrentThread.ManagedThreadId == MCPServer.MainThreadId;

        /// <summary>Restores the activity pointer from a previous domain.</summary>
        static AppNapBypass()
        {
            if (_isMainThread) {
                try {
                    string stored = SessionState.GetString(SessionKey, "0");
                    if (long.TryParse(stored, out long ptr) && ptr != 0)
                        _activity = new IntPtr(ptr);
                    
                    _cachedApplicationPath = SessionState.GetString(PathKey, "");
                } catch { }
            }
        }

        // Objective-C runtime
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
        private static extern IntPtr objc_getClass(string className);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
        private static extern IntPtr sel_registerName(string selectorName);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_RetPtr(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_Activity(
            IntPtr receiver, IntPtr selector, ulong options, IntPtr reason);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_End(
            IntPtr receiver, IntPtr selector, IntPtr activity);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_NSString(
            IntPtr receiver, IntPtr selector, string utf8String);

        // CoreFoundation retain/release
        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFRetain(IntPtr cf);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease(IntPtr cf);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFRunLoopGetMain();

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRunLoopWakeUp(IntPtr rl);

        // NSActivityUserInitiated | NSActivityIdleSystemSleepDisabled
        private const ulong ActivityOptions = 0x00FFFFFF | (1UL << 20);

        public static void CacheApplicationPath()
        {
            if (!_isMainThread) return;

            string path = EditorApplication.applicationPath;
            if (string.IsNullOrEmpty(path)) return;

            int appIdx = path.IndexOf(".app");
            if (appIdx != -1)
            {
                _cachedApplicationPath = path.Substring(0, appIdx + 4);
                SessionState.SetString(PathKey, _cachedApplicationPath);
            }
        }

        public static void WakeMainLoop()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try { CFRunLoopWakeUp(CFRunLoopGetMain()); } catch { }
        }

        public static void Enable()
        {
            if (_activity != IntPtr.Zero) return;

            try
            {
                CacheApplicationPath();
                IntPtr nsString = CreateNSString("Nexus MCP Server Active");
                IntPtr processInfo = GetProcessInfo();
                if (processInfo == IntPtr.Zero || nsString == IntPtr.Zero) return;

                IntPtr beginSel = sel_registerName("beginActivityWithOptions:reason:");
                IntPtr activity = objc_msgSend_Activity(processInfo, beginSel, ActivityOptions, nsString);

                if (activity != IntPtr.Zero)
                {
                    _activity = CFRetain(activity);
                    if (_isMainThread) SessionState.SetString(SessionKey, _activity.ToInt64().ToString());
                    Debug.Log("[MCP] App Nap bypass ENABLED.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MCP] AppNapBypass.Enable failed: {e.Message}");
            }
        }

        public static void Disable()
        {
            if (_activity == IntPtr.Zero) return;

            try
            {
                IntPtr processInfo = GetProcessInfo();
                if (processInfo == IntPtr.Zero) return;

                IntPtr endSel = sel_registerName("endActivity:");
                objc_msgSend_End(processInfo, endSel, _activity);

                CFRelease(_activity);
                _activity = IntPtr.Zero;
                if (_isMainThread) SessionState.SetString(SessionKey, "0");
                Debug.Log("[MCP] App Nap bypass DISABLED.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MCP] AppNapBypass.Disable failed: {e.Message}");
                _activity = IntPtr.Zero;
                if (_isMainThread) SessionState.SetString(SessionKey, "0");
            }
        }

        public static void ScheduleActivation()
        {
            // Ephemeral background thread mandatory for bypassing Main Thread lockups
            var t = new System.Threading.Thread(() =>
            {
                try {
                    System.Threading.Thread.Sleep(500);
                    ActivateEditor();
                } catch (Exception e) {
                    Debug.LogWarning($"[MCP] Focus-steal background thread error: {e.Message}");
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private static void ActivateEditor()
        {
            try
            {
                // Capture current frontmost app
                var getProc = new System.Diagnostics.Process();
                getProc.StartInfo.FileName = "osascript";
                getProc.StartInfo.Arguments = "-e 'tell app \"System Events\" to get name of first process whose frontmost is true'";
                getProc.StartInfo.UseShellExecute = false;
                getProc.StartInfo.RedirectStandardOutput = true;
                getProc.StartInfo.CreateNoWindow = true;
                getProc.Start();
                string frontApp = getProc.StandardOutput.ReadToEnd().Trim();
                getProc.WaitForExit(2000);

                if (!string.IsNullOrEmpty(frontApp) && frontApp != "Unity")
                {
                    MCPServer.Enqueue(() => {
                        SessionState.SetString(PrevAppKey, frontApp);
                    });
                }

                // Force foregrounding via LaunchServices
                var actProc = new System.Diagnostics.Process();
                actProc.StartInfo.FileName = "open";
                actProc.StartInfo.Arguments = $"-a \"{_cachedApplicationPath}\"";
                actProc.StartInfo.UseShellExecute = false;
                actProc.StartInfo.CreateNoWindow = true;
                actProc.Start();
                
                Debug.Log($"[MCP] Focus bypass: Sent 'open -a' for {_cachedApplicationPath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MCP] ActivateEditor failed: {e.Message}");
            }
        }

        public static void ReturnToPreviousApp()
        {
            try
            {
                string prevApp = "";
                if (_isMainThread) prevApp = SessionState.GetString(PrevAppKey, "");
                if (string.IsNullOrEmpty(prevApp)) return;

                if (_isMainThread) SessionState.SetString(PrevAppKey, "");

                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = "open";
                proc.StartInfo.Arguments = $"-a \"{prevApp}\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
            }
            catch { }
        }

        private static IntPtr GetProcessInfo()
        {
            IntPtr cls = objc_getClass("NSProcessInfo");
            IntPtr sel = sel_registerName("processInfo");
            return objc_msgSend_RetPtr(cls, sel);
        }

        private static IntPtr CreateNSString(string text)
        {
            IntPtr cls = objc_getClass("NSString");
            IntPtr sel = sel_registerName("stringWithUTF8String:");
            return objc_msgSend_NSString(cls, sel, text);
        }
    }
}
#endif