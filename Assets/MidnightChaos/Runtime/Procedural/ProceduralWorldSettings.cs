using System;
using System.Collections.Generic;
using MidnightChaos.World;
using UnityEngine;

namespace MidnightChaos.Procedural
{
    public enum ProceduralSurfaceAlignment : byte
    {
        Upright = 0,
        AlignToSurfaceNormal = 1
    }

    public enum ProceduralNavigationMode : byte
    {
        None = 0,
        BakeIntoNavMesh = 1,
        DynamicCarving = 2
    }

    [Serializable]
    public sealed class ProceduralCategorySettings
    {
        [Tooltip("Danh sách definition được chọn bằng cùng một seeded random trên Host và Client. Thứ tự và phần tử lặp là cấu hình trọng số deterministic; danh tính object dùng Stable ID, không dùng vị trí mảng.")]
        [SerializeField] private WorldObjectDefinition[] definitions =
            Array.Empty<WorldObjectDefinition>();
        [Tooltip("Số vật thể tối đa hệ thống cố gắng đặt cho category này.")]
        [SerializeField, Min(0)] private int count;
        [Tooltip("Bán kính giữ khoảng cách trên mặt phẳng XZ. Tăng giá trị để giảm spawn chồng nhau.")]
        [SerializeField, Min(0.1f)] private float clearanceRadius = 1f;
        [Tooltip("Khoảng scale đồng đều ngẫu nhiên. Dùng uniform scale để giữ transform deterministic và không làm méo collider.")]
        [SerializeField] private Vector2 uniformScaleRange = Vector2.one;
        [Tooltip("Độ nghiêng ngẫu nhiên thêm vào sau khi áp dụng surface alignment.")]
        [SerializeField, Range(0f, 30f)] private float randomTiltDegrees;
        [Tooltip("Upright giữ trục up thẳng đứng (phù hợp cây). Align To Surface Normal đặt BottomPoint vuông góc với normal (phù hợp đá/quặng).")]
        [SerializeField] private ProceduralSurfaceAlignment surfaceAlignment;
        [Tooltip("None: không tham gia navigation. Bake Into NavMesh: collider tạo lỗ cố định khi build. Dynamic Carving: bắt buộc prefab có NavMeshObstacle và bật Carving; runtime không tự tạo component.")]
        [SerializeField] private ProceduralNavigationMode navigationMode =
            ProceduralNavigationMode.DynamicCarving;
        [SerializeField, HideInInspector]
        private float layoutHashCompatibilityValue;

        public WorldObjectDefinition[] Definitions =>
            definitions ?? Array.Empty<WorldObjectDefinition>();
        public int Count => Mathf.Max(0, count);
        public float ClearanceRadius => Mathf.Max(0.1f, clearanceRadius);
        public Vector2 UniformScaleRange => new Vector2(
            Mathf.Max(0.05f, Mathf.Min(uniformScaleRange.x, uniformScaleRange.y)),
            Mathf.Max(0.05f, Mathf.Max(uniformScaleRange.x, uniformScaleRange.y)));
        public float RandomTiltDegrees => Mathf.Clamp(randomTiltDegrees, 0f, 30f);
        public ProceduralSurfaceAlignment SurfaceAlignment => surfaceAlignment;
        public ProceduralNavigationMode NavigationMode => navigationMode;
        public float LayoutHashCompatibilityValue =>
            Mathf.Clamp(layoutHashCompatibilityValue, 0f, 0.2f);

        public void ConfigureDefinitionReferencesIfEmpty(
            WorldObjectDefinition[] configuredDefinitions)
        {
            if ((definitions == null || definitions.Length == 0) &&
                configuredDefinitions != null)
            {
                definitions = configuredDefinitions;
            }
        }

        internal void Sanitize()
        {
            count = Mathf.Max(0, count);
            clearanceRadius = Mathf.Max(0.1f, clearanceRadius);
            uniformScaleRange = UniformScaleRange;
            randomTiltDegrees = Mathf.Clamp(randomTiltDegrees, 0f, 30f);
            layoutHashCompatibilityValue = LayoutHashCompatibilityValue;
            definitions ??= Array.Empty<WorldObjectDefinition>();
        }
    }

