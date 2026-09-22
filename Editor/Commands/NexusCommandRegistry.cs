using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Holds the canonical Nexus command instances used by Legacy HTTP and Pipeline projections.
    /// </summary>
    public static class NexusCommandRegistry
    {
        private static readonly Dictionary<string, INexusCommand> ById =
            new Dictionary<string, INexusCommand>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<INexusCommand> Unique = new List<INexusCommand>();
        private static readonly object Sync = new object();
        private static bool _ready;

        /// <summary>
        /// Ensures the three M2 proof-of-concept commands are registered exactly once for this domain.
        /// </summary>
        public static void EnsureCreated()
        {
            if (_ready) return;
            lock (Sync)
            {
                if (_ready) return;
                Register(new ProjectMapCommand());
                Register(new GroupCompileErrorsCommand());
                Register(new CaptureGameViewCommand());
                _ready = true;
            }
        }

        /// <summary>Gets the unique canonical command instances.</summary>
        public static IReadOnlyList<INexusCommand> All
        {
            get
            {
                EnsureCreated();
                return Unique;
            }
        }

        /// <summary>
        /// Resolves a canonical id or transport alias to the shared handler instance.
        /// </summary>
        /// <param name="id">Canonical id or alias.</param>
        /// <returns>The shared command handler.</returns>
        public static INexusCommand Get(string id)
        {
            EnsureCreated();
            if (id != null && ById.TryGetValue(id, out var command)) return command;
            throw new KeyNotFoundException($"Unknown Nexus command '{id}'.");
        }

        /// <summary>
        /// Returns whether <paramref name="id"/> is a canonical command id or alias.
        /// </summary>
        public static bool Contains(string id)
        {
            EnsureCreated();
            return id != null && ById.ContainsKey(id);
        }

        private static void Register(INexusCommand command)
        {
            Unique.Add(command);
            ById[command.Descriptor.Id] = command;
            if (command.Descriptor.Aliases == null) return;
            foreach (string alias in command.Descriptor.Aliases)
            {
                if (!string.IsNullOrEmpty(alias)) ById[alias] = command;
            }
        }
    }
}
