using UnityEngine;

namespace Saltmarsh.Tides
{
    public class TideClock : MonoBehaviour
    {
        public float low = -1.2f;
        public float high = 2.4f;
        public TideTable table;

        public float NormalizedHeight(float h) => TideMath.InverseLerpClamped(low, high, h);
    }
}