    [Serializable]
    public sealed class ProceduralGrassClusterSettings
    {
        [Tooltip("Khoảng số Grass instance mục tiêu trong một cluster. Mỗi cluster chỉ chọn một GrassDefinition.")]
        [SerializeField] private Vector2Int instancesPerClusterRange =
            new Vector2Int(50, 100);
        [Tooltip("Khoảng bán kính cluster tính bằng world unit. Giảm bán kính làm Grass dày hơn.")]
        [SerializeField] private Vector2 radiusRange = new Vector2(3f, 7f);
        [Tooltip("Khoảng cách tâm tối thiểu giữa Grass trong cùng generation pass. Giá trị được chọn deterministic một lần cho mỗi cluster.")]
        [SerializeField] private Vector2 minimumSpacingRange =
            new Vector2(0.15f, 0.3f);

        public Vector2Int InstancesPerClusterRange => new Vector2Int(
            Mathf.Max(1, Mathf.Min(
                instancesPerClusterRange.x,
                instancesPerClusterRange.y)),
            Mathf.Max(1, Mathf.Max(
                instancesPerClusterRange.x,
                instancesPerClusterRange.y)));
        public Vector2 RadiusRange => new Vector2(
            Mathf.Max(0.1f, Mathf.Min(radiusRange.x, radiusRange.y)),
            Mathf.Max(0.1f, Mathf.Max(radiusRange.x, radiusRange.y)));
        public Vector2 MinimumSpacingRange => new Vector2(
            Mathf.Max(0.01f, Mathf.Min(
                minimumSpacingRange.x,
                minimumSpacingRange.y)),
            Mathf.Max(0.01f, Mathf.Max(
                minimumSpacingRange.x,
                minimumSpacingRange.y)));

        internal void Sanitize()
        {
            instancesPerClusterRange = InstancesPerClusterRange;
            radiusRange = RadiusRange;
            minimumSpacingRange = MinimumSpacingRange;
        }

        internal void Validate(List<string> errors)
        {
            if (instancesPerClusterRange.x <= 0 ||
                instancesPerClusterRange.y < instancesPerClusterRange.x)
            {
                errors.Add(
                    "Grass Instances Per Cluster must satisfy 0 < Min <= Max.");
            }
            if (radiusRange.x <= 0f || radiusRange.y < radiusRange.x)
            {
                errors.Add("Grass Cluster Radius must satisfy 0 < Min <= Max.");
            }
            if (minimumSpacingRange.x <= 0f ||
                minimumSpacingRange.y < minimumSpacingRange.x)
            {
                errors.Add(
                    "Grass Minimum Spacing must satisfy 0 < Min <= Max.");
            }
        }
    }

    [Serializable]
    public sealed class ProceduralSmallRockSettings
    {
        [Tooltip("Prefab visual Small Rock. Runtime clone visual này vào generic network world item; component gameplay trên source prefab không được instantiate.")]
        [SerializeField] private GameObject visualPrefab;
        [Tooltip("Bật để tính số Small Rock từ diện tích map và Density Per 1000 m². Tắt để dùng Fixed Count.")]
        [SerializeField] private bool useDensity = true;
        [Tooltip("Số Small Rock cố định khi Use Density tắt.")]
        [SerializeField, Range(1, 256)] private int fixedCount = 48;
        [Tooltip("Số Small Rock mục tiêu trên mỗi 1.000 m² khi dùng density.")]
        [SerializeField, Min(0.01f)] private float densityPer1000SquareMeters =
            0.05f;
        [Tooltip("Khoảng cách tối thiểu giữa hai Small Rock và với object procedural đã reserve.")]
        [SerializeField, Min(0.1f)] private float minimumSpacing = 2f;
        [Tooltip("Số Rock trong tổng target được ưu tiên đặt gần player spawn đầu tiên.")]
        [SerializeField, Range(0, 8)] private int starterCount = 2;
        [Tooltip("Bán kính tuning tối đa của starter Rock. Runtime còn cap theo Pickup Radius để vòng gameplay có thể bắt đầu ngay.")]
        [SerializeField, Min(2f)] private float starterRadius = 8f;
        [Tooltip("Giới hạn placement thực tế quanh player spawn để starter Rock nằm trong tầm tương tác. Giá trị migrate 1.05 giữ đúng behavior cũ từ Pickup Radius 1.4 x 75%.")]
        [SerializeField, Min(0.1f)] private float starterReachRadius = 1.05f;
        [Tooltip("Khoảng nâng root pickup khỏi mặt terrain.")]
        [SerializeField, Min(0f)] private float groundOffset = 0.18f;
        [Tooltip("Upright giữ Small Rock thẳng đứng như behavior cũ. Align To Surface Normal căn trục up theo normal terrain.")]
        [SerializeField] private ProceduralSurfaceAlignment surfaceAlignment =
            ProceduralSurfaceAlignment.Upright;
        [Tooltip("Độ nghiêng ngẫu nhiên quanh hai trục local sau surface alignment. Random được derive từ world seed và không làm đổi PRNG placement.")]
        [SerializeField, Range(0f, 30f)] private float randomTiltDegrees;

