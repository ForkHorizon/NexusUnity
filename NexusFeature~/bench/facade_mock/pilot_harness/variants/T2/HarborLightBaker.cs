using UnityEditor;
using UnityEngine;
using Saltmarsh.Weather;

namespace Saltmarsh.EditorTools
{
    // Computes the per-district light budget from the active weather profile.
    public static class HarborLightBaker
    {
        const string ProfilePath = "Assets/BenchFixture/Data/Weather/Harbor.asset";

        [MenuItem("Tools/Saltmarsh/Lighting/Bake Harbor Light Budget")]
        public static void Bake()
        {
            var profile = AssetDatabase.LoadAssetAtPath<WeatherProfile>(ProfilePath);
            float budget = profile != null ? profile.lightBudget : 1f;
            Debug.Log($"[Lighting] Harbor light budget {budget:0.00}");
        }
    }
}
