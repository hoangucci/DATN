using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class BuildSeparateMainMenuSceneTool
    {
        [MenuItem("Game Utility/Separate Main Menu Scene (Tạo Scene MainMenu Độc Lập)")]
        public static void CreateSeparateMainMenuScene()
        {
            string mainMenuPath = "Assets/Game/Scenes/MainMenu.unity";
            string loginPath = "Assets/Game/Scenes/Login.unity";

            // 1. Tạo mới Scene MainMenu.unity
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2. Cài đặt 3D Background, Main Menu UI, và Intro Story UI
            DemoBackgroundInstaller.InstallBackground();
            MuckStyleUIBuilder.BuildMuckUI();
            MidnightChaos.Editor.IntroStoryUIBuilder.BuildIntroStoryUI();

            // Ẩn bớt LoginPanel nếu có thừa trong MainMenu scene
            GameObject loginPanel = GameObject.Find("LoginPanel");
            if (loginPanel != null) loginPanel.SetActive(false);
            GameObject authGroup = GameObject.Find("AuthGroupPanel");
            if (authGroup != null) authGroup.SetActive(false);

            // Bật MainMenuPanel làm giao diện chính
            GameObject menuPanel = GameObject.Find("MainMenuPanel");
            if (menuPanel != null) menuPanel.SetActive(true);

            // 3. Lưu Scene MainMenu.unity
            EditorSceneManager.SaveScene(newScene, mainMenuPath);
            Debug.Log($"[BuildSeparateMainMenuSceneTool] Đã tạo thành công Scene MainMenu tại {mainMenuPath}");

            // 4. Cập nhật Build Settings
            RegisterBuildSettings();

            // 5. Cập nhật Login.unity để sau khi đăng nhập thành công sẽ chuyển sang MainMenu.unity
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

                // Trong Scene Login, ẩn bớt MainMenuPanel và bật LoginPanel
                GameObject mainLoginPanel = GameObject.Find("LoginPanel");
                if (mainLoginPanel != null) mainLoginPanel.SetActive(true);

                GameObject mainAuthGroup = GameObject.Find("AuthGroupPanel");
                if (mainAuthGroup != null) mainAuthGroup.SetActive(true);

                GameObject mainOverlayMenu = GameObject.Find("MainMenuPanel");
                if (mainOverlayMenu != null) Object.DestroyImmediate(mainOverlayMenu);

                GameObject introStoryObj = GameObject.Find("[IntroStoryPanel]");
                if (introStoryObj != null) Object.DestroyImmediate(introStoryObj);

                GameObject lobbyObj = GameObject.Find("[Muck_LobbyManager]");
                if (lobbyObj != null) Object.DestroyImmediate(lobbyObj);

                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }

            EditorUtility.DisplayDialog("Hoàn tất 100%!", "Đã tách thành công Main Menu sang Scene độc lập (MainMenu.unity)!\n\n1. Scene Login: Chứa bảng Đăng Nhập / Đăng Ký.\n2. Scene MainMenu: Chứa Menu chính, Phòng chờ Lobby & Cài Đặt 3D.\n3. Khi đăng nhập thành công: Tự động chuyển từ Login sang MainMenu!", "OK");
        }

        public static void RegisterBuildSettings()
        {
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();

            string loginPath = "Assets/Game/Scenes/Login.unity";
            string mainMenuPath = "Assets/Game/Scenes/MainMenu.unity";
            string mapPath = "Assets/Game/Scenes/Map.unity";
            string combatPath = "Assets/MidnightChaos/Generated/Scenes/ProceduralCombatDemo.unity";

            if (System.IO.File.Exists(loginPath)) buildScenes.Add(new EditorBuildSettingsScene(loginPath, true));
            if (System.IO.File.Exists(mainMenuPath)) buildScenes.Add(new EditorBuildSettingsScene(mainMenuPath, true));
            if (System.IO.File.Exists(mapPath)) buildScenes.Add(new EditorBuildSettingsScene(mapPath, true));
            if (System.IO.File.Exists(combatPath)) buildScenes.Add(new EditorBuildSettingsScene(combatPath, true));

            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log("[BuildSettings] Đã đăng ký đầy đủ các Scene: Login -> MainMenu -> Map -> ProceduralCombatDemo!");
        }
    }
}
