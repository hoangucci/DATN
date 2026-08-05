using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class LogoGlowSetupTool
    {
        [MenuItem("GameObject/Game Utility/Setup Purple Glow for Logo (Cách 1 - Lửa Cháy)", false, 10)]
        [MenuItem("Game Utility/Setup Purple Glow for Logo (Cách 1 - Lửa Cháy)")]
        public static void CreatePurpleGlowLayer()
        {
            GameObject logoObj = Selection.activeGameObject;

            if (logoObj == null)
            {
                logoObj = GameObject.Find("logo game") ?? GameObject.Find("Logo Game") ?? GameObject.Find("Logo");
            }

            if (logoObj == null)
            {
                EditorUtility.DisplayDialog("Thông báo",
                    "Không tìm thấy GameObject tên 'logo game'. Vui lòng chọn GameObject Logo trong Hierarchy rồi bấm lại!", "OK");
                return;
            }

            // Dọn dẹp hạt cũ nếu có
            Transform existingParticles1 = logoObj.transform.Find("[PurpleFlame_Border_Particles]");
            if (existingParticles1 != null) Object.DestroyImmediate(existingParticles1.gameObject);
            Transform existingParticles2 = logoObj.transform.Find("[PurpleSparks_Particles]");
            if (existingParticles2 != null) Object.DestroyImmediate(existingParticles2.gameObject);

            Transform parentTransform = logoObj.transform.parent;
            string glowName = "logo_purple_glow";

            Transform existingGlow = parentTransform != null ? parentTransform.Find(glowName) : null;
            if (existingGlow != null)
            {
                Object.DestroyImmediate(existingGlow.gameObject);
            }

            // 1. Nhân bản logo game làm lớp glow
            GameObject glowObj = Object.Instantiate(logoObj, parentTransform);
            glowObj.name = glowName;

            // Xoá các script thừa trên bản sao
            var scripts = glowObj.GetComponents<MonoBehaviour>();
            foreach (var script in scripts)
            {
                if (script != null && !(script is Image) && !(script is RawImage))
                {
                    Object.DestroyImmediate(script);
                }
            }

            // Đặt vị trí hiển thị ngay sau logo game trong Hierarchy
            int logoIndex = logoObj.transform.GetSiblingIndex();
            glowObj.transform.SetSiblingIndex(logoIndex);

            // Scale nhẹ
            glowObj.transform.localScale = logoObj.transform.localScale * 1.08f;
            glowObj.transform.localPosition = logoObj.transform.localPosition;

            // 2. Gắn các Component Outline, Shadow & Animation Lửa Cháy
            Outline outline = glowObj.GetComponent<Outline>() ?? glowObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.9f, 0.35f, 1.0f, 0.9f);
            outline.effectDistance = new Vector2(6f, -6f);

            Shadow shadow = glowObj.GetComponent<Shadow>() ?? glowObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.5f, 0.1f, 0.9f, 0.7f);
            shadow.effectDistance = new Vector2(12f, -12f);

            // Gắn Animator hiệu ứng ngọn lửa uốn lượn nhấp nháy
            PurpleFlameBurnAnimator flameAnimator = glowObj.GetComponent<PurpleFlameBurnAnimator>() ?? glowObj.AddComponent<PurpleFlameBurnAnimator>();

            EditorUtility.SetDirty(glowObj);
            EditorSceneManager.MarkSceneDirty(glowObj.scene);

            Selection.activeGameObject = glowObj;
            Debug.Log($"[LogoGlow] Đã tạo thành công lớp Lửa Tím bùng cháy 'logo_purple_glow' phía sau '{logoObj.name}'!");

            EditorUtility.DisplayDialog("Thành công!",
                $"Đã tạo thành công lớp viền LỬA TÍM CHÁY BÙNG BÙNG (Dynamic Purple Flame) cho '{logoObj.name}'!\n\n" +
                "Ngọn lửa sẽ phồng xẹp, chuyển màu nhấp nháy uốn lượn liên tục trong cả Scene View và Game View!", "OK");
        }
    }
}
