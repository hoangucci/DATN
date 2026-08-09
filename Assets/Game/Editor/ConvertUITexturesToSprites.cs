using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class ConvertUITexturesToSprites
    {
        [MenuItem("Game Utility/Fix & Convert UI Folder Images to Sprites (Khôi Phục Ảnh UI)")]
        public static void ConvertImagesToSprites()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Asset/UI", "Assets/Game/UI" });
            int convertedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null)
                {
                    bool needsSave = false;

                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        needsSave = true;
                    }

                    if (needsSave)
                    {
                        importer.SaveAndReimport();
                        convertedCount++;
                    }
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ConvertUITextures] Đã tự động chuyển đổi {convertedCount} file ảnh trong thư mục UI thành chuẩn Sprite (2D and UI)!");
            EditorUtility.DisplayDialog("Hoàn tất!", $"Đã tự động kiểm tra và chuyển đổi {convertedCount} file ảnh trong thư mục UI sang định dạng Sprite (2D and UI) mượt mà!", "OK");
        }
    }
}
