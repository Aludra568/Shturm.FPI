using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace RicochetBerlin.EditorTools
{
    /// <summary>
    /// A fresh clone has no Library, so Unity would open an empty untitled scene and Play would show nothing.
    /// Open the game scene when the project loads into an empty scene, and make Play always start the game.
    /// </summary>
    [InitializeOnLoad]
    static class OpenGameScene
    {
        const string GameScene = "Assets/_Game/Scenes/Game.unity";

        static OpenGameScene()
        {
            EditorApplication.delayCall += () =>
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene);
                if (scene == null) return;
                EditorSceneManager.playModeStartScene = scene;

                Scene active = SceneManager.GetActiveScene();
                if (!EditorApplication.isPlayingOrWillChangePlaymode && string.IsNullOrEmpty(active.path) && !active.isDirty)
                    EditorSceneManager.OpenScene(GameScene);
            };
        }
    }
}
