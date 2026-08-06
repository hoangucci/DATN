using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralWorldGenerator : MonoBehaviour
    {
        private Transform generatedRoot;
        private Material fallbackGroundMaterial;
        private Mesh terrainMesh;
        private ProceduralVegetationRenderer vegetationRenderer;
        private readonly HashSet<int> missingAnchorWarnings = new HashSet<int>();

        public ProceduralWorldLayout CurrentLayout { get; private set; }
        public int GeneratedObjectCount { get; private set; }
        public int GeneratedTreeCount { get; private set; }
        public int GeneratedRockCount { get; private set; }
        public int GeneratedOreCount { get; private set; }
        public int GeneratedVegetationCount { get; private set; }
        public int GeneratedVegetationGameObjectCount { get; private set; }
        public int InstancedVegetationCount =>
            vegetationRenderer != null
                ? vegetationRenderer.LogicalInstanceCount
                : 0;
        public int VegetationChunkCount =>
            vegetationRenderer != null ? vegetationRenderer.ChunkCount : 0;
        public int VegetationDrawBatchCount =>
            vegetationRenderer != null ? vegetationRenderer.DrawBatchCount : 0;
        public int VisibleVegetationChunkCount =>
            vegetationRenderer != null
                ? vegetationRenderer.VisibleChunkCount
                : 0;
        public int SubmittedVegetationDrawCount =>
            vegetationRenderer != null
                ? vegetationRenderer.SubmittedDrawCount
                : 0;
        public Transform GeneratedRoot => generatedRoot;

        public ProceduralWorldLayout Generate(
            ProceduralWorldSettings settings,
            int seed,
            uint revision)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            ValidateDynamicObstacleContracts(settings);
            ClearGeneratedContent();
            CurrentLayout = ProceduralWorldLayoutBuilder.Build(settings, seed);

            GameObject rootObject = new GameObject(
                $"GeneratedWorld_R{revision}_S{seed}");
            rootObject.transform.SetParent(transform, false);
            generatedRoot = rootObject.transform;

            vegetationRenderer = null;
            if (settings.UseInstancedVegetation)
            {
                vegetationRenderer =
                    rootObject.AddComponent<ProceduralVegetationRenderer>();
                vegetationRenderer.Initialize(settings);
            }

            CreateTerrain(CurrentLayout, settings, generatedRoot);
            GeneratedObjectCount = 1;

            foreach (ProceduralObjectPlacement placement in CurrentLayout.Objects)
            {
                GameObject prefab = ResolvePrefab(settings, placement);
                if (prefab == null)
                {
                    continue;
                }

                ProceduralCategorySettings categorySettings =
                    ResolveCategorySettings(settings, placement.Category);
                if (placement.Category ==
                        ProceduralObjectCategory.Vegetation &&
                    vegetationRenderer != null &&
                    vegetationRenderer.TryAddPlacement(
                        prefab,
                        placement,
                        categorySettings))
                {
                    GeneratedObjectCount++;
                    IncrementCategoryCount(placement.Category);
                    continue;
                }

                GameObject instance = Instantiate(prefab, generatedRoot);
                if (placement.Category ==
                    ProceduralObjectCategory.Vegetation)
                {
                    GeneratedVegetationGameObjectCount++;
                }
                // Nature bundle prefabs are authored with all static flags set.
                // Clear inherited flags before any runtime transform changes.
                SetStaticRecursively(instance, false);
                instance.name =
                    $"{placement.Category}_{placement.PrefabIndex}_{GeneratedObjectCount:000}";
                instance.transform.localScale =
                    prefab.transform.localScale * placement.UniformScale;
                PlaceInstance(
                    instance,
                    prefab,
                    placement,
                    categorySettings);
                ApplyLodCullOverride(
                    instance,
                    categorySettings.LodCullScreenHeightOverride);
                ConfigureRendering(
                    instance,
                    placement.Category,
                    settings);
                ConfigureNavigation(instance, prefab, placement.NavigationMode);

                GeneratedObjectCount++;
                IncrementCategoryCount(placement.Category);
            }

            vegetationRenderer?.Complete();

            foreach (string warning in CurrentLayout.Warnings)
            {
                Debug.LogWarning($"[Procedural] {warning}", this);
            }

            return CurrentLayout;
        }

        public void ClearGeneratedContent()
        {
            CurrentLayout = null;
            GeneratedObjectCount = 0;
            GeneratedTreeCount = 0;
            GeneratedRockCount = 0;
            GeneratedOreCount = 0;
            GeneratedVegetationCount = 0;
            GeneratedVegetationGameObjectCount = 0;
            vegetationRenderer = null;
            missingAnchorWarnings.Clear();

            Mesh oldTerrainMesh = terrainMesh;
            terrainMesh = null;

            if (generatedRoot == null)
            {
                DestroyRuntimeObject(oldTerrainMesh);
                return;
            }

            GameObject oldRoot = generatedRoot.gameObject;
            generatedRoot = null;

            // Disable immediately so delayed runtime destruction cannot leak
            // old renderers or colliders into the next NavMesh build.
            oldRoot.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(oldRoot);
            }
            else
            {
                DestroyImmediate(oldRoot);
            }

            DestroyRuntimeObject(oldTerrainMesh);
        }

        private void CreateTerrain(
            ProceduralWorldLayout layout,
            ProceduralWorldSettings settings,
            Transform parent)
        {
            GameObject ground = new GameObject(
                "ProceduralTerrain",
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(MeshCollider));
            ground.transform.SetParent(parent, false);

            int segments = layout.TerrainSegments;
            int side = segments + 1;
            Vector2 mapSize = layout.MapSize;
            Vector3[] vertices = new Vector3[side * side];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * segments * 6];

            for (int z = 0; z <= segments; z++)
            {
                float normalizedZ = (float)z / segments;
                for (int x = 0; x <= segments; x++)
                {
                    float normalizedX = (float)x / segments;
                    int index = z * side + x;
                    vertices[index] = new Vector3(
                        (normalizedX - 0.5f) * mapSize.x,
                        layout.TerrainHeights[index],
                        (normalizedZ - 0.5f) * mapSize.y);
                    uvs[index] = new Vector2(
                        normalizedX * mapSize.x / 12f,
                        normalizedZ * mapSize.y / 12f);
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < segments; z++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int current = z * side + x;
                    int nextRow = current + side;
                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = nextRow;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = nextRow;
                    triangles[triangleIndex++] = nextRow + 1;
                }
            }

            Mesh mesh = new Mesh
            {
                name = $"ProceduralTerrain_{layout.Seed}"
            };
            terrainMesh = mesh;
            if (vertices.Length > ushort.MaxValue)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            ground.GetComponent<MeshCollider>().sharedMesh = mesh;
            ground.GetComponent<MeshRenderer>().sharedMaterial =
                settings.GroundMaterial != null
                    ? settings.GroundMaterial
                    : GetFallbackGroundMaterial();
        }

        private Material GetFallbackGroundMaterial()
        {
            if (fallbackGroundMaterial != null)
            {
                return fallbackGroundMaterial;
            }

            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            fallbackGroundMaterial = new Material(shader)
            {
                name = "ProceduralFallbackGround",
                color = new Color(0.22f, 0.55f, 0.24f)
            };
            return fallbackGroundMaterial;
        }

        private static GameObject ResolvePrefab(
            ProceduralWorldSettings settings,
            ProceduralObjectPlacement placement)
        {
            GameObject[] prefabs = placement.Category switch
            {
                ProceduralObjectCategory.Tree => settings.Trees.Prefabs,
                ProceduralObjectCategory.Rock => settings.Rocks.Prefabs,
                ProceduralObjectCategory.Ore => settings.Ores.Prefabs,
                ProceduralObjectCategory.Vegetation => settings.Vegetation.Prefabs,
                _ => Array.Empty<GameObject>()
            };

            return placement.PrefabIndex >= 0 &&
                   placement.PrefabIndex < prefabs.Length
                ? prefabs[placement.PrefabIndex]
                : null;
        }

        private static ProceduralCategorySettings ResolveCategorySettings(
            ProceduralWorldSettings settings,
            ProceduralObjectCategory category)
        {
            return category switch
            {
                ProceduralObjectCategory.Tree => settings.Trees,
                ProceduralObjectCategory.Rock => settings.Rocks,
                ProceduralObjectCategory.Ore => settings.Ores,
                ProceduralObjectCategory.Vegetation => settings.Vegetation,
                _ => settings.Vegetation
            };
        }

        private static void ValidateDynamicObstacleContracts(
            ProceduralWorldSettings settings)
        {
            List<string> errors = new List<string>();
            ValidateDynamicObstacleCategory(settings.Trees, "Trees", errors);
            ValidateDynamicObstacleCategory(settings.Rocks, "Rocks", errors);
            ValidateDynamicObstacleCategory(settings.Ores, "Ores", errors);
            ValidateDynamicObstacleCategory(
                settings.Vegetation,
                "Vegetation",
                errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Invalid procedural prefab contract:\n- " +
                    string.Join("\n- ", errors));
            }
        }

        private static void ValidateDynamicObstacleCategory(
            ProceduralCategorySettings category,
            string label,
            List<string> errors)
        {
            if (category.NavigationMode !=
                ProceduralNavigationMode.DynamicCarving)
            {
                return;
            }

            for (int index = 0; index < category.Prefabs.Length; index++)
            {
                GameObject prefab = category.Prefabs[index];
                if (prefab == null)
                {
                    errors.Add($"{label}[{index}] is null.");
                    continue;
                }

                NavMeshObstacle obstacle =
                    prefab.GetComponentInChildren<NavMeshObstacle>(true);
                if (obstacle == null)
                {
                    errors.Add(
                        $"{label}[{index}] '{prefab.name}' is missing an " +
                        "authored NavMeshObstacle.");
                }
                else if (!obstacle.carving)
                {
                    errors.Add(
                        $"{label}[{index}] '{prefab.name}' has Carving disabled.");
                }
            }
        }

        private static void ApplyLodCullOverride(
            GameObject instance,
            float screenHeight)
        {
            if (screenHeight <= 0f)
            {
                return;
            }

            foreach (LODGroup group in instance.GetComponentsInChildren<LODGroup>())
            {
                LOD[] levels = group.GetLODs();
                if (levels.Length == 0)
                {
                    continue;
                }

                int last = levels.Length - 1;
                levels[last].screenRelativeTransitionHeight = Mathf.Min(
                    levels[last].screenRelativeTransitionHeight,
                    screenHeight);
                group.SetLODs(levels);
                group.RecalculateBounds();
            }
        }

        private void PlaceInstance(
            GameObject instance,
            GameObject prefab,
            ProceduralObjectPlacement placement,
            ProceduralCategorySettings categorySettings)
        {
            Vector3 targetUp = categorySettings.SurfaceAlignment ==
                               ProceduralSurfaceAlignment.AlignToSurfaceNormal
                ? placement.SurfaceNormal
                : Vector3.up;
            if (targetUp.sqrMagnitude < 0.000001f)
            {
                targetUp = Vector3.up;
            }
            targetUp.Normalize();

            Quaternion targetAnchorRotation = CalculateTargetAnchorRotation(
                placement,
                targetUp);

            if (ProceduralPrefabContract.TryFindPlacementAnchor(
                    instance.transform,
                    out Transform anchor))
            {
                Quaternion rotationDelta =
                    targetAnchorRotation * Quaternion.Inverse(anchor.rotation);
                instance.transform.rotation =
                    rotationDelta * instance.transform.rotation;
                instance.transform.position +=
                    placement.Position - anchor.position;
                return;
            }

            instance.transform.SetPositionAndRotation(
                placement.Position,
                targetAnchorRotation);
            AlignBottomToGround(instance, placement.Position.y);

            int prefabId = prefab.GetInstanceID();
            if (placement.Category != ProceduralObjectCategory.Vegetation &&
                missingAnchorWarnings.Add(prefabId))
            {
                Debug.LogWarning(
                    $"[Procedural] Prefab '{prefab.name}' không có " +
                    $"{ProceduralPrefabContract.DefaultAnchorName}. " +
                    "Đang dùng renderer-bounds fallback; hãy thêm BottomPoint để " +
                    "kiểm soát offset và rotation chính xác.",
                    prefab);
            }
        }

        internal static Quaternion CalculateTargetAnchorRotation(
            ProceduralObjectPlacement placement,
            Vector3 targetUp)
        {
            Quaternion surfaceRotation =
                Quaternion.FromToRotation(Vector3.up, targetUp);
            return
                Quaternion.AngleAxis(placement.EulerAngles.y, targetUp) *
                surfaceRotation *
                Quaternion.Euler(
                    placement.EulerAngles.x,
                    0f,
                    placement.EulerAngles.z);
        }

        private void ConfigureNavigation(
            GameObject instance,
            GameObject prefab,
            ProceduralNavigationMode mode)
        {
            switch (mode)
            {
                case ProceduralNavigationMode.None:
                    DisableColliders(instance);
                    return;

                case ProceduralNavigationMode.BakeIntoNavMesh:
                    return;

                case ProceduralNavigationMode.DynamicCarving:
                    NavMeshModifier modifier =
                        instance.GetComponent<NavMeshModifier>() ??
                        instance.AddComponent<NavMeshModifier>();
                    modifier.ignoreFromBuild = true;
                    modifier.applyToChildren = true;

                    NavMeshObstacle obstacle =
                        instance.GetComponentInChildren<NavMeshObstacle>(true);
                    if (obstacle == null)
                    {
                        throw new InvalidOperationException(
                            $"Prefab '{prefab.name}' dùng DynamicCarving nhưng " +
                            "không có NavMeshObstacle authored trên prefab. " +
                            "Hãy chạy migration trong ProceduralWorldSettings " +
                            "Inspector, sau đó chỉnh obstacle theo model.");
                    }
                    if (!obstacle.carving)
                    {
                        throw new InvalidOperationException(
                            $"NavMeshObstacle trên prefab '{prefab.name}' chưa " +
                            "bật Carving.");
                    }
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private void ConfigureRendering(
            GameObject instance,
            ProceduralObjectCategory category,
            ProceduralWorldSettings settings)
        {
            int layer = ProceduralRenderUtility.ResolveCategoryLayer(
                category,
                this);
            SetLayerRecursively(instance, layer);

            if (category == ProceduralObjectCategory.Vegetation &&
                settings.DisableVegetationShadows)
            {
                foreach (Renderer renderer in
                         instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.motionVectorGenerationMode =
                        MotionVectorGenerationMode.ForceNoMotion;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
            }

            if (category == ProceduralObjectCategory.Tree &&
                !settings.EnableTreeParticles)
            {
                foreach (ParticleSystem particles in
                         instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particles.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                    ParticleSystemRenderer particleRenderer =
                        particles.GetComponent<ParticleSystemRenderer>();
                    if (particleRenderer != null)
                    {
                        particleRenderer.enabled = false;
                    }
                }
            }
        }

        private void IncrementCategoryCount(ProceduralObjectCategory category)
        {
            switch (category)
            {
                case ProceduralObjectCategory.Tree:
                    GeneratedTreeCount++;
                    break;
                case ProceduralObjectCategory.Rock:
                    GeneratedRockCount++;
                    break;
                case ProceduralObjectCategory.Ore:
                    GeneratedOreCount++;
                    break;
                case ProceduralObjectCategory.Vegetation:
                    GeneratedVegetationCount++;
                    break;
            }
        }

        private static void AlignBottomToGround(GameObject instance, float groundHeight)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            float minimumY = float.PositiveInfinity;
            foreach (Renderer renderer in renderers)
            {
                if (renderer.enabled)
                {
                    minimumY = Mathf.Min(minimumY, renderer.bounds.min.y);
                }
            }

            if (!float.IsPositiveInfinity(minimumY))
            {
                instance.transform.position +=
                    Vector3.up * (groundHeight - minimumY);
            }
        }

        private static void DisableColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void SetStaticRecursively(GameObject root, bool value)
        {
            root.isStatic = value;
            foreach (Transform child in root.transform)
            {
                SetStaticRecursively(child.gameObject, value);
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(terrainMesh);
            terrainMesh = null;
            if (fallbackGroundMaterial != null)
            {
                DestroyRuntimeObject(fallbackGroundMaterial);
                fallbackGroundMaterial = null;
            }
        }
    }
}
