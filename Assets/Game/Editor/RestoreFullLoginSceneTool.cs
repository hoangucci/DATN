using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class RestoreFullLoginSceneTool
    {
        [InitializeOnLoadMethod]
        private static void AutoRegisterScenesInBuildSettings()
        {
            System.Collections.Generic.List<EditorBuildSettingsScene> buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool updated = false;

            EditorBuildSettingsScene loginScene = buildScenes.Find(s => s.path.Contains("Login.unity"));
            if (loginScene == null)
            {
                buildScenes.Insert(0, new EditorBuildSettingsScene("Assets/Game/Scenes/Login.unity", true));
                updated = true;
            }
            else if (!loginScene.enabled)
            {
                loginScene.enabled = true;
                updated = true;
            }

            EditorBuildSettingsScene combatScene = buildScenes.Find(s => s.path.Contains("ProceduralCombatDemo.unity"));
            if (combatScene == null)
            {
                buildScenes.Add(new EditorBuildSettingsScene("Assets/MidnightChaos/Generated/Scenes/ProceduralCombatDemo.unity", true));
                updated = true;
            }
            else if (!combatScene.enabled)
            {
                combatScene.enabled = true;
                updated = true;
            }

            if (updated)
            {
                EditorBuildSettings.scenes = buildScenes.ToArray();
                Debug.Log("[AutoRegister] Đã tự động kích hoạt (Enable) Login.unity, Map.unity và ProceduralCombatDemo.unity trong Build Settings!");
            }
        }

        [MenuItem("Game Utility/Restore 100% Full Login Scene & UI (Khôi Phục Giao Diện Hoàn Chỉnh)")]
        public static void RestoreFullScene()
        {
            AutoRegisterScenesInBuildSettings();
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy Canvas trong Scene hiện tại! Vui lòng mở Scene Login.unity.", "OK");
                return;
            }

            Debug.Log("[RestoreTool] Bắt đầu tự động khôi phục toàn bộ giao diện Login Scene...");

            // 1. Khôi phục AuthUIManager & MainMenuManager references
            AuthUIManager authManager = Object.FindFirstObjectByType<AuthUIManager>();
            MainMenuManager mainManager = Object.FindFirstObjectByType<MainMenuManager>();

            // Tìm LogoGame / Fire
            GameObject logoGame = GameObject.Find("LogoGame") ?? GameObject.Find("logo game") ?? GameObject.Find("Logo");
            GameObject fireObj = GameObject.Find("Fire");

            if (authManager != null)
            {
                SerializedObject authSO = new SerializedObject(authManager);

                // Gán các panel chính
                SetPropertyIfNull(authSO, "loginPanel", GameObject.Find("LoginPanel"));
                SetPropertyIfNull(authSO, "registerPanel", GameObject.Find("RegisterPanel"));
                SetPropertyIfNull(authSO, "forgotPasswordPanel", GameObject.Find("ForgotPasswordPanel"));
                SetPropertyIfNull(authSO, "mainMenuPanel", GameObject.Find("MainMenuPanel"));
                SetPropertyIfNull(authSO, "gameLogoObject", logoGame);

                // Gán các field đăng nhập
                SetPropertyIfNull(authSO, "loginEmailInput", GameObject.Find("LoginEmailInput"));
                SetPropertyIfNull(authSO, "loginPasswordInput", GameObject.Find("LoginPasswordInput"));
                SetPropertyIfNull(authSO, "rememberMeToggle", GameObject.Find("RememberMeToggle"));
                SetPropertyIfNull(authSO, "loginButton", GameObject.Find("LoginButton"));
                SetPropertyIfNull(authSO, "goToRegisterButton", GameObject.Find("GoToRegisterButton"));
                SetPropertyIfNull(authSO, "goToForgotPasswordButton", GameObject.Find("GoToForgotPasswordButton"));

                // Status Text & Spinner
                SetPropertyIfNull(authSO, "statusText", GameObject.Find("StatusText"));
                SetPropertyIfNull(authSO, "loadingSpinner", GameObject.Find("LoadingSpinner"));

                authSO.ApplyModifiedProperties();
            }

            if (mainManager != null)
            {
                SerializedObject mainSO = new SerializedObject(mainManager);
                SetPropertyIfNull(mainSO, "mainMenuPanel", GameObject.Find("MainMenuPanel"));
                SetPropertyIfNull(mainSO, "settingsPanel", GameObject.Find("SettingsPanel") ?? GameObject.Find("Settings"));
                SetPropertyIfNull(mainSO, "authCanvasGroup", GameObject.Find("AuthGroupPanel") ?? GameObject.Find("AuthManager"));
                SetPropertyIfNull(mainSO, "gameLogoObject", logoGame);
                SetPropertyIfNull(mainSO, "userNameText", GameObject.Find("UserNameText"));
                SetPropertyIfNull(mainSO, "userEmailText", GameObject.Find("UserEmailText"));
                SetPropertyIfNull(mainSO, "playButton", GameObject.Find("PlayButton"));
                SetPropertyIfNull(mainSO, "settingsButton", GameObject.Find("SettingsButton"));
                
                SerializedProperty sceneProp = mainSO.FindProperty("gameSceneName");
                if (sceneProp != null)
                {
                    sceneProp.stringValue = "Map";
                }
                
                mainSO.ApplyModifiedProperties();
            }

            // Đăng ký tự động Login.unity và Map.unity vào Build Settings
            System.Collections.Generic.List<EditorBuildSettingsScene> buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!buildScenes.Exists(s => s.path.Contains("Login.unity")))
            {
                buildScenes.Insert(0, new EditorBuildSettingsScene("Assets/Game/Scenes/Login.unity", true));
            }
            if (!buildScenes.Exists(s => s.path.Contains("Map.unity")))
            {
                buildScenes.Add(new EditorBuildSettingsScene("Assets/Game/Scenes/Map.unity", true));
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();

            // 2. Tự động kiểm tra và dựng Scene ProceduralCombatDemo của Hoàng nếu chưa có
            string combatScenePath = "Assets/MidnightChaos/Generated/Scenes/ProceduralCombatDemo.unity";
            if (!System.IO.File.Exists(combatScenePath))
            {
                MidnightChaos.Editor.MidnightChaosProceduralDemoBuilder.CreateOrRefreshProceduralCombatDemo();
            }

            // 3. Tự động chạy lại các Builder UI, Texture Sprites & Background để khôi phục 100%
            ConvertUITexturesToSprites.ConvertImagesToSprites();
            DemoBackgroundInstaller.InstallBackground();
            MuckStyleUIBuilder.BuildMuckUI();
            MidnightChaos.Editor.IntroStoryUIBuilder.BuildIntroStoryUI();
            MuckHotbarCanvasBuilder.BuildHotbarCanvas();

            // 3. Bật hiển thị lại các Panel bị giấu (Ensure Active)
            GameObject loginPanel = GameObject.Find("LoginPanel");
            if (loginPanel != null) loginPanel.SetActive(true);

            GameObject authGroup = GameObject.Find("AuthGroupPanel");
            if (authGroup != null) authGroup.SetActive(true);

            if (logoGame != null) logoGame.SetActive(true);
            if (fireObj != null) fireObj.SetActive(true);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);

            EditorUtility.DisplayDialog("Hoàn Tất!",
                "Đã tự động quét và khôi phục 100% toàn bộ giao diện Scene Login:\n\n" +
                "✓ Đã gán lại tất cả liên kết Form Đăng nhập/Đăng ký.\n" +
                "✓ Đã khôi phục LogoGame, hiệu ứng Lửa Fire và viền Lửa Tím.\n" +
                "✓ Đã dựng lại Khung Muck Lobby và Nút Đăng xuất/Thoát game.\n" +
                "✓ Đã khôi phục Màn hình Dẫn dắt Cốt truyện Intro.", "OK");
        }

        private static void SetPropertyIfNull(SerializedObject so, string propName, GameObject targetObj)
        {
            if (targetObj == null) return;
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = targetObj;
            }
        }
    }
}
