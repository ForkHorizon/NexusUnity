using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saltmarsh.Data
{
    [Serializable]
    public class LocEntry
    {
        public string key;
        public string text;
    }

    [CreateAssetMenu(menuName = "Saltmarsh/Localization Table")]
    public class LocalizationTable : ScriptableObject
    {
        public string tableName = "UI_en";
        public List<LocEntry> entries = new List<LocEntry>();

        public bool Has(string key) => entries.Exists(e => e.key == key);
    }
}
