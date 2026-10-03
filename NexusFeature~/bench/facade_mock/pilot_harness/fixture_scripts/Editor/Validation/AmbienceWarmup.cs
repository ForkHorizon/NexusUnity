using System;
using UnityEditor;
using UnityEngine;
using Saltmarsh.Audio;

namespace Saltmarsh.EditorTools
{
    // Pre-builds the default ambience mix so the first Play mode entry doesn't hitch.
    public static class AmbienceWarmup
    {
        const string ProfilePath = "Assets/BenchFixture/Data/Ambience/AmbienceProfile_Default.asset";

        public static void Run()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AmbienceProfile>(ProfilePath);
            if (profile == null) return;
            try
            {
                HarborAmbience.BuildMix(profile);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
