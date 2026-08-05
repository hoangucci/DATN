using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public class DemoBackgroundInstaller : EditorWindow
    {
        private string sourceScenePath = "Assets/Asset/StylizedNatureBundle/Demo/Scenes/DemoScene_01.unity";
        private string loginScenePath = "Assets/Game/Scenes/Login.unity";
        private string prefabSavePath = "Assets/Game/Prefabs/DemoBackground.prefab";

        [MenuItem("Game Utility/Copy Demo Scene to Login Scene")]
        public static void ShowWindow()
        {
            var window = GetWindow<DemoBackgroundInstaller>("Demo Background Installer");
            window.minSize = new Vector2(450, 320);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Đóng gói Demo Scene -> Prefab -> Scene Login", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Tool này chỉ hoạt động trong folder Assets/Game (Hoàn toàn KHÔNG động tới MidnightChaos).", MessageType.Info);

            GUILayout.Space(10);
            sourceScenePath = EditorGUILayout.TextField("Demo Scene Path", sourceScenePath);
            loginScenePath = EditorGUILayout.TextField("Login Scene Path", loginScenePath);
            prefabSavePath = EditorGUILayout.TextField("Prefab Output Path", prefabSavePath);

            GUILayout.Space(20);
            GUI.backgroundColor = new Color(0.2f, 0.7f, 1.0f);
            if (GUILayout.Button("TẠO PREFAB & ĐƯA VÀO SCENE LOGIN", GUILayout.Height(40)))
            {
                ExecuteProcess();
            }
            GUI.backgroundColor = Color.white;
        }

        private void ExecuteProcess()
        {
            if (!File.Exists(sourceScenePath))
            {
                EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy scene demo tại:\n{sourceScenePath}", "OK");
                return;
            }

            string prefabFolder = Path.GetDirectoryName(prefabSavePath);
            if (!Directory.Exists(prefabFolder))
            {
                Directory.CreateDirectory(prefabFolder);
                AssetDatabase.Refresh();
            }

            try
            {
                // 1. Open Source Demo Scene
                Scene demoScene = EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Single);

                // Save Lighting/RenderSettings
                Material skybox = RenderSettings.skybox;
                AmbientMode ambientMode = RenderSettings.ambientMode;
                Color ambientSkyColor = RenderSettings.ambientSkyColor;
                Color ambientEquatorColor = RenderSettings.ambientEquatorColor;
                Color ambientGroundColor = RenderSettings.ambientGroundColor;
                Color ambientLight = RenderSettings.ambientLight;

                bool fog = RenderSettings.fog;
                Color fogColor = RenderSettings.fogColor;
                FogMode fogMode = RenderSettings.fogMode;
                float fogDensity = RenderSettings.fogDensity;
                float fogStartDistance = RenderSettings.fogStartDistance;
                float fogEndDistance = RenderSettings.fogEndDistance;

                // Group all roots under container
                GameObject container = new GameObject("[Demo_Background]");
                GameObject[] roots = demoScene.GetRootGameObjects();
                foreach (GameObject r in roots)
                {
                    if (r == container) continue;
                    r.transform.SetParent(container.transform, true);
                }

                // Save Prefab
                GameObject prefabObj = PrefabUtility.SaveAsPrefabAsset(container, prefabSavePath);
                Debug.Log($"[GameUtility] Đã tạo Prefab tại: {prefabSavePath}");

                // 2. Open Login Scene
                Scene loginScene;
                if (File.Exists(loginScenePath))
                {
                    loginScene = EditorSceneManager.OpenScene(loginScenePath, OpenSceneMode.Single);
                }
                else
                {
                    string sceneDir = Path.GetDirectoryName(loginScenePath);
                    if (!Directory.Exists(sceneDir)) Directory.CreateDirectory(sceneDir);
                    loginScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    EditorSceneManager.SaveScene(loginScene, loginScenePath);
                }

                // Remove existing background container if present
                GameObject oldBg = GameObject.Find("[Demo_Background]");
                if (oldBg != null)
                {
                    DestroyImmediate(oldBg);
                }

                // Instantiate Prefab into Login Scene
                GameObject instantiated = (GameObject)PrefabUtility.InstantiatePrefab(prefabObj, loginScene);
                instantiated.name = "[Demo_Background]";

                // Apply RenderSettings
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = ambientSkyColor;
                RenderSettings.ambientEquatorColor = ambientEquatorColor;
                RenderSettings.ambientGroundColor = ambientGroundColor;
                RenderSettings.ambientLight = ambientLight;

                RenderSettings.fog = fog;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogStartDistance = fogStartDistance;
                RenderSettings.fogEndDistance = fogEndDistance;

                EditorSceneManager.MarkSceneDirty(loginScene);
                EditorSceneManager.SaveScene(loginScene);

                EditorUtility.DisplayDialog("Hoàn tất", $"Đã tạo Prefab tại:\n{prefabSavePath}\n\nvà chèn vào Scene Login tại:\n{loginScenePath}!", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameUtility] Lỗi: {ex.Message}");
                EditorUtility.DisplayDialog("Lỗi", ex.Message, "OK");
            }
        }
    }
}
