using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class OpenMainMenuSceneTool
    {
        [MenuItem("Game Utility/Open Login Scene (Mở Scene Login)")]
        public static void OpenLoginScene()
        {
            string path = "Assets/Game/Scenes/Login.unity";
            if (System.IO.File.Exists(path))
            {
                EditorSceneManager.OpenScene(path);
                Debug.Log("[SceneManager] Đã mở Scene Login!");
            }
        }

        [MenuItem("Game Utility/Open Main Menu Scene (Mở Scene MainMenu)")]
        public static void OpenMainMenuScene()
        {
            string path = "Assets/Game/Scenes/MainMenu.unity";
            if (System.IO.File.Exists(path))
            {
                EditorSceneManager.OpenScene(path);
                Debug.Log("[SceneManager] Đã mở Scene MainMenu!");
            }
            else
            {
                BuildSeparateMainMenuSceneTool.CreateSeparateMainMenuScene();
            }
        }
    }
}
