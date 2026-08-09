using System;
using UnityEngine;

namespace MidnightChaos.Enemies
{
    [Serializable]
    public sealed class ChaosEvolutionTierSettings
    {
        [Tooltip("Tên hiển thị của tier. Core evolution dùng index mảng, không phụ thuộc tên này.")]
        [SerializeField] private string displayName = "Small";
        [Tooltip("Charge cần tiêu thụ để chuyển sang tier kế tiếp. Bị bỏ qua ở Final Tier.")]
        [SerializeField, Min(1)] private int chargesToNextTier = 2;
        [Tooltip("Đánh dấu tier cuối. Tier cuối không nhận thêm charge để tiến hóa.")]
        [SerializeField] private bool finalTier;
        [Tooltip("Scale áp dụng cho visual enemy ở tier này.")]
        [SerializeField] private Vector3 bodyScale = Vector3.one;
        [Tooltip("Hệ số Max HP so với Mature Max Health trên enemy prefab.")]
        [SerializeField, Min(0.05f)] private float healthMultiplier = 1f;
        [Tooltip("Hệ số damage của enemy.")]
        [SerializeField, Min(0.05f)] private float damageMultiplier = 1f;
        [Tooltip("Hệ số tốc độ NavMeshAgent.")]
        [SerializeField, Min(0.05f)] private float speedMultiplier = 1f;
        [Tooltip("Hệ số tầm đánh.")]
        [SerializeField, Min(0.05f)] private float attackReachMultiplier = 1f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? "Tier"
            : displayName;
        public int ChargesToNextTier => Mathf.Max(1, chargesToNextTier);
        public bool FinalTier => finalTier;
        public Vector3 BodyScale => bodyScale;
        public float HealthMultiplier => Mathf.Max(0.05f, healthMultiplier);
        public float DamageMultiplier => Mathf.Max(0.05f, damageMultiplier);
        public float SpeedMultiplier => Mathf.Max(0.05f, speedMultiplier);
        public float AttackReachMultiplier =>
            Mathf.Max(0.05f, attackReachMultiplier);

        public ChaosEvolutionTierSettings()
        {
        }

        public ChaosEvolutionTierSettings(
            string name,
            int chargeCost,
            bool isFinal,
            Vector3 scale,
            float health,
            float damage,
            float speed,
            float reach)
        {
            displayName = name;
            chargesToNextTier = chargeCost;
            finalTier = isFinal;
            bodyScale = scale;
            healthMultiplier = health;
            damageMultiplier = damage;
            speedMultiplier = speed;
            attackReachMultiplier = reach;
        }
    }

    [CreateAssetMenu(
        fileName = "ChaosEvolutionProfile",
        menuName = "Midnight Chaos/Enemies/Chaos Evolution Profile")]
    public sealed class ChaosEvolutionProfile : ScriptableObject
    {
        public const string ResourcePath = "Procedural/ChaosEvolutionProfile";

        [Tooltip("Charge cơ bản mà một enemy đóng góp khi chết.")]
        [SerializeField, Min(1)] private int baseDeathCharge = 1;
        [Tooltip("Số enemy dự phòng thêm vào group sau khi đã đủ số death cần cho toàn bộ evolution threshold.")]
        [SerializeField, Min(0)] private int groupExtraEnemies = 1;
        [Tooltip("Bán kính tối đa để charge chuyển sang enemy cùng species gần nhất.")]
        [SerializeField, Min(0.1f)] private float evolutionRadius = 250f;
        [Tooltip("Số ChaosShard được drop khi enemy Final Tier chết.")]
        [SerializeField, Min(1)] private int chaosShardAmount = 1;
        [Tooltip("Danh sách tier theo thứ tự evolution. Tier cuối phải bật Final Tier.")]
        [SerializeField] private ChaosEvolutionTierSettings[] tiers =
        {
            new ChaosEvolutionTierSettings(
                "Small", 2, false,
                new Vector3(0.68f, 0.72f, 0.68f),
                0.55f, 0.70f, 1.15f, 0.85f),
            new ChaosEvolutionTierSettings(
                "Mature", 3, false,
                new Vector3(0.9f, 1.05f, 0.9f),
                1f, 1f, 1f, 1f),
            new ChaosEvolutionTierSettings(
                "Alpha", 1, true,
                new Vector3(1.35f, 1.55f, 1.35f),
                2.20f, 1.50f, 0.85f, 1.25f)
        };

        public int BaseDeathCharge => Mathf.Max(1, baseDeathCharge);
        public int GroupExtraEnemies => Mathf.Max(0, groupExtraEnemies);
        public float EvolutionRadius => Mathf.Max(0.1f, evolutionRadius);
        public int ChaosShardAmount => Mathf.Max(1, chaosShardAmount);
        public ChaosEvolutionTierSettings[] Tiers => tiers ??
            Array.Empty<ChaosEvolutionTierSettings>();

        public int MinimumRequiredEnemyGroupSize =>
            Mathf.Max(2, CalculateRequiredDeaths() + 1);

        public int RequiredEnemyGroupSize
        {
            get
            {
                return Mathf.Max(
                    MinimumRequiredEnemyGroupSize,
                    CalculateRequiredDeaths() + 1 + GroupExtraEnemies);
            }
        }

        private int CalculateRequiredDeaths()
        {
            int result = 0;
            foreach (ChaosEvolutionTierSettings tier in Tiers)
            {
                if (tier == null || tier.FinalTier)
                {
                    break;
                }
                result += tier.ChargesToNextTier;
            }
            return result;
        }

        private void OnValidate()
        {
            baseDeathCharge = BaseDeathCharge;
            groupExtraEnemies = GroupExtraEnemies;
            evolutionRadius = EvolutionRadius;
            chaosShardAmount = ChaosShardAmount;
            tiers ??= Array.Empty<ChaosEvolutionTierSettings>();
        }
    }
}
