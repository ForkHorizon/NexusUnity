using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Projects canonical Nexus commands onto the Legacy HTTP/MCP dispatch tables.
    /// </summary>
    public static class NexusLegacyCommandProjection
    {
        /// <summary>
        /// Registers each canonical command under its id and aliases. HTTP and alias names share one handler instance.
        /// </summary>
        /// <param name="methods">Synchronous JSON-RPC table. Unused for the M2 async POC set.</param>
        /// <param name="asyncMethods">Asynchronous JSON-RPC table that owns the POC commands.</param>
        public static void Register(
            Dictionary<string, Func<JToken, JToken>> methods,
            Dictionary<string, Func<JToken, Task<JToken>>> asyncMethods)
        {
            if (asyncMethods == null) throw new ArgumentNullException(nameof(asyncMethods));
            NexusCommandRegistry.EnsureCreated();
            foreach (INexusCommand command in NexusCommandRegistry.All)
            {
                INexusCommand shared = command;
                if (shared.Descriptor.DispatchAsync)
                {
                    Func<JToken, Task<JToken>> handler =
                        p => shared.ExecuteAsync(p, CancellationToken.None);
                    BindAsync(asyncMethods, shared.Descriptor.Id, handler);
                    if (shared.Descriptor.Aliases == null) continue;
                    foreach (string alias in shared.Descriptor.Aliases)
                        BindAsync(asyncMethods, alias, handler);
                    continue;
                }

                Func<JToken, JToken> syncHandler = p =>
                    shared.ExecuteAsync(p, CancellationToken.None).GetAwaiter().GetResult();
                BindSync(methods, shared.Descriptor.Id, syncHandler);
                if (shared.Descriptor.Aliases == null) continue;
                foreach (string alias in shared.Descriptor.Aliases)
                    BindSync(methods, alias, syncHandler);
            }
        }

        private static void BindAsync(
            Dictionary<string, Func<JToken, Task<JToken>>> asyncMethods,
            string name,
            Func<JToken, Task<JToken>> handler)
        {
            if (!string.IsNullOrEmpty(name)) asyncMethods[name] = handler;
        }

        private static void BindSync(
            Dictionary<string, Func<JToken, JToken>> methods,
            string name,
            Func<JToken, JToken> handler)
        {
            if (methods == null) throw new ArgumentNullException(nameof(methods));
            if (!string.IsNullOrEmpty(name)) methods[name] = handler;
        }
    }
}
