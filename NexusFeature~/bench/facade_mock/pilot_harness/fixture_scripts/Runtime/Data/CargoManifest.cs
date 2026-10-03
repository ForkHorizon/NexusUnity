using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saltmarsh.Data
{
    [Serializable]
    public class CargoEntry
    {
        public string id;
        public int quantity;
    }

    [CreateAssetMenu(menuName = "Saltmarsh/Cargo Manifest")]
    public class CargoManifest : ScriptableObject
    {
        public string pier;
        public List<CargoEntry> entries = new List<CargoEntry>();
    }
}