        public GameObject VisualPrefab => visualPrefab;
        public bool UseDensity => useDensity;
        public int FixedCount => Mathf.Clamp(fixedCount, 1, 256);
        public float DensityPer1000SquareMeters =>
            Mathf.Max(0.01f, densityPer1000SquareMeters);
        public float MinimumSpacing => Mathf.Max(0.1f, minimumSpacing);
        public int StarterCount => Mathf.Clamp(starterCount, 0, 8);
        public float StarterRadius => Mathf.Max(2f, starterRadius);
        public float StarterReachRadius => Mathf.Max(0.1f, starterReachRadius);
        public float GroundOffset => Mathf.Max(0f, groundOffset);
        public ProceduralSurfaceAlignment SurfaceAlignment => surfaceAlignment;
        public float RandomTiltDegrees =>
            Mathf.Clamp(randomTiltDegrees, 0f, 30f);

        public int CalculateTargetCount(Vector2 mapSize)
        {
            if (!UseDensity)
            {
                return FixedCount;
            }

            float area = Mathf.Max(1f, mapSize.x * mapSize.y);
            return Mathf.Clamp(
                Mathf.RoundToInt(
                    area / 1000f * DensityPer1000SquareMeters),
                1,
                256);
        }

        public void ConfigureVisualReferenceIfEmpty(GameObject configuredVisual)
        {
            visualPrefab ??= configuredVisual;
        }

        internal void Sanitize()
        {
            fixedCount = FixedCount;
            densityPer1000SquareMeters = DensityPer1000SquareMeters;
            minimumSpacing = MinimumSpacing;
            starterCount = StarterCount;
            starterRadius = StarterRadius;
            starterReachRadius = StarterReachRadius;
            groundOffset = GroundOffset;
            randomTiltDegrees = RandomTiltDegrees;
        }
    }

    [CreateAssetMenu(
        fileName = "ProceduralWorldSettings",
        menuName = "Midnight Chaos/Procedural/World Settings")]
    public sealed class ProceduralWorldSettings : ScriptableObject
    {
        [Header("Determinism")]
        [Tooltip("Tăng số này khi thuật toán generate thay đổi không tương thích với layout cũ.")]
        [SerializeField, Min(1)] private int generatorVersion = 4;
        [Tooltip("0 để Host chọn seed mới khi bắt đầu. Giá trị khác 0 giúp tái tạo map cố định khi test.")]
        [SerializeField] private int initialSeed = 12345;

        [Header("Terrain")]
        [Tooltip("Kích thước mặt đất theo trục X và Z, tính bằng world unit.")]
        [SerializeField] private Vector2 mapSize = new Vector2(160f, 160f);
        [Tooltip("Số ô lưới mỗi cạnh. Giá trị cao cho địa hình mịn hơn nhưng tăng thời gian mesh và NavMesh build.")]
        [SerializeField, Range(8, 200)] private int terrainSegments = 64;
        [Tooltip("Cao độ trung tâm của địa hình trước khi cộng noise và edge falloff.")]
        [SerializeField] private float baseHeight = 2f;
        [Tooltip("Biên độ cao thấp tối đa do noise tạo ra.")]
        [SerializeField, Min(0f)] private float heightAmplitude = 6f;
        [Tooltip("Tần số noise trong world space. Giá trị nhỏ tạo đồi rộng hơn.")]
        [SerializeField, Min(0.0001f)] private float noiseScale = 0.035f;
        [Tooltip("Tỷ lệ từ tâm map bắt đầu hạ thấp mép đảo.")]
        [SerializeField, Range(0.4f, 0.98f)] private float edgeFalloffStart = 0.78f;
        [Tooltip("Độ sâu hạ xuống tại mép ngoài cùng của map.")]
        [SerializeField, Min(0f)] private float edgeDrop = 3f;
        [Tooltip("Nâng hoặc hạ đồng đều toàn bộ vành mép. Cao độ mép cuối cùng = Base Height - Edge Drop + Edge Height Offset.")]
        [SerializeField] private float edgeHeightOffset;
        [Tooltip("Material dùng cho procedural terrain. Bỏ trống sẽ dùng material fallback runtime.")]
        [SerializeField] private Material groundMaterial;

