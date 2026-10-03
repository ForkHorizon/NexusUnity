foreach (var d in new[]{"Art","Audio","Data","Materials","Prefabs","Scenes"}) UnityEditor.AssetDatabase.DeleteAsset("Assets/BenchFixture/"+d);
UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
UnityEditor.AssetDatabase.Refresh(); return "cleaned";
