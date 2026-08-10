using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class BuildCompleteMainMenuScene
    {
        [MenuItem("Game Utility/Build Complete MainMenu Scene (Dựng Hoàn Chỉnh Scene MainMenu 100%)")]
        public static void BuildMainMenuScene()
        {
            string mainMenuPath = "Assets/Game/Scenes/MainMenu.unity";
            string loginPath = "Assets/Game/Scenes/Login.unity";

            // 1. Tạo mới hoặc mở Scene MainMenu.unity
            Scene mainMenuScene;
            if (System.IO.File.Exists(mainMenuPath))
            {
                mainMenuScene = EditorSceneManager.OpenScene(mainMenuPath);
            }
            else
            {
                mainMenuScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }

            // 2. Dựng 3D Background, Muck UI (MainMenuPanel) & Intro Story UI
            DemoBackgroundInstaller.InstallBackground();
            MuckStyleUIBuilder.BuildMuckUI();
            MidnightChaos.Editor.IntroStoryUIBuilder.BuildIntroStoryUI();

            // 3. Đảm bảo MainMenuPanel hiển thị mượt mà 100% làm giao diện chính
            GameObject menuPanel = GameObject.Find("MainMenuPanel");
            if (menuPanel != null)
            {
                menuPanel.SetActive(true);
            }

            // Xóa bớt LoginPanel thừa nếu có trong MainMenu scene
            GameObject loginPanel = GameObject.Find("LoginPanel");
            if (loginPanel != null) Object.DestroyImmediate(loginPanel);

            GameObject authGroup = GameObject.Find("AuthGroupPanel");
            if (authGroup != null) Object.DestroyImmediate(authGroup);

            // 4. Lưu Scene MainMenu.unity
            EditorSceneManager.SaveScene(mainMenuScene, mainMenuPath);
            Debug.Log($"[BuildCompleteMainMenuScene] Đã dựng thành công 100% Scene MainMenu tại {mainMenuPath}");

            // 5. Đăng ký Build Settings
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
            string combatPath = "Assets/MidnightChaos/Generated/Scenes/ProceduralCombatDemo.unity";
            string mapPath = "Assets/Game/Scenes/Map.unity";

            if (System.IO.File.Exists(loginPath)) buildScenes.Add(new EditorBuildSettingsScene(loginPath, true));
            if (System.IO.File.Exists(mainMenuPath)) buildScenes.Add(new EditorBuildSettingsScene(mainMenuPath, true));
            if (System.IO.File.Exists(mapPath)) buildScenes.Add(new EditorBuildSettingsScene(mapPath, true));
            if (System.IO.File.Exists(combatPath)) buildScenes.Add(new EditorBuildSettingsScene(combatPath, true));

            EditorBuildSettings.scenes = buildScenes.ToArray();

            // 6. Mở lại Scene Login để cấu hình tự động chuyển sang MainMenu
            if (System.IO.File.Exists(loginPath))
            {
                EditorSceneManager.OpenScene(loginPath);
                AuthUIManager authUI = Object.FindFirstObjectByType<AuthUIManager>();
                if (authUI != null)
                {
                    SerializedObject authSO = new SerializedObject(authUI);
                    authSO.FindProperty("loadSceneOnLogin").boolValue = true;
                    authSO.FindProperty("mainMenuSceneName").stringValue = "MainMenu";
                    authSO.ApplyModifiedProperties();
                }

                GameObject oldMenu = GameObject.Find("MainMenuPanel");
                if (oldMenu != null) oldMenu.SetActive(false);

                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }

            // Mở lại Scene MainMenu để hiển thị trực quan cho người dùng
            EditorSceneManager.OpenScene(mainMenuPath);

            EditorUtility.DisplayDialog("Hoàn tất 100%!", "Đã dựng xong 100% Bảng Main Menu Panel + 3D Background trên Scene MainMenu.unity!\n\n✓ Scene MainMenu hiện đang mở sẵn trên màn hình của bạn.\n✓ Đã kết nối nút CHƠI, CÀI ĐẶT, ĐĂNG XUẤT và THOÁT.\n✓ Khi đăng nhập từ Login, game sẽ tự động chuyển sang Scene MainMenu này!", "OK");
        }
    }
}
