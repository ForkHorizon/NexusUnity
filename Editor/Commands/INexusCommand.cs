using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Canonical Nexus command handler. Legacy HTTP and Pipeline adapters invoke the same instance.
    /// </summary>
    public interface INexusCommand
    {
        /// <summary>Gets the canonical descriptor used by every transport projection.</summary>
        NexusCommandDescriptor Descriptor { get; }

        /// <summary>
        /// Executes the command against Unity editor state.
        /// </summary>
        /// <param name="parameters">Semantic arguments. Transport envelopes are not included.</param>
        /// <param name="cancellationToken">Token used to abort long-running work such as capture.</param>
        /// <returns>Structured command result ready for transport serialization.</returns>
        Task<JToken> ExecuteAsync(JToken parameters, CancellationToken cancellationToken);
    }
}
