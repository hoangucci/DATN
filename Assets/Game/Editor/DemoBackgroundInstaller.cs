using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class DemoBackgroundInstaller
    {
        [MenuItem("Game Utility/Install Demo Background (Nạp 3D Background cho Login)")]
        public static void InstallBackground()
        {
            GameObject existingBg = GameObject.Find("[Demo_Background]");
            if (existingBg != null)
            {
                existingBg.SetActive(true);
                Debug.Log("[BackgroundInstaller] Khung [Demo_Background] đã tồn tại và đã được kích hoạt.");
                return;
            }

            string prefabPath = "Assets/Game/Prefabs/DemoBackground.prefab";
            GameObject bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (bgPrefab == null)
            {
                // Tìm kiếm prefab trong toàn bộ dự án nếu đường dẫn đổi
                string[] guids = AssetDatabase.FindAssets("DemoBackground t:Prefab");
                if (guids.Length > 0)
                {
                    prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                }
            }

            if (bgPrefab != null)
            {
                GameObject bgInstance = (GameObject)PrefabUtility.InstantiatePrefab(bgPrefab);
                bgInstance.name = "[Demo_Background]";
                bgInstance.transform.position = Vector3.zero;
                bgInstance.transform.rotation = Quaternion.identity;
                bgInstance.transform.SetAsFirstSibling(); // Đưa về làm nền đằng sau

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[BackgroundInstaller] Đã nạp thành công 3D Map Background vào Scene Login!");
            }
            else
            {
                Debug.LogWarning("[BackgroundInstaller] Không tìm thấy DemoBackground.prefab!");
            }
        }
    }
}
