using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor-side helper copied into the scratch project only.
/// Batchmode has no save dialog, so this discards the open scene and reloads Lab.
/// </summary>
public static class LabFixtureReset
{
    public static void Reload()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var scenePath = "Assets/LabFixture/Scenes/Lab.unity";
        if (File.Exists(scenePath))
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        AssetDatabase.Refresh();
    }
}
