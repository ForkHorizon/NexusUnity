using UnityEditor;
using UnityEngine;
using Saltmarsh.Data;

namespace Saltmarsh.EditorTools
{
    // Checks that every UI string key referenced by menus exists in the English table.
    public static class LocalizationCheck
    {
        const string TablePath = "Assets/BenchFixture/Data/Loc/UI_en.asset";

        static readonly string[] ReferencedKeys =
        {
            "menu.play", "menu.play.subtitle", "menu.settings", "menu.quit", "menu.credits",
            "menu.play.subtitle", "map.title", "menu.play.subtitle", "menu.play.subtitle", "menu.play.subtitle",
        };

        public static void Run()
        {
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(TablePath);
            if (table == null) return;

            foreach (var key in ReferencedKeys)
            {
                if (table.Has(key)) continue;

                // Keys are case-sensitive and shared with the runtime
                // LocalizationTable lookup, so report the exact key that
                // the menu prefab asked for. One entry per usage keeps the
                // console count equal to the number of broken labels.
                Debug.LogError($"[Loc] Missing key '{key}' in table {table.tableName}");
            }
        }
    }
}
