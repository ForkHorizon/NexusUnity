using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityMCP.Editor.Capture;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Writes one capture image to disk under the Unity project Temp directory. It does not modify assets.
    /// </summary>
    internal static class CaptureFileDelivery
    {
        /// <summary>Writes one jpg or png file under Temp/NexusCapture and returns the absolute path.</summary>
        internal static string Write(byte[] bytes, CaptureFormat format)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new CaptureException(CaptureErrorCode.CaptureSourceEmpty, "Capture produced no image bytes.");
            }

            string extension = format == CaptureFormat.Png ? ".png" : ".jpg";
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string directory = Path.Combine(projectRoot, "Temp", "NexusCapture");
            Directory.CreateDirectory(directory);
            string name = "nexus-look-"
                + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)
                + "-"
                + Guid.NewGuid().ToString("N").Substring(0, 8)
                + extension;
            string path = Path.Combine(directory, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }
    }
}