        [Header("Placement")]
        [Tooltip("Khoảng trống không spawn tính từ mép map vào trong.")]
        [SerializeField, Min(0f)] private float edgePadding = 6f;
        [Tooltip("Độ dốc lớn nhất cho phép đặt object hoặc spawn point.")]
        [SerializeField, Range(0f, 80f)] private float maximumSlopeDegrees = 34f;
        [Tooltip("Số lần thử tối đa cho mỗi object trước khi bỏ qua vì hết vị trí hợp lệ.")]
        [SerializeField, Range(4, 200)] private int attemptsPerObject = 40;
        [Tooltip("Cấu hình cây. Prefab nên có child tên BottomPoint và NavMeshObstacle nếu dùng Dynamic Carving.")]
        [SerializeField] private ProceduralCategorySettings trees = new ProceduralCategorySettings();
        [Tooltip("Cấu hình đá. Nên dùng Align To Surface Normal, BottomPoint và Dynamic Carving.")]
        [SerializeField] private ProceduralCategorySettings rocks = new ProceduralCategorySettings();
        [Tooltip("Cấu hình quặng. Nên dùng Align To Surface Normal, BottomPoint và Dynamic Carving.")]
        [SerializeField] private ProceduralCategorySettings ores = new ProceduralCategorySettings();
        [Tooltip("Cấu hình vegetation nhỏ. BottomPoint không bắt buộc; hệ thống có fallback theo renderer bounds.")]
        [SerializeField] private ProceduralCategorySettings vegetation = new ProceduralCategorySettings();
        [Tooltip("Cấu hình Grass trang trí GPU-instanced. Uniform Scale thay đổi đồng thời chiều cao, chiều rộng và chiều sâu; đây chưa phải height-only scaling.")]
        [SerializeField] private ProceduralCategorySettings grass =
            new ProceduralCategorySettings();
        [Tooltip("Cấu hình deterministic cluster cho Grass. Không dùng UnityEngine.Random global.")]
        [SerializeField] private ProceduralGrassClusterSettings grassClusters =
            new ProceduralGrassClusterSettings();
        [Tooltip("Cấu hình phân bố deterministic của Small Rock world pickup.")]
        [SerializeField] private ProceduralSmallRockSettings smallRocks =
            new ProceduralSmallRockSettings();

