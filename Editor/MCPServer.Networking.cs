using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    // Server lifecycle, HttpListener setup, accept loop, and shutdown cleanup.
    // Probing existing instances and port discovery live in MCPServer.Discovery.cs.
    public static partial class MCPServer
    {
        private static void BindAndStartListener()
        {
            try
            {
                if (_cts == null || _cts.IsCancellationRequested) return;

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
                _state = ServerState.Running;
                _ = Task.Run(() => ServerLoop(_cts.Token));
                NexusEditorLog.Log(NexusLogCategory.Server, $"[MCP] Server started on port {_port}", true);
            }
            catch (Exception e)
            {
                Cleanup();
                _state = ServerState.Error;
                string owner = GetPortOwner(_port);
                LastError = $"{e.Message} (Port {_port} owner: {owner})";
                NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] Server failed to start: {LastError}");
            }
        }

        private static async Task ServerLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest) _ = Task.Run(() => ProcessWebSocket(context));
                    else _ = Task.Run(() => HandleHttpRequest(context));
                }
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                    NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] Fatal server loop error: {e.Message}");
            }
            finally
            {
                if (_state == ServerState.Running) _state = ServerState.Stopped;
            }
        }

        /// <summary>
        /// Starts the loopback-only Nexus Unity HTTP/WebSocket server and persists restart intent in Unity <see cref="EditorPrefs"/>.
        /// </summary>
        /// <remarks>
        /// Startup must run from the Unity Editor main thread; background calls are marshaled through <see cref="EditorApplication.delayCall"/>.
        /// The method initializes tool dispatch, resolves or allocates the configured port, records restart intent, may attach to an
        /// existing Nexus Unity instance on the same port, and enables the macOS App Nap bypass before binding the listener.
        /// </remarks>
        public static void Start()
        {
            if (_mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                EditorApplication.delayCall += Start;
                return;
            }

            lock (_startLock)
            {
                if (_state == ServerState.Running || _state == ServerState.Starting) return;
                _state = ServerState.Starting;
                LastError = null;
                _cts = new CancellationTokenSource();
            }

            MCPServerMethods.Init();
            EnsureAuthToken();
            if (EditorApplication.isPlaying) Application.runInBackground = true;

            ResolvePort();

            EditorPrefs.SetBool(StablePrefsKey, true);
            NexusEditorLog.Log(NexusLogCategory.Server, $"[MCP] Attempting to start server on port {_port}...", true);

            var token = _cts.Token;
            Task.Run(() => StartListenerAsync(token));
        }

        // Runs off the main thread: attaches to an existing instance if one owns
        // the port, otherwise binds our own listener.
        private static async Task StartListenerAsync(CancellationToken token)
        {
            try
            {
                if (IsPortBusy(_port) && !await TryClaimBusyPort()) return;

                if (token.IsCancellationRequested) return;
                #if UNITY_EDITOR_OSX
                AppNapBypass.Enable();
                #endif
                BindAndStartListener();
            }
            catch (Exception e)
            {
                _state = ServerState.Error;
                LastError = e.Message;
                NexusEditorLog.Error(NexusLogCategory.Server, $"[MCP] Server start error: {e.Message}");
            }
        }

        /// <summary>
        /// Stops the Nexus Unity server on the editor main thread, clears restart intent, disables macOS App Nap bypass, and closes listeners.
        /// </summary>
        /// <remarks>
        /// Calls from background threads are marshaled through <see cref="Enqueue"/> before mutating Unity editor state.
        /// Cleanup cancels pending server work and closes the HTTP listener/WebSocket state used by the local automation server.
        /// </remarks>
        public static void Stop()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                MCPServer.Enqueue(Stop);
                return;
            }
            EditorPrefs.SetBool(StablePrefsKey, false);
            #if UNITY_EDITOR_OSX
            AppNapBypass.Disable();
            #endif
            Cleanup();
        }

        internal static void Cleanup()
        {
            lock (_startLock)
            {
                _cts?.Cancel();
                lock (_webSocketLock)
                {
                    foreach (var ws in _activeWebSockets)
                    {
                        try { ws?.Dispose(); } catch { }
                    }
                    _activeWebSockets.Clear();
                }
                if (_listener != null)
                {
                    try { if (_listener.IsListening) _listener.Stop(); } catch { }
                    try { _listener.Close(); } catch { }
                    _listener = null;
                }
                _state = ServerState.Stopped;
            }
        }
    }
}
