using UnityEngine;

namespace MidnightChaos.Resources
{
    [DisallowMultipleComponent]
    public class DayNightCycleController : MonoBehaviour
    {
        [Header("Cycle Settings (Thời gian Chu kỳ Ngày / Đêm)")]
        [Tooltip("Độ dài 1 ngày đêm tính bằng Giây (Ví dụ: 120s = 2 phút cho 1 ngày đêm)")]
        [SerializeField, Range(10f, 1200f)] private float dayDurationSeconds = 120f;

        [Tooltip("Thời gian hiện tại trong ngày (0.0 = Đêm 0h, 0.25 = Bình minh 6h, 0.5 = Trưa 12h, 0.75 = Hoàng hôn 18h)")]
        [SerializeField, Range(0f, 1f)] private float currentTimeNormalized = 0.25f;

        [Tooltip("Tạm dừng chu kỳ ngày đêm")]
        [SerializeField] private bool pauseCycle = false;

        [Header("Light & Fog References")]
        [SerializeField] private Light sunLight;

        [Header("Color Gradients (Phối màu Nắng & Sương Mù)")]
        [SerializeField] private Gradient sunColorGradient;
        [SerializeField] private Gradient fogColorGradient;
        [SerializeField] private Gradient ambientSkyGradient;
        [SerializeField] private Gradient ambientEquatorGradient;
        [SerializeField] private Gradient ambientGroundGradient;

        public int CurrentDayNumber { get; private set; } = 1;
        public bool IsDaytime => currentTimeNormalized >= 0.25f && currentTimeNormalized <= 0.75f;
        public float CurrentTimeNormalized => currentTimeNormalized;

        public event System.Action<int> OnDayChanged;
        public event System.Action<bool> OnDayNightStateChanged; // true = Day, false = Night

        private bool _wasDay = true;

        private void Awake()
        {
            // Tự động kiểm tra và gán Skybox M_SNB_Skybox.mat nếu chưa có
            if (RenderSettings.skybox == null)
            {
                Material skyboxMat = null;
#if UNITY_EDITOR
                skyboxMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/Environments/StylizedNatureBundle/Materials/M_SNB_Skybox.mat")
                         ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/StylizedNatureBundle/Materials/M_SNB_Skybox.mat");
#endif
                if (skyboxMat == null)
                {
                    skyboxMat = UnityEngine.Resources.Load<Material>("M_SNB_Skybox");
                }

                if (skyboxMat != null)
                {
                    RenderSettings.skybox = skyboxMat;
                    DynamicGI.UpdateEnvironment();
                }
            }

            if (sunLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (Light l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        sunLight = l;
                        break;
                    }
                }
            }

            // Tắt thuộc tính Flare legacy cũ trên Directional Light để loại bỏ chuỗi vệt tròn chói mắt
            if (sunLight != null)
            {
                sunLight.flare = null;
            }

            InitDefaultGradients();
        }