        [Header("Spawn Point Layout")]
        [Tooltip("Số điểm spawn player được reserve để kiểm tra an toàn; scene demo không tự spawn player.")]
        [SerializeField, Range(0, 32)] private int playerSpawnPointCount = 8;
        [Tooltip("Số group center deterministic có thể dùng để đặt gameplay enemy group. Mỗi center có thể chứa cả group; đây không phải số enemy trong group.")]
        [SerializeField, Range(1, 64)] private int enemySpawnPointCount = 12;
        [Tooltip("Bán kính không cho environment chồng vào điểm spawn player.")]
        [SerializeField, Min(0.5f)] private float playerSpawnClearance = 4f;
        [Tooltip("Bán kính tối đa gom mọi player spawn point quanh điểm đầu tiên (Host). Giá trị thực tế không nhỏ hơn hai lần Player Spawn Clearance.")]
        [SerializeField, Min(1f)] private float playerSpawnGroupRadius = 24f;
        [Tooltip("Bán kính không cho environment hoặc enemy khác chồng vào điểm spawn enemy.")]
        [SerializeField, Min(0.5f)] private float enemySpawnClearance = 3f;
        [Tooltip("Khoảng cách tối thiểu giữa enemy spawn point và mọi player spawn point.")]
        [SerializeField, Min(0f)] private float enemyDistanceFromPlayerSpawns = 18f;
        public int GeneratorVersion => Mathf.Max(1, generatorVersion);
        public int InitialSeed => initialSeed;
        public Vector2 MapSize => new Vector2(
            Mathf.Max(20f, mapSize.x),
            Mathf.Max(20f, mapSize.y));
        public int TerrainSegments => Mathf.Clamp(terrainSegments, 8, 200);
        public float BaseHeight => baseHeight;
        public float HeightAmplitude => Mathf.Max(0f, heightAmplitude);
        public float NoiseScale => Mathf.Max(0.0001f, noiseScale);
        public float EdgeFalloffStart => Mathf.Clamp(edgeFalloffStart, 0.4f, 0.98f);
        public float EdgeDrop => Mathf.Max(0f, edgeDrop);
        public float EdgeHeightOffset => edgeHeightOffset;
        public float UniformEdgeHeight => BaseHeight - EdgeDrop + EdgeHeightOffset;
        public Material GroundMaterial => groundMaterial;
        public float EdgePadding => Mathf.Clamp(
            edgePadding,
            0f,
            Mathf.Min(MapSize.x, MapSize.y) * 0.45f);
        public float MaximumSlopeDegrees => Mathf.Clamp(maximumSlopeDegrees, 0f, 80f);
        public int AttemptsPerObject => Mathf.Clamp(attemptsPerObject, 4, 200);
        public ProceduralCategorySettings Trees => trees;
        public ProceduralCategorySettings Rocks => rocks;
        public ProceduralCategorySettings Ores => ores;
        public ProceduralCategorySettings Vegetation => vegetation;
        public ProceduralCategorySettings Grass => grass;
        public ProceduralGrassClusterSettings GrassClusters => grassClusters;
        public ProceduralSmallRockSettings SmallRocks =>
            smallRocks ??= new ProceduralSmallRockSettings();
        public int PlayerSpawnPointCount => Mathf.Clamp(playerSpawnPointCount, 0, 32);
        public int EnemySpawnPointCount => Mathf.Clamp(enemySpawnPointCount, 1, 64);
        public float PlayerSpawnClearance => Mathf.Max(0.5f, playerSpawnClearance);
        public float PlayerSpawnGroupRadius => Mathf.Max(
            PlayerSpawnClearance * 2f,
            playerSpawnGroupRadius);
        public float EnemySpawnClearance => Mathf.Max(0.5f, enemySpawnClearance);
        public float EnemyDistanceFromPlayerSpawns => Mathf.Max(0f, enemyDistanceFromPlayerSpawns);
        public int ConfiguredEnvironmentCount =>
            Trees.Count + Rocks.Count + Ores.Count + Vegetation.Count +
            Grass.Count;

        public void ConfigureDefinitionReferencesIfEmpty(
            WorldObjectDefinition[] treeDefinitions,
            WorldObjectDefinition[] rockDefinitions,
            WorldObjectDefinition[] oreDefinitions,
            WorldObjectDefinition[] vegetationDefinitions,
            WorldObjectDefinition[] grassDefinitions,
            Material configuredGroundMaterial)
        {
            trees ??= new ProceduralCategorySettings();
            rocks ??= new ProceduralCategorySettings();
            ores ??= new ProceduralCategorySettings();
            vegetation ??= new ProceduralCategorySettings();
            grass ??= new ProceduralCategorySettings();

            trees.ConfigureDefinitionReferencesIfEmpty(treeDefinitions);
            rocks.ConfigureDefinitionReferencesIfEmpty(rockDefinitions);
            ores.ConfigureDefinitionReferencesIfEmpty(oreDefinitions);
            vegetation.ConfigureDefinitionReferencesIfEmpty(
                vegetationDefinitions);
            grass.ConfigureDefinitionReferencesIfEmpty(grassDefinitions);
            groundMaterial ??= configuredGroundMaterial;
        }

        public void ConfigureDefinitionReferencesIfEmpty(
            WorldObjectDefinition[] treeDefinitions,
            WorldObjectDefinition[] rockDefinitions,
            WorldObjectDefinition[] oreDefinitions,
            WorldObjectDefinition[] vegetationDefinitions,
            Material configuredGroundMaterial)
        {
            ConfigureDefinitionReferencesIfEmpty(
                treeDefinitions,
                rockDefinitions,
                oreDefinitions,
                vegetationDefinitions,
                Array.Empty<WorldObjectDefinition>(),
                configuredGroundMaterial);
        }

