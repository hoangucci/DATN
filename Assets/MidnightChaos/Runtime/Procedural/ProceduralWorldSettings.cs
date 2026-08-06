using System;
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
        [Tooltip("Danh sách prefab được chọn bằng cùng một seeded random trên Host và Client. Thứ tự phần tử là một phần của kết quả deterministic.")]
        [SerializeField] private GameObject[] prefabs = Array.Empty<GameObject>();
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
        [Tooltip("0 giữ nguyên ngưỡng culling của prefab. Giá trị nhỏ như 0.001 giúp vegetation nhỏ vẫn hiện từ camera demo.")]
        [SerializeField, Range(0f, 0.2f)]
        private float lodCullScreenHeightOverride;

        public GameObject[] Prefabs => prefabs ?? Array.Empty<GameObject>();
        public int Count => Mathf.Max(0, count);
        public float ClearanceRadius => Mathf.Max(0.1f, clearanceRadius);
        public Vector2 UniformScaleRange => new Vector2(
            Mathf.Max(0.05f, Mathf.Min(uniformScaleRange.x, uniformScaleRange.y)),
            Mathf.Max(0.05f, Mathf.Max(uniformScaleRange.x, uniformScaleRange.y)));
        public float RandomTiltDegrees => Mathf.Clamp(randomTiltDegrees, 0f, 30f);
        public ProceduralSurfaceAlignment SurfaceAlignment => surfaceAlignment;
        public ProceduralNavigationMode NavigationMode => navigationMode;
        public float LodCullScreenHeightOverride =>
            Mathf.Clamp(lodCullScreenHeightOverride, 0f, 0.2f);

        public void ConfigureAssetReferencesIfEmpty(GameObject[] configuredPrefabs)
        {
            if ((prefabs == null || prefabs.Length == 0) && configuredPrefabs != null)
            {
                prefabs = configuredPrefabs;
            }
        }

        internal void Sanitize()
        {
            count = Mathf.Max(0, count);
            clearanceRadius = Mathf.Max(0.1f, clearanceRadius);
            uniformScaleRange = UniformScaleRange;
            randomTiltDegrees = Mathf.Clamp(randomTiltDegrees, 0f, 30f);
            lodCullScreenHeightOverride = LodCullScreenHeightOverride;
            prefabs ??= Array.Empty<GameObject>();
        }
    }

    [CreateAssetMenu(
        fileName = "ProceduralWorldSettings",
        menuName = "Midnight Chaos/Procedural/World Settings")]
    public sealed class ProceduralWorldSettings : ScriptableObject
    {
        [Header("Determinism")]
        [Tooltip("Tăng số này khi thuật toán generate thay đổi không tương thích với layout cũ.")]
        [SerializeField, Min(1)] private int generatorVersion = 3;
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

        [Header("Spawn Points Only - No Player Is Spawned In This Demo")]
        [Tooltip("Số điểm spawn player được reserve để kiểm tra an toàn; scene demo không tự spawn player.")]
        [SerializeField, Range(0, 32)] private int playerSpawnPointCount = 8;
        [Tooltip("Số điểm có thể chọn khi nhấn nút Spawn Enemy; đây không phải số enemy tự spawn.")]
        [SerializeField, Range(1, 64)] private int enemySpawnPointCount = 12;
        [Tooltip("Bán kính không cho environment chồng vào điểm spawn player.")]
        [SerializeField, Min(0.5f)] private float playerSpawnClearance = 4f;
        [Tooltip("Bán kính không cho environment hoặc enemy khác chồng vào điểm spawn enemy.")]
        [SerializeField, Min(0.5f)] private float enemySpawnClearance = 3f;
        [Tooltip("Khoảng cách tối thiểu giữa enemy spawn point và mọi player spawn point.")]
        [SerializeField, Min(0f)] private float enemyDistanceFromPlayerSpawns = 18f;
        [Tooltip("Bán kính tìm polygon NavMesh gần spawn point khi spawn enemy thủ công.")]
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 2.5f;
        [Tooltip("Hiện gizmo/marker của các điểm spawn trong scene demo.")]
        [SerializeField] private bool showSpawnMarkers = true;
        [Tooltip("Kích thước marker debug của spawn point.")]
        [SerializeField, Min(0.05f)] private float spawnMarkerScale = 0.65f;

        [Header("Rendering Performance (Local Only)")]
        [Tooltip("Dùng Camera.layerCullDistances cho các layer procedural. Chỉ thay đổi hiển thị local, không thay đổi layout, physics hoặc Layout Hash.")]
        [SerializeField] private bool useLayerDistanceCulling = true;
        [Tooltip("Far Clip Plane áp dụng cho camera procedural. Với map lớn phải giữ đủ để terrain không bị cắt.")]
        [SerializeField, Min(50f)] private float cameraFarClipPlane = 1000f;
        [Tooltip("Khoảng render tối đa của grass/flower.")]
        [SerializeField, Min(1f)] private float vegetationCullDistance = 55f;
        [Tooltip("Khoảng đổi từ LOD0 sang LOD thấp của vegetation instanced.")]
        [SerializeField, Min(1f)] private float vegetationLodSwitchDistance = 28f;
        [Tooltip("Khoảng render tối đa của cây.")]
        [SerializeField, Min(1f)] private float treeCullDistance = 200f;
        [Tooltip("Khoảng render tối đa cho prop nhỏ. Hiện để sẵn cho category tương lai.")]
        [SerializeField, Min(1f)] private float smallPropCullDistance = 90f;
        [Tooltip("Khoảng render tối đa của đá và quặng tương tác.")]
        [SerializeField, Min(1f)] private float resourceCullDistance = 130f;
        [Tooltip("Render vegetation bằng GPU instancing theo chunk, không tạo một GameObject cho mỗi cây cỏ/hoa.")]
        [SerializeField] private bool useInstancedVegetation = true;
        [Tooltip("Kích thước ô dùng để distance-cull vegetation theo nhóm. Giá trị nhỏ giảm overdraw nhưng tăng số draw group.")]
        [SerializeField, Range(8f, 64f)] private float vegetationChunkSize = 24f;
        [Tooltip("Tắt cast/receive shadow trên vegetation trang trí.")]
        [SerializeField] private bool disableVegetationShadows = true;
        [Tooltip("Bật ParticleSystem lá trên từng tree prefab. Tắt mặc định vì 2.000 cây tương đương 2.000 particle simulations.")]
        [SerializeField] private bool enableTreeParticles;

        [Header("Runtime NavMesh")]
        [Tooltip("Agent Type ID dùng để build NavMeshSurface. Phải trùng Agent Type ID trên mọi enemy prefab được spawn.")]
        [SerializeField] private int navMeshAgentTypeId;
        [Tooltip("Chiều cao vùng thu thập source của runtime NavMesh, đặt đủ để bao toàn bộ địa hình.")]
        [SerializeField, Min(5f)] private float navMeshVolumeHeight = 40f;
        [Tooltip("Thời gian chờ sau khi build để NavMeshObstacle carving ổn định trước khi validate spawn point. Nên lớn hơn Time To Stationary lớn nhất trên obstacle prefab.")]
        [SerializeField, Range(0f, 2f)]
        private float navMeshCarvingSettleSeconds = 0.65f;

        [Header("Manual Enemy Spawn")]
        [Tooltip("Prefab enemy spawn khi Host nhấn nút. Prefab phải có NetworkObject và NavMeshAgent đã cấu hình đầy đủ.")]
        [SerializeField] private GameObject enemyPrefab;
        [Tooltip("Giới hạn enemy đang tồn tại do nút Spawn Enemy của demo tạo ra.")]
        [SerializeField, Range(1, 32)] private int maximumActiveEnemies = 5;

        [Header("LAN")]
        [Tooltip("Cổng UDP mặc định cho Host/Client LAN trong scene demo.")]
        [SerializeField] private ushort defaultPort = 7777;

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
        public int PlayerSpawnPointCount => Mathf.Clamp(playerSpawnPointCount, 0, 32);
        public int EnemySpawnPointCount => Mathf.Clamp(enemySpawnPointCount, 1, 64);
        public float PlayerSpawnClearance => Mathf.Max(0.5f, playerSpawnClearance);
        public float EnemySpawnClearance => Mathf.Max(0.5f, enemySpawnClearance);
        public float EnemyDistanceFromPlayerSpawns => Mathf.Max(0f, enemyDistanceFromPlayerSpawns);
        public float NavMeshSampleRadius => Mathf.Max(0.1f, navMeshSampleRadius);
        public bool ShowSpawnMarkers => showSpawnMarkers;
        public float SpawnMarkerScale => Mathf.Max(0.05f, spawnMarkerScale);
        public bool UseLayerDistanceCulling => useLayerDistanceCulling;
        public float CameraFarClipPlane => Mathf.Max(50f, cameraFarClipPlane);
        public float VegetationCullDistance => Mathf.Max(1f, vegetationCullDistance);
        public float VegetationLodSwitchDistance => Mathf.Clamp(
            vegetationLodSwitchDistance,
            1f,
            VegetationCullDistance);
        public float TreeCullDistance => Mathf.Max(1f, treeCullDistance);
        public float SmallPropCullDistance => Mathf.Max(1f, smallPropCullDistance);
        public float ResourceCullDistance => Mathf.Max(1f, resourceCullDistance);
        public bool UseInstancedVegetation => useInstancedVegetation;
        public float VegetationChunkSize => Mathf.Clamp(
            vegetationChunkSize,
            8f,
            64f);
        public bool DisableVegetationShadows => disableVegetationShadows;
        public bool EnableTreeParticles => enableTreeParticles;
        public int NavMeshAgentTypeId => navMeshAgentTypeId;
        public float NavMeshVolumeHeight => Mathf.Max(5f, navMeshVolumeHeight);
        public float NavMeshCarvingSettleSeconds =>
            Mathf.Clamp(navMeshCarvingSettleSeconds, 0f, 2f);
        public GameObject EnemyPrefab => enemyPrefab;
        public int MaximumActiveEnemies => Mathf.Clamp(maximumActiveEnemies, 1, 32);
        public ushort DefaultPort => defaultPort == 0 ? (ushort)7777 : defaultPort;
        public int ConfiguredEnvironmentCount =>
            Trees.Count + Rocks.Count + Ores.Count + Vegetation.Count;

        public void ConfigureAssetReferencesIfEmpty(
            GameObject[] treePrefabs,
            GameObject[] rockPrefabs,
            GameObject[] orePrefabs,
            GameObject[] vegetationPrefabs,
            Material configuredGroundMaterial,
            GameObject configuredEnemyPrefab)
        {
            trees ??= new ProceduralCategorySettings();
            rocks ??= new ProceduralCategorySettings();
            ores ??= new ProceduralCategorySettings();
            vegetation ??= new ProceduralCategorySettings();

            trees.ConfigureAssetReferencesIfEmpty(treePrefabs);
            rocks.ConfigureAssetReferencesIfEmpty(rockPrefabs);
            ores.ConfigureAssetReferencesIfEmpty(orePrefabs);
            vegetation.ConfigureAssetReferencesIfEmpty(vegetationPrefabs);
            groundMaterial ??= configuredGroundMaterial;
            enemyPrefab ??= configuredEnemyPrefab;
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
            enemySpawnClearance = EnemySpawnClearance;
            enemyDistanceFromPlayerSpawns = EnemyDistanceFromPlayerSpawns;
            navMeshSampleRadius = NavMeshSampleRadius;
            spawnMarkerScale = SpawnMarkerScale;
            cameraFarClipPlane = CameraFarClipPlane;
            vegetationCullDistance = VegetationCullDistance;
            vegetationLodSwitchDistance = VegetationLodSwitchDistance;
            treeCullDistance = TreeCullDistance;
            smallPropCullDistance = SmallPropCullDistance;
            resourceCullDistance = ResourceCullDistance;
            vegetationChunkSize = VegetationChunkSize;
            navMeshVolumeHeight = NavMeshVolumeHeight;
            navMeshCarvingSettleSeconds = NavMeshCarvingSettleSeconds;
            maximumActiveEnemies = MaximumActiveEnemies;
            if (defaultPort == 0)
            {
                defaultPort = 7777;
            }

            trees ??= new ProceduralCategorySettings();
            rocks ??= new ProceduralCategorySettings();
            ores ??= new ProceduralCategorySettings();
            vegetation ??= new ProceduralCategorySettings();
            trees.Sanitize();
            rocks.Sanitize();
            ores.Sanitize();
            vegetation.Sanitize();
        }
    }
}
