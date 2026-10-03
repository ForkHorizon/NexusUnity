using System.Collections.Generic;
using UnityEngine;

namespace Saltmarsh.Props
{
    [CreateAssetMenu(menuName = "Saltmarsh/Prop Set")]
    public class PropSet : ScriptableObject
    {
        public List<GameObject> prefabs = new List<GameObject>();
        public float density = 1f;
    }
}
