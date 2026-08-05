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
        [SerializeField] private float burnSpeed = 3.5f;
        [SerializeField] private float scalePulseAmount = 0.05f; // Độ phồng xẹp ngọn lửa
        [SerializeField] private float flickerIntensity = 0.25f;

        private Image targetImage;
        private Outline targetOutline;
        private Shadow targetShadow;
        private Vector3 baseScale;

        private void OnEnable()
        {
            targetImage = GetComponent<Image>();
            targetOutline = GetComponent<Outline>();
            targetShadow = GetComponent<Shadow>();
            baseScale = transform.localScale;

            if (baseScale == Vector3.zero)
            {
                baseScale = Vector3.one * 1.08f;
            }
        }

        private void Update()
        {
            float time = (Application.isPlaying ? Time.time : (float)System.DateTime.Now.TimeOfDay.TotalSeconds) * burnSpeed;

            // 1. Phồng xẹp ngọn lửa (Pulse Scale)
            float noiseScale = Mathf.PerlinNoise(time * 0.8f, 0f);
            float currentPulse = 1f + (Mathf.Sin(time * 2f) * 0.5f + noiseScale * 0.5f) * scalePulseAmount;
            transform.localScale = baseScale * currentPulse;

            // 2. Chuyển đổi màu lửa nhấp nháy rực rỡ (Color Flicker)
            float colorLerp = Mathf.PerlinNoise(time * 1.5f, 10f);
            Color currentFlameColor = Color.Lerp(primaryPurple, secondaryViolet, colorLerp);

            float alphaNoise = 1f - (Mathf.PerlinNoise(time * 3f, 20f) * flickerIntensity);
            currentFlameColor.a *= alphaNoise;

            if (targetImage != null)
            {
                targetImage.color = currentFlameColor;
            }

            // 3. Hiệu ứng ngọn lửa uốn lượn (Flickering Outline & Shadow)
            if (targetOutline != null)
            {
                float offsetX = Mathf.Sin(time * 2.5f) * 6f + Mathf.Cos(time * 4f) * 3f;
                float offsetY = -Mathf.Abs(Mathf.Cos(time * 3f) * 8f) - 3f; // Luôn hướng bùng lên trên
                targetOutline.effectDistance = new Vector2(offsetX, offsetY);
                targetOutline.effectColor = new Color(secondaryViolet.r, secondaryViolet.g, secondaryViolet.b, 0.8f * alphaNoise);
            }

            if (targetShadow != null)
            {
                float shadowX = Mathf.Cos(time * 2f) * 10f;
                float shadowY = -Mathf.Abs(Mathf.Sin(time * 3.5f) * 12f) - 5f;
                targetShadow.effectDistance = new Vector2(shadowX, shadowY);
                targetShadow.effectColor = new Color(primaryPurple.r * 0.7f, primaryPurple.g * 0.7f, primaryPurple.b * 0.7f, 0.7f * alphaNoise);
            }
        }
    }
}
