using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace UnityMCP.Editor.Capture
{
    /// <summary>
    /// Acceptance harness: submit Capture V2, prove the GPU request is still pending, then request an Editor domain reload.
    /// Writes a JSONL log under Library so the record survives domain reload.
    /// </summary>
    public static class NexusCaptureReloadProbe
    {
        /// <summary>Project-relative JSONL path for pending-reload cycles.</summary>
        public const string LogPath = "Library/NexusPendingReload.jsonl";

        /// <summary>
        /// Starts a Game View capture, records whether the GPU request is still pending, then
        /// schedules a domain reload on the next Editor delayCall. Does not wait for GPU.
        /// </summary>
        public static JObject SubmitPendingThenReload()
        {
            if (DriverOwnedReadback.IsReloading)
            {
                return Write(new JObject
                {
                    ["submitted"] = false,
                    ["done_before_reload"] = true,
                    ["error"] = "already_reloading"
                });
            }

            Task<CaptureResult> started = CaptureGateway.Shared.CaptureGameView(
                new CaptureRequest { Format = CaptureFormat.Jpeg, JpegQuality = 85, IncludeTelemetry = true },
                CancellationToken.None);

            bool submitted;
            bool done;
            DriverOwnedReadback.TryDescribeInFlight(out submitted, out done);
            var record = new JObject
            {
                ["utc"] = DateTime.UtcNow.ToString("o"),
                ["submitted"] = submitted,
                ["done_before_reload"] = done,
                ["pending_count"] = DriverOwnedReadback.PendingCount,
                ["task_completed"] = started.IsCompleted,
                ["reload_scheduled"] = submitted && !done
            };

            Write(record);
            if (submitted && !done)
            {
                EditorApplication.delayCall += RequestReloadOnce;
            }

            return record;
        }

        private static void RequestReloadOnce()
        {
            EditorUtility.RequestScriptReload();
        }

        private static JObject Write(JObject record)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), LogPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(path, record.ToString(Newtonsoft.Json.Formatting.None) + "\n");
            return record;
        }
    }
}