        public void ValidateDefinitionsOrThrow()
        {
            List<string> errors = new List<string>();
            Dictionary<string, WorldObjectDefinition> definitionsById =
                new Dictionary<string, WorldObjectDefinition>(
                    StringComparer.Ordinal);

            ValidateCategoryDefinitions(
                trees,
                WorldObjectCategory.Tree,
                nameof(Trees),
                definitionsById,
                errors);
            ValidateCategoryDefinitions(
                rocks,
                WorldObjectCategory.Rock,
                nameof(Rocks),
                definitionsById,
                errors);
            ValidateCategoryDefinitions(
                ores,
                WorldObjectCategory.Ore,
                nameof(Ores),
                definitionsById,
                errors);
            ValidateCategoryDefinitions(
                vegetation,
                WorldObjectCategory.Vegetation,
                nameof(Vegetation),
                definitionsById,
                errors);
            ValidateCategoryDefinitions(
                grass,
                WorldObjectCategory.Grass,
                nameof(Grass),
                definitionsById,
                errors);
            if (grassClusters == null)
            {
                errors.Add("Grass cluster settings are null.");
            }
            else
            {
                grassClusters.Validate(errors);
            }

            if (grass == null)
            {
                errors.Add("Grass settings are null.");
            }
            else if (grass.NavigationMode != ProceduralNavigationMode.None)
            {
                errors.Add("Grass NavigationMode must be None.");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Invalid procedural world metadata:\n- " +
                    string.Join("\n- ", errors));
            }
        }

        private static void ValidateCategoryDefinitions(
            ProceduralCategorySettings categorySettings,
            WorldObjectCategory expectedCategory,
            string label,
            Dictionary<string, WorldObjectDefinition> definitionsById,
            List<string> errors)
        {
            if (categorySettings == null)
            {
                errors.Add($"{label} settings are null.");
                return;
            }

            WorldObjectDefinition[] definitions = categorySettings.Definitions;
            if (categorySettings.Count > 0 && definitions.Length == 0)
            {
                errors.Add($"{label} has Count > 0 but no definitions.");
            }

            for (int index = 0; index < definitions.Length; index++)
            {
                WorldObjectDefinition definition = definitions[index];
                if (definition == null)
                {
                    errors.Add($"{label}[{index}] is null.");
                    continue;
                }

                if (!definition.TryValidate(out string definitionError))
                {
                    errors.Add($"{label}[{index}]: {definitionError}");
                    continue;
                }

                if (definition.Category != expectedCategory)
                {
                    errors.Add(
                        $"{label}[{index}] '{definition.StableId}' has " +
                        $"category {definition.Category}, expected " +
                        $"{expectedCategory}.");
                }

                if (expectedCategory == WorldObjectCategory.Grass)
                {
                    ValidateGrassDefinition(definition, label, index, errors);
                }

                bool blocksNavMesh = definition.HasFlag(
                    WorldObjectFlags.BlocksNavMesh);
                bool navigationEnabled = categorySettings.NavigationMode !=
                                         ProceduralNavigationMode.None;
                if (blocksNavMesh != navigationEnabled)
                {
                    errors.Add(
                        $"{label}[{index}] '{definition.StableId}' " +
                        $"BlocksNavMesh={blocksNavMesh}, but category " +
                        $"NavigationMode is {categorySettings.NavigationMode}.");
                }

                if (definitionsById.TryGetValue(
                        definition.StableId,
                        out WorldObjectDefinition previous) &&
                    previous != definition)
                {
                    errors.Add(
                        $"Stable ID '{definition.StableId}' is duplicated by " +
                        $"definitions '{previous.name}' and '{definition.name}'.");
                }
                else
                {
                    definitionsById[definition.StableId] = definition;
                }
            }
        }

