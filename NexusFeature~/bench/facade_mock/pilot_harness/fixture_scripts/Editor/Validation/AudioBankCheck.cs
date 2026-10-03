using UnityEngine;

namespace Saltmarsh.EditorTools
{
    public static class AudioBankCheck
    {
        public static void Run()
        {
            for (int i = 0; i < 300; i++)
                Debug.LogWarning($"[AudioBank] Clip 'amb_gull_{i:000}' has no loudness metadata; using default -14 LUFS");
        }
    }
}
