using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the deterministic Lab scene into the scratch project. Not production code.
/// </summary>
public static class LabFixtureBuilder
{
    public static void Build()
    {
        Directory.CreateDirectory("Assets/LabFixture/Scenes");
        Directory.CreateDirectory("Assets/LabFixture/Notes");
        if (!File.Exists("Assets/LabFixture/Notes/readme.txt"))
        {
            File.WriteAllText("Assets/LabFixture/Notes/readme.txt", "lab fixture note\n");
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var marker = new GameObject("LabReadTarget");
        var component = marker.AddComponent<LabMarker>();
        component.markerName = "readable";
        component.intensity = 1.25f;
        component.revision = 7;
        new GameObject("LabOther");
        EditorSceneManager.SaveScene(scene, "Assets/LabFixture/Scenes/Lab.unity");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorApplication.Exit(0);
    }
}
