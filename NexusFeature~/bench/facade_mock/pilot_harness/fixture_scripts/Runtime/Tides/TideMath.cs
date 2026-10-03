using UnityEngine;

namespace Saltmarsh.Tides
{
    public static class TideMath
    {
        // Maps v from [a, b] to [0, 1], clamped.
        public static float InverseLerpClamped(float a, float b, float v)
        {
            if (Mathf.Approximately(a, b)) return 0f;
            return Mathf.Clamp01((v - a) / (b - a));
        }

        public static float PeriodHours(float cyclesPerDay) => cyclesPerDay <= 0f ? 0f : 24f / cyclesPerDay;

        public static float Wrap01(float t) => t - Mathf.Floor(t);
    }
}
