using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PurpleFlameBurnAnimator : MonoBehaviour
    {
        [Header("Flame Colors")]
        [ColorUsage(true, true)]
        [SerializeField] private Color primaryPurple = new Color(0.65f, 0.15f, 1.0f, 0.95f); // Deep Flame Purple

        [ColorUsage(true, true)]
        [SerializeField] private Color secondaryViolet = new Color(0.95f, 0.35f, 1.0f, 1.0f); // Bright Magenta Flame

        [Header("Animation Settings")]
        [SerializeField] private float burnSpeed = 3.0f;
        [SerializeField] private float scalePulseAmount = 0.015f; // Tỉ lệ phồng xẹp nhỏ gọn 1.5% vừa khít logo
        [SerializeField] private float flickerIntensity = 0.15f;

        private Image targetImage;
        private Outline targetOutline;
        private Shadow targetShadow;

        private void OnEnable()
        {
            targetImage = GetComponent<Image>();
            targetOutline = GetComponent<Outline>();
            targetShadow = GetComponent<Shadow>();

            // Khôi phục scale chuẩn vừa khít với logo chính (1.025x)
            transform.localScale = new Vector3(1.025f, 1.025f, 1.0f);
        }

        private void Update()
        {
            float time = (Application.isPlaying ? Time.time : (float)System.DateTime.Now.TimeOfDay.TotalSeconds) * burnSpeed;

            // 1. Phồng xẹp ngọn lửa nhẹ nhàng vừa khít viền (Subtle Pulse)
            float noiseScale = Mathf.PerlinNoise(time * 0.8f, 0f);
            float currentPulse = 1.025f + (Mathf.Sin(time * 2f) * 0.5f + noiseScale * 0.5f) * scalePulseAmount;
            transform.localScale = new Vector3(currentPulse, currentPulse, 1.0f);

            // 2. Chuyển đổi màu lửa nhấp nháy rực rỡ (Color Flicker)
            float colorLerp = Mathf.PerlinNoise(time * 1.5f, 10f);
            Color currentFlameColor = Color.Lerp(primaryPurple, secondaryViolet, colorLerp);

            float alphaNoise = 1f - (Mathf.PerlinNoise(time * 3f, 20f) * flickerIntensity);
            currentFlameColor.a *= alphaNoise;

            if (targetImage != null)
            {
                targetImage.color = currentFlameColor;
            }

            // 3. Hiệu ứng viền ngọn lửa vừa vặn tinh tế (Tight Outline & Shadow)
            if (targetOutline != null)
            {
                float offsetX = Mathf.Sin(time * 2.5f) * 2f;
                float offsetY = -Mathf.Abs(Mathf.Cos(time * 3f) * 3f) - 1f;
                targetOutline.effectDistance = new Vector2(offsetX, offsetY);
                targetOutline.effectColor = new Color(secondaryViolet.r, secondaryViolet.g, secondaryViolet.b, 0.7f * alphaNoise);
            }

            if (targetShadow != null)
            {
                float shadowX = Mathf.Cos(time * 2f) * 3f;
                float shadowY = -Mathf.Abs(Mathf.Sin(time * 3.5f) * 4f) - 2f;
                targetShadow.effectDistance = new Vector2(shadowX, shadowY);
                targetShadow.effectColor = new Color(primaryPurple.r * 0.6f, primaryPurple.g * 0.6f, primaryPurple.b * 0.6f, 0.6f * alphaNoise);
            }
        }
    }
}
