using UnityEngine;

namespace Saltmarsh.Tides
{
    [CreateAssetMenu(menuName = "Saltmarsh/Tide Table")]
    public class TideTable : ScriptableObject
    {
        public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
        public float cyclesPerDay = 2f;
    }
}