        private void InitDefaultGradients()
        {
            // 1. Phối màu Ánh sáng Nắng & Ánh Trăng
            if (sunColorGradient == null || sunColorGradient.colorKeys.Length <= 1)
            {
                sunColorGradient = new Gradient();
                GradientColorKey[] colors = new GradientColorKey[5];
                colors[0] = new GradientColorKey(new Color(0.35f, 0.55f, 0.95f), 0.0f);   // Đêm (0h): Ánh Trăng xanh mộng mơ chuẩn Muck
                colors[1] = new GradientColorKey(new Color(1.0f, 0.72f, 0.45f), 0.25f);  // Bình minh (6h): Nắng hồng cam ấm
                colors[2] = new GradientColorKey(new Color(1.0f, 0.94f, 0.85f), 0.50f);  // Trưa (12h): Nắng ấm rực rỡ
                colors[3] = new GradientColorKey(new Color(1.0f, 0.50f, 0.25f), 0.75f);  // Hoàng hôn (18h): Nắng cam đỏ tía
                colors[4] = new GradientColorKey(new Color(0.35f, 0.55f, 0.95f), 1.0f);   // Đêm (24h)

                GradientAlphaKey[] alphas = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
                sunColorGradient.SetKeys(colors, alphas);
            }

            // 2. Phối màu Sương mù Atmospheric Fog
            if (fogColorGradient == null || fogColorGradient.colorKeys.Length <= 1)
            {
                fogColorGradient = new Gradient();
                GradientColorKey[] colors = new GradientColorKey[5];
                colors[0] = new GradientColorKey(new Color(0.12f, 0.22f, 0.38f), 0.0f);  // Đêm (0h): Sương chàm dịu mắt, nhìn rõ đường & quái
                colors[1] = new GradientColorKey(new Color(0.95f, 0.70f, 0.58f), 0.25f); // Bình minh (6h): Sương ửng hồng rực rỡ
                colors[2] = new GradientColorKey(new Color(0.485f, 0.855f, 0.943f), 0.5f);// Trưa (12h): Sương Cyan mộng mơ Demo 01
                colors[3] = new GradientColorKey(new Color(0.85f, 0.45f, 0.42f), 0.75f);  // Hoàng hôn (18h): Sương đỏ tím chiều tà
                colors[4] = new GradientColorKey(new Color(0.12f, 0.22f, 0.38f), 1.0f);  // Đêm (24h)

                GradientAlphaKey[] alphas = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
                fogColorGradient.SetKeys(colors, alphas);
            }

            // 3. Phối màu Môi trường Sky Ambient
            if (ambientSkyGradient == null || ambientSkyGradient.colorKeys.Length <= 1)
            {
                ambientSkyGradient = new Gradient();
                GradientColorKey[] colors = new GradientColorKey[5];
                colors[0] = new GradientColorKey(new Color(0.12f, 0.20f, 0.38f), 0.0f);  // Đêm: Bầu trời mộc lam
                colors[1] = new GradientColorKey(new Color(0.25f, 0.18f, 0.28f), 0.25f); // Bình minh
                colors[2] = new GradientColorKey(new Color(0.035f, 0.133f, 0.255f), 0.5f);// Trưa
                colors[3] = new GradientColorKey(new Color(0.28f, 0.15f, 0.22f), 0.75f); // Hoàng hôn
                colors[4] = new GradientColorKey(new Color(0.12f, 0.20f, 0.38f), 1.0f);  // Đêm

                GradientAlphaKey[] alphas = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
                ambientSkyGradient.SetKeys(colors, alphas);
            }

            // 4. Phối màu Chân trời & Mặt đất
            if (ambientEquatorGradient == null || ambientEquatorGradient.colorKeys.Length <= 1)
            {
                ambientEquatorGradient = new Gradient();
                GradientColorKey[] colors = new GradientColorKey[3];
                colors[0] = new GradientColorKey(new Color(0.08f, 0.16f, 0.28f), 0.0f);  // Đêm
                colors[1] = new GradientColorKey(new Color(0.314f, 0.377f, 0.300f), 0.5f);// Trưa
                colors[2] = new GradientColorKey(new Color(0.08f, 0.16f, 0.28f), 1.0f);  // Đêm
                ambientEquatorGradient.SetKeys(colors, new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            }

            if (ambientGroundGradient == null || ambientGroundGradient.colorKeys.Length <= 1)
            {
                ambientGroundGradient = new Gradient();
                GradientColorKey[] colors = new GradientColorKey[3];
                colors[0] = new GradientColorKey(new Color(0.06f, 0.14f, 0.22f), 0.0f);  // Đêm (Mặt đất hắt sáng dịu nhìn rõ cỏ cây)
                colors[1] = new GradientColorKey(new Color(0.185f, 0.254f, 0.128f), 0.5f);// Trưa
                colors[2] = new GradientColorKey(new Color(0.06f, 0.14f, 0.22f), 1.0f);  // Đêm
                ambientGroundGradient.SetKeys(colors, new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            }
        }

        private void Update()
        {
            if (!pauseCycle && dayDurationSeconds > 0f)
            {
                float prevTime = currentTimeNormalized;
                currentTimeNormalized += Time.deltaTime / dayDurationSeconds;
                if (currentTimeNormalized >= 1f)
                {
                    currentTimeNormalized %= 1f;
                }

                // Phát hiện chuyển sang Bình minh (0.25f) -> Tăng số ngày (DAY 1 -> DAY 2)
                if (prevTime < 0.25f && currentTimeNormalized >= 0.25f)
                {
                    CurrentDayNumber++;
                    OnDayChanged?.Invoke(CurrentDayNumber);
                }

                // Phát hiện chuyển giao trạng thái Ngày <-> Đêm
                bool currentDayState = IsDaytime;
                if (currentDayState != _wasDay)
                {
                    _wasDay = currentDayState;
                    OnDayNightStateChanged?.Invoke(currentDayState);
                }
            }

            UpdateLightingAndAtmosphere(currentTimeNormalized);
        }

        public void SetTimeNormalized(float time0to1)
        {
            currentTimeNormalized = Mathf.Clamp01(time0to1);
            UpdateLightingAndAtmosphere(currentTimeNormalized);
        }

        private void UpdateLightingAndAtmosphere(float time)
        {
            // 1. Quỹ đạo Mặt Trời quay tròn 360 độ chuẩn vật lý (Mặt Trời lặn hẳn xuống chân trời)
            float sunAngleX = (time * 360f) - 90f;
            bool isDaytime = time >= 0.25f && time <= 0.75f;

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(sunAngleX, 130f, 0f);

                if (isDaytime)
                {
                    // BAN NGÀY (6h -> 18h): Nắng ấm mọc và lặn mượt mượt từ 0 đến 1.35f
                    float dayProgress = (time - 0.25f) / 0.5f;
                    sunLight.intensity = Mathf.Lerp(0.0f, 1.35f, Mathf.Sin(dayProgress * Mathf.PI));
                    sunLight.color = sunColorGradient.Evaluate(time);
                    sunLight.enabled = true;
                }
                else
                {
                    // BAN ĐÊM (18h -> 6h): Mặt Trời chìm hẳn dưới lòng đất -> TẮT HOÀN TOÀN CƯỜNG ĐỘ ĐÈN (Intensity = 0)
                    // Tuyệt đối KHÔNG BAO GIỜ có vệt sáng chiếu ngược từ lòng đất lên!
                    sunLight.intensity = 0f;
                }
            }

            // 2. Chuyển màu Sương Mù & Ánh sáng môi trường (Ambient Trilight)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSkyGradient.Evaluate(time);
            RenderSettings.ambientEquatorColor = ambientEquatorGradient.Evaluate(time);
            RenderSettings.ambientGroundColor = ambientGroundGradient.Evaluate(time);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColorGradient.Evaluate(time);

            // Tầm nhìn sương mù (Ban đêm giữ 180m rộ mở giúp combat đánh quái thoải mái)
            float fogFactor = Mathf.Clamp01(Mathf.Sin(time * Mathf.PI));
            RenderSettings.fogStartDistance = Mathf.Lerp(25f, 15f, fogFactor);
            RenderSettings.fogEndDistance = Mathf.Lerp(180f, 220f, fogFactor);

            // 3. Tự động đồng bộ màu Bầu trời Skybox Tint (Hiển thị vầng Mặt Trời sáng rực trên bầu trời)
            if (RenderSettings.skybox != null)
            {
                Material skyMat = RenderSettings.skybox;
                if (skyMat.HasProperty("_SkyTint"))
                {
                    skyMat.SetColor("_SkyTint", ambientSkyGradient.Evaluate(time));
                }
                else if (skyMat.HasProperty("_Tint"))
                {
                    skyMat.SetColor("_Tint", Color.Lerp(new Color(0.15f, 0.25f, 0.45f), new Color(0.64f, 0.75f, 0.80f), fogFactor));
                }
            }
        }
    }
}
