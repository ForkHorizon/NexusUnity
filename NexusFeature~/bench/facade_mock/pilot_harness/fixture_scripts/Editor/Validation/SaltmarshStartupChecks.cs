using UnityEditor;
using UnityEngine;

namespace Saltmarsh.EditorTools
{
    // Lightweight project sanity checks that run after every script reload.
    [InitializeOnLoad]
    public static class SaltmarshStartupChecks
    {
        static readonly string[] CargoTypes =
        {
            "crate", "crate_small", "barrel", "fish_box", "net", "rope", "sack", "pallet", "lobster", "oyster",
            "salt", "timber", "coal", "lamp_oil", "tar", "canvas", "spice", "tea", "wool", "cider",
            "ice", "mail", "cannonball", "anchor_chain", "sailcloth", "copper", "tin", "bricks", "nails", "flour",
            "eel", "herring", "mackerel", "cod", "crab", "kelp", "pearls", "ink", "glass", "soap",
        };

        static SaltmarshStartupChecks()
        {
            EditorApplication.delayCall += RunAll;
        }

        static void RunAll()
        {
            foreach (var c in CargoTypes) Debug.Log("[Startup] Registered cargo type " + c);
            AudioBankCheck.Run();
            for (int i = 0; i < 120; i++)
                Debug.LogWarning("[Tides] Legacy tide curve format detected in 'Tides_Default'; re-save the asset to upgrade");
            Debug.LogWarning("[Net] Socket error budget at 0/5 (ok)");
            ManifestValidator.Run();
            LocalizationCheck.Run();
            AmbienceWarmup.Run();
        }
    }
}
