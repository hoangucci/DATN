using System.IO;
using MidnightChaos.Runtime;
using UnityEditor;
using UnityEngine;

namespace MidnightChaos.Editor
{
    public static class MidnightChaosMapConfigCreator
    {
        public const string ConfigAssetPath = "Assets/MidnightChaos/Generated/MapGeneratorConfig.asset";

        [MenuItem("Midnight Chaos/Create or Find ScriptableObject Config")]
        public static MapGeneratorConfigSO GetOrCreateDefaultConfig()
        {
            if (!Directory.Exists("Assets/MidnightChaos/Generated"))
            {
                Directory.CreateDirectory("Assets/MidnightChaos/Generated");
                AssetDatabase.Refresh();
            }

            MapGeneratorConfigSO config = AssetDatabase.LoadAssetAtPath<MapGeneratorConfigSO>(ConfigAssetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MapGeneratorConfigSO>();
                config.mountainMaxHeight = 22.0f;
                config.hillsMaxHeight = 6.0f;
                config.baseHeightOffset = 4.0f;
                config.treeCount = 950;
                config.rockCount = 500;
                config.vegetationCount = 1200;

                AssetDatabase.CreateAsset(config, ConfigAssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Map Config Creator] Đã tạo file ScriptableObject Config tại {ConfigAssetPath}");
            }

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            return config;
        }
    }
}
