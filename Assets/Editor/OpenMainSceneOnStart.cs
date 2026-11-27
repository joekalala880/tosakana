using UnityEditor;
using UnityEditor.SceneManagement;

// Opens Assets/Scenes/Main.unity when the editor is showing an empty "Untitled" scene,
// so the project always starts on the game.
[InitializeOnLoad]
public static class OpenMainSceneOnStart
{
	static OpenMainSceneOnStart()
	{
		EditorApplication.delayCall += () =>
		{
			var scene = EditorSceneManager.GetActiveScene();
			if (string.IsNullOrEmpty(scene.path) && !scene.isDirty && !EditorApplication.isPlayingOrWillChangePlaymode)
				EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
		};
	}
}