        public IReadOnlyList<string> CollectDefinitionWarnings()
        {
            List<string> warnings = new List<string>();
            if (grass == null)
            {
                return warnings;
            }

            HashSet<GameObject> visited = new HashSet<GameObject>();
            foreach (WorldObjectDefinition definition in grass.Definitions)
            {
                GameObject prefab = definition == null ? null : definition.Prefab;
                if (prefab == null || !visited.Add(prefab))
                {
                    continue;
                }

                if (prefab.GetComponentInChildren<Collider>(true) != null)
                {
                    warnings.Add(
                        $"Grass source prefab '{prefab.name}' contains Collider; GPU instances ignore it.");
                }
                if (prefab.GetComponentInChildren<Rigidbody>(true) != null)
                {
                    warnings.Add(
                        $"Grass source prefab '{prefab.name}' contains Rigidbody; GPU instances ignore it.");
                }
                if (prefab.GetComponentInChildren<Unity.Netcode.NetworkObject>(
                        true) != null)
                {
                    warnings.Add(
                        $"Grass source prefab '{prefab.name}' contains NetworkObject; GPU instances ignore it.");
                }
                MonoBehaviour[] behaviours =
                    prefab.GetComponentsInChildren<MonoBehaviour>(true);
                if (behaviours.Length > 0)
                {
                    warnings.Add(
                        $"Grass source prefab '{prefab.name}' contains {behaviours.Length} MonoBehaviour component(s) unnecessary for GPU instances.");
                }
            }
            return warnings;
        }

        private static void ValidateGrassDefinition(
            WorldObjectDefinition definition,
            string label,
            int index,
            List<string> errors)
        {
            WorldObjectFlags forbidden =
                WorldObjectFlags.Interactive |
                WorldObjectFlags.BlocksNavMesh |
                WorldObjectFlags.Networked;
            if (!definition.HasFlag(WorldObjectFlags.Decorative) ||
                (definition.Flags & forbidden) != 0)
            {
                errors.Add(
                    $"{label}[{index}] '{definition.StableId}' must be Decorative only and cannot be Interactive, BlocksNavMesh, or Networked.");
            }

            GameObject prefab = definition.Prefab;
            if (prefab == null)
            {
                return;
            }

            MeshRenderer[] renderers =
                prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                errors.Add(
                    $"{label}[{index}] '{definition.StableId}' has no MeshRenderer.");
                return;
            }

            foreach (MeshRenderer renderer in renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    errors.Add(
                        $"{label}[{index}] '{definition.StableId}' has a renderer without a render mesh.");
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    errors.Add(
                        $"{label}[{index}] '{definition.StableId}' has no render material.");
                    continue;
                }
                foreach (Material material in materials)
                {
                    if (material == null)
                    {
                        errors.Add(
                            $"{label}[{index}] '{definition.StableId}' has a null render material.");
                    }
                    else if (!material.enableInstancing)
                    {
                        errors.Add(
                            $"{label}[{index}] '{definition.StableId}' material '{material.name}' does not enable GPU instancing.");
                    }
                }
            }
        }

        private void OnValidate()
        {
            generatorVersion = Mathf.Max(1, generatorVersion);
            mapSize.x = Mathf.Max(20f, mapSize.x);
            mapSize.y = Mathf.Max(20f, mapSize.y);
            terrainSegments = Mathf.Clamp(terrainSegments, 8, 200);
            heightAmplitude = Mathf.Max(0f, heightAmplitude);
            noiseScale = Mathf.Max(0.0001f, noiseScale);
            edgeFalloffStart = Mathf.Clamp(edgeFalloffStart, 0.4f, 0.98f);
            edgeDrop = Mathf.Max(0f, edgeDrop);
            edgePadding = EdgePadding;
            maximumSlopeDegrees = MaximumSlopeDegrees;
            attemptsPerObject = AttemptsPerObject;
            playerSpawnPointCount = PlayerSpawnPointCount;
            enemySpawnPointCount = EnemySpawnPointCount;
            playerSpawnClearance = PlayerSpawnClearance;
            playerSpawnGroupRadius = PlayerSpawnGroupRadius;
            enemySpawnClearance = EnemySpawnClearance;
            enemyDistanceFromPlayerSpawns = EnemyDistanceFromPlayerSpawns;

            trees ??= new ProceduralCategorySettings();
            rocks ??= new ProceduralCategorySettings();
            ores ??= new ProceduralCategorySettings();
            vegetation ??= new ProceduralCategorySettings();
            grass ??= new ProceduralCategorySettings();
            grassClusters ??= new ProceduralGrassClusterSettings();
            smallRocks ??= new ProceduralSmallRockSettings();
            trees.Sanitize();
            rocks.Sanitize();
            ores.Sanitize();
            vegetation.Sanitize();
            grass.Sanitize();
            grassClusters.Sanitize();
            smallRocks.Sanitize();
        }
    }
}
