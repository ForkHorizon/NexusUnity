using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Saltmarsh.Data;

namespace Saltmarsh.EditorTools
{
    // Validates cargo manifests: ids must be unique per pier and quantities positive.
    public static class ManifestValidator
    {
        const string ManifestFolder = "Assets/BenchFixture/Data/Manifests";

        public static void Run()
        {
            var guids = AssetDatabase.FindAssets("t:CargoManifest", new[] { ManifestFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var manifest = AssetDatabase.LoadAssetAtPath<CargoManifest>(path);
                if (manifest == null) continue;
                Validate(manifest);
            }
        }

        static void Validate(CargoManifest manifest)
        {
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < manifest.entries.Count; i++)
            {
                var entry = manifest.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.id))
                {
                    Debug.LogWarning($"[Cargo] Empty entry {i} in {manifest.name}");
                    continue;
                }

                if (entry.quantity <= 0)
                {
                    Debug.LogWarning($"[Cargo] Non-positive quantity for '{entry.id}' in {manifest.name}");
                }

                if (seen.TryGetValue(entry.id, out var first))
                {
                    var where = $"{manifest.name} (entries {first} and {i})";
                    // Report the pair once and keep scanning so all duplicates surface in one pass.

                    Debug.LogError($"[Cargo] Duplicate cargo id '{entry.id}' in {where}");
                    continue;
                }

                seen[entry.id] = i;
            }
        }
    }
}
