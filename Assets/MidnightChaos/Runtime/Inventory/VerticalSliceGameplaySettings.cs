using MidnightChaos.Enemies;
using UnityEngine;

namespace MidnightChaos.Inventory
{
    public enum GameplayGroupSizeMode : byte
    {
        Auto = 0,
        Manual = 1
    }

    [CreateAssetMenu(
        fileName = "VerticalSliceGameplaySettings",
        menuName = "Midnight Chaos/Gameplay/Vertical Slice Settings")]
    public sealed class VerticalSliceGameplaySettings : ScriptableObject
    {
        public const string ResourcePath =
            "Procedural/VerticalSliceGameplaySettings";

        [Header("World Items")]
        [Tooltip("Generic network prefab dùng cho Rock/Wood/Ore/Workbench/ChaosShard.")]
        [SerializeField] private GameObject worldItemNetworkPrefab;
        [Tooltip("Khoảng cách tối đa để player nhặt world item bằng E.")]
        [SerializeField, Min(0.5f)] private float pickupRadius = 1.4f;

        [Header("Harvest")]
        [Tooltip("Số hit cần để phá một Tree procedural.")]
        [SerializeField, Min(1)] private int treeHealth = 3;
        [Tooltip("Số hit cần để phá một Ore procedural.")]
        [SerializeField, Min(1)] private int oreHealth = 4;
        [Tooltip("Harvest damage khi player đang equip Rock.")]
        [SerializeField, Min(1)] private int rockHarvestDamage = 1;
        [Tooltip("Số Wood thật được drop khi phá Tree.")]
        [SerializeField, Min(1)] private int woodDropAmount = 3;
        [Tooltip("Số Ore thật được drop khi phá Ore node.")]
        [SerializeField, Min(1)] private int oreDropAmount = 2;

        [Header("Craft + Placement")]
        [Tooltip("Số Wood cần để craft một Workbench.")]
        [SerializeField, Min(1)] private int workbenchWoodCost = 3;
        [Tooltip("Khoảng cách đặt Workbench tính từ player/camera.")]
        [SerializeField, Min(1f)] private float placementDistance = 3.5f;
        [Tooltip("Độ cao ray origin dùng để tìm ground cho Workbench preview và server validation.")]
        [SerializeField, Min(0.1f)] private float placementGroundProbe = 3f;

        [Header("Gameplay Enemy Groups")]
        [Tooltip("Prefab enemy dùng bởi nút Spawn Enemy và gameplay group của demo.")]
        [SerializeField] private GameObject enemyPrefab;
        // Legacy serialized data only. Total runtime groups now come from the
        // actual Enemy Spawn Point registry and this field is intentionally
        // hidden from the Inspector.
        [SerializeField, HideInInspector]
        private int gameplayGroupCount = 3;
        [Tooltip("Auto lấy trực tiếp Required Enemy Group Size từ ChaosEvolutionProfile. Manual dùng Manual Gameplay Group Size mà không clamp theo evolution minimum.")]
        [SerializeField] private GameplayGroupSizeMode gameplayGroupSizeMode =
            GameplayGroupSizeMode.Auto;
        [Tooltip("Số enemy được spawn cho mỗi gameplay group khi Group Size Mode là Manual. Giá trị dưới evolution minimum được phép nhưng không bảo đảm tiến hóa tới Final Tier.")]
        [SerializeField, Min(1)] private int manualGameplayGroupSize = 8;
        [Tooltip("Số gameplay group tối đa được Active cùng lúc. Không giới hạn tổng gameplay enemy.")]
        [SerializeField, Range(1, 64)] private int maximumActiveGroups = 12;
        [Tooltip("Group Dormant/Suspended được Activate khi có ít nhất một player sống trong bán kính này tính từ group center.")]
        [SerializeField, Min(0.1f)] private float groupActivationDistance = 150f;
        [Tooltip("Active group được Suspended khi tất cả player sống ở xa hơn khoảng này. Phải lớn hơn Activation Distance.")]
        [SerializeField, Min(0.2f)] private float groupSuspensionDistance = 200f;
        [Tooltip("Khoảng thời gian giữa hai lần Host kiểm tra khoảng cách player-group.")]
        [SerializeField, Min(0.05f)] private float groupProximityCheckInterval =
            0.5f;
        [Tooltip("Bán kính tối đa quanh một Enemy Spawn Point dùng để đặt toàn bộ enemy của một gameplay group.")]
        [SerializeField, Min(1f)] private float gameplayGroupRadius = 8f;
        [Tooltip("Khoảng cách phẳng tối thiểu giữa hai enemy trong cùng gameplay group.")]
        [SerializeField, Min(0.25f)] private float gameplayGroupMinimumSpacing =
            1.5f;
        [Tooltip("Giới hạn debug enemy đang tồn tại do nút Spawn Enemy tạo ra.")]
        [SerializeField, Range(1, 32)] private int maximumActiveEnemies = 5;
        [Tooltip("Khoảng cách spawn debug enemy trước mặt Host.")]
        [SerializeField, Min(1f)] private float debugSpawnDistance = 5f;

