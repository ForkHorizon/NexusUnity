using System.Threading;
using System.Threading.Tasks;

namespace UnityMCP.Editor.Capture
{
    /// <summary>
    /// Transport-independent Game View capture entry point.
    /// </summary>
    public interface ICaptureGateway
    {
        /// <summary>
        /// Captures presented Game View pixels, including Screen Space Overlay UI when Unity composites it into Game View.
        /// </summary>
        /// <param name="request">Semantic capture options. Must not include transport envelopes.</param>
        /// <param name="cancellationToken">Token used to fail the logical request without destroying an in-flight GPU buffer.</param>
        /// <returns>Compressed image bytes and capture metadata.</returns>
        Task<CaptureResult> CaptureGameView(CaptureRequest request, CancellationToken cancellationToken);
    }
}