        public GameObject WorldItemNetworkPrefab => worldItemNetworkPrefab;
        public float PickupRadius => Mathf.Max(0.5f, pickupRadius);
        public int TreeHealth => Mathf.Max(1, treeHealth);
        public int OreHealth => Mathf.Max(1, oreHealth);
        public int RockHarvestDamage => Mathf.Max(1, rockHarvestDamage);
        public int WoodDropAmount => Mathf.Max(1, woodDropAmount);
        public int OreDropAmount => Mathf.Max(1, oreDropAmount);
        public int WorkbenchWoodCost => Mathf.Max(1, workbenchWoodCost);
        public float PlacementDistance => Mathf.Max(1f, placementDistance);
        public float PlacementGroundProbe =>
            Mathf.Max(0.1f, placementGroundProbe);
        public GameObject EnemyPrefab => enemyPrefab;
        public GameplayGroupSizeMode GroupSizeMode => gameplayGroupSizeMode;
        public int ManualGameplayGroupSize =>
            Mathf.Max(1, manualGameplayGroupSize);
        public int MaximumActiveGroups =>
            Mathf.Clamp(maximumActiveGroups, 1, 64);
        public float GroupActivationDistance =>
            Mathf.Max(0.1f, groupActivationDistance);
        public float GroupSuspensionDistance => Mathf.Max(
            GroupActivationDistance + 0.1f,
            groupSuspensionDistance);
        public float GroupProximityCheckInterval =>
            Mathf.Max(0.05f, groupProximityCheckInterval);
        public float GameplayGroupRadius =>
            Mathf.Max(1f, gameplayGroupRadius);
        public float GameplayGroupMinimumSpacing =>
            Mathf.Clamp(
                gameplayGroupMinimumSpacing,
                0.25f,
                GameplayGroupRadius);
        public int MaximumActiveEnemies =>
            Mathf.Clamp(maximumActiveEnemies, 1, 32);
        public float DebugSpawnDistance => Mathf.Max(1f, debugSpawnDistance);

        public int ResolveGameplayGroupSize(
            ChaosEvolutionProfile evolutionProfile)
        {
            if (GroupSizeMode == GameplayGroupSizeMode.Manual)
            {
                return ManualGameplayGroupSize;
            }

            return evolutionProfile != null
                ? evolutionProfile.RequiredEnemyGroupSize
                : 1;
        }

        public void ConfigureReferencesIfEmpty(
            GameObject worldItemPrefab,
            GameObject configuredEnemyPrefab)
        {
            worldItemNetworkPrefab ??= worldItemPrefab;
            enemyPrefab ??= configuredEnemyPrefab;
        }

        private void OnValidate()
        {
            pickupRadius = PickupRadius;
            treeHealth = TreeHealth;
            oreHealth = OreHealth;
            rockHarvestDamage = RockHarvestDamage;
            woodDropAmount = WoodDropAmount;
            oreDropAmount = OreDropAmount;
            workbenchWoodCost = WorkbenchWoodCost;
            placementDistance = PlacementDistance;
            placementGroundProbe = PlacementGroundProbe;
            gameplayGroupCount = Mathf.Max(1, gameplayGroupCount);
            manualGameplayGroupSize = ManualGameplayGroupSize;
            maximumActiveGroups = MaximumActiveGroups;
            groupActivationDistance = GroupActivationDistance;
            groupSuspensionDistance = GroupSuspensionDistance;
            groupProximityCheckInterval = GroupProximityCheckInterval;
            gameplayGroupRadius = GameplayGroupRadius;
            gameplayGroupMinimumSpacing = GameplayGroupMinimumSpacing;
            maximumActiveEnemies = MaximumActiveEnemies;
            debugSpawnDistance = DebugSpawnDistance;
        }
    }
}
