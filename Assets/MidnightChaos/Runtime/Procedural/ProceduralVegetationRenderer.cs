using System;
using System.Collections.Generic;
using MidnightChaos.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralVegetationRenderer : MonoBehaviour
    {
        private const int MaximumInstancesPerDraw = 1023;

        private readonly Dictionary<int, PrefabDescriptor> descriptors =
            new Dictionary<int, PrefabDescriptor>();
        private readonly Dictionary<Vector2Int, ChunkBuilder> chunkBuilders =
            new Dictionary<Vector2Int, ChunkBuilder>();
        private readonly List<RenderChunk> chunks = new List<RenderChunk>();
        private readonly HashSet<int> invalidPrefabWarnings = new HashSet<int>();

        private ProceduralWorldSettings settings;
        private Camera renderCamera;
        private int vegetationLayer;
        private int grassLayer;
        private float chunkSize;
        private float vegetationCullDistanceSquared;
        private float grassCullDistanceSquared;
        private float vegetationLodSwitchDistanceSquared;
        private float grassLodSwitchDistanceSquared;
        private bool isComplete;

        public int LogicalInstanceCount { get; private set; }
        public int ChunkCount => chunks.Count;
        public int DrawBatchCount { get; private set; }
        public int VisibleChunkCount { get; private set; }
        public int SubmittedDrawCount { get; private set; }

        public void Initialize(ProceduralWorldSettings configuredSettings)
        {
            settings = configuredSettings ??
                throw new ArgumentNullException(nameof(configuredSettings));
            vegetationLayer = ProceduralRenderUtility.ResolveLayer(
                ProceduralRenderUtility.VegetationLayerName,
                2,
                this);
            grassLayer = ProceduralRenderUtility.ResolveLayer(
                ProceduralRenderUtility.GrassLayerName,
                2,
                this);
            chunkSize = settings.VegetationChunkSize;
            vegetationCullDistanceSquared =
                settings.VegetationCullDistance *
                settings.VegetationCullDistance;
            grassCullDistanceSquared =
                settings.GrassCullDistance * settings.GrassCullDistance;
            vegetationLodSwitchDistanceSquared =
                settings.VegetationLodSwitchDistance *
                settings.VegetationLodSwitchDistance;
            grassLodSwitchDistanceSquared =
                settings.GrassLodSwitchDistance *
                settings.GrassLodSwitchDistance;
        }

        public bool TryAddPlacement(
            GameObject prefab,
            ProceduralObjectPlacement placement,
            ProceduralCategorySettings categorySettings)
        {
            if (isComplete || prefab == null || settings == null)
            {
                return false;
            }

            int prefabId = prefab.GetInstanceID();
            if (!descriptors.TryGetValue(prefabId, out PrefabDescriptor descriptor))
            {
                descriptor = BuildDescriptor(prefab);
                descriptors.Add(prefabId, descriptor);
            }

            if (!descriptor.IsValid)
            {
                if (invalidPrefabWarnings.Add(prefabId))
                {
                    Debug.LogWarning(
                        $"[Procedural] Plant prefab '{prefab.name}' " +
                        "cannot use GPU instancing. Falling back to its " +
                        "legacy GameObject path.",
                        prefab);
                }
                return false;
            }

            Matrix4x4 rootMatrix = CreateRootMatrix(
                prefab,
                descriptor,
                placement,
                categorySettings);
            bool disableShadows = placement.Category ==
                                  WorldObjectCategory.Grass
                ? settings.DisableGrassShadows
                : settings.DisableVegetationShadows;
            Vector2Int chunkCoordinate = new Vector2Int(
                Mathf.FloorToInt(placement.Position.x / chunkSize),
                Mathf.FloorToInt(placement.Position.z / chunkSize));
            if (!chunkBuilders.TryGetValue(
                    chunkCoordinate,
                    out ChunkBuilder chunk))
            {
                chunk = new ChunkBuilder(chunkCoordinate);
                chunkBuilders.Add(chunkCoordinate, chunk);
            }

            foreach (DrawPart part in descriptor.DrawParts)
            {
                Matrix4x4 matrix = rootMatrix * part.LocalToRoot;
                BatchKey key = new BatchKey(
                    placement.Category,
                    part.Mesh,
                    part.Material,
                    part.SubmeshIndex,
                    part.LodMode,
                    disableShadows
                        ? ShadowCastingMode.Off
                        : part.ShadowCastingMode,
                    disableShadows
                        ? false
                        : part.ReceiveShadows);
                chunk.Add(key, matrix, part.Mesh.bounds);
            }

            LogicalInstanceCount++;
            return true;
        }

        public void Complete()
        {
            if (isComplete)
            {
                return;
            }

            isComplete = true;
            chunks.Clear();
            DrawBatchCount = 0;
            foreach (ChunkBuilder builder in chunkBuilders.Values)
            {
                RenderChunk chunk = builder.Build();
                chunks.Add(chunk);
                DrawBatchCount += chunk.Batches.Count;
            }

            chunkBuilders.Clear();
            descriptors.Clear();
        }

        private void LateUpdate()
        {
            VisibleChunkCount = 0;
            SubmittedDrawCount = 0;
            if (!isComplete || chunks.Count == 0)
            {
                return;
            }

            if (renderCamera == null ||
                !renderCamera.isActiveAndEnabled ||
                !renderCamera.CompareTag("MainCamera"))
            {
                renderCamera = Camera.main;
            }
            if (renderCamera == null ||
                ((renderCamera.cullingMask & (1 << vegetationLayer)) == 0 &&
                 (renderCamera.cullingMask & (1 << grassLayer)) == 0))
            {
                return;
            }

            Vector3 cameraPosition = renderCamera.transform.position;
            float maximumCullDistanceSquared = Mathf.Max(
                vegetationCullDistanceSquared,
                grassCullDistanceSquared);
            foreach (RenderChunk chunk in chunks)
            {
                if (chunk.Bounds.SqrDistance(cameraPosition) >
                    maximumCullDistanceSquared)
                {
                    continue;
                }

                bool submittedChunk = false;
                foreach (RenderBatch batch in chunk.Batches)
                {
                    bool isGrass = batch.Category == WorldObjectCategory.Grass;
                    int renderLayer = isGrass ? grassLayer : vegetationLayer;
                    if ((renderCamera.cullingMask & (1 << renderLayer)) == 0)
                    {
                        continue;
                    }

                    float cullDistanceSquared = isGrass
                        ? grassCullDistanceSquared
                        : vegetationCullDistanceSquared;
                    if (batch.Bounds.SqrDistance(cameraPosition) >
                        cullDistanceSquared)
                    {
                        continue;
                    }

                    float lodSwitchDistanceSquared = isGrass
                        ? grassLodSwitchDistanceSquared
                        : vegetationLodSwitchDistanceSquared;
                    bool useLowLod =
                        (batch.Bounds.center - cameraPosition).sqrMagnitude >=
                        lodSwitchDistanceSquared;
                    if ((batch.LodMode == 0 && useLowLod) ||
                        (batch.LodMode == 1 && !useLowLod))
                    {
                        continue;
                    }

                    RenderParams renderParams = new RenderParams(batch.Material)
                    {
                        camera = renderCamera,
                        layer = renderLayer,
                        worldBounds = batch.Bounds,
                        shadowCastingMode = batch.ShadowCastingMode,
                        receiveShadows = batch.ReceiveShadows,
                        motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                        lightProbeUsage = LightProbeUsage.Off,
                        reflectionProbeUsage = ReflectionProbeUsage.Off
                    };
                    Graphics.RenderMeshInstanced(
                        renderParams,
                        batch.Mesh,
                        batch.SubmeshIndex,
                        batch.Matrices,
                        batch.Count);
                    SubmittedDrawCount++;
                    submittedChunk = true;
                }
                if (submittedChunk)
                {
                    VisibleChunkCount++;
                }
            }
        }

        private static PrefabDescriptor BuildDescriptor(GameObject prefab)
        {
            PrefabDescriptor descriptor = new PrefabDescriptor();
            if (ProceduralPrefabContract.TryFindPlacementAnchor(
                    prefab.transform,
                    out Transform anchor))
            {
                descriptor.HasAnchor = true;
                descriptor.AnchorPosition =
                    ProceduralPrefabContract.GetRootRelativePosition(
                        prefab.transform,
                        anchor);
                descriptor.AnchorRotation =
                    ProceduralPrefabContract.GetRootRelativeRotation(
                        prefab.transform,
                        anchor);
            }

            Dictionary<Renderer, int> lodModes = BuildLodLookup(prefab);
            MeshRenderer[] renderers =
                prefab.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                if (!renderer.enabled ||
                    !IsActiveInPrefabHierarchy(renderer.transform, prefab.transform))
                {
                    continue;
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }

                Matrix4x4 localToRoot =
                    prefab.transform.worldToLocalMatrix *
                    renderer.transform.localToWorldMatrix;
                descriptor.BoundsParts.Add(
                    new BoundsPart(mesh.bounds, localToRoot));

                int lodMode = lodModes.TryGetValue(renderer, out int value)
                    ? value
                    : -1;
                if (lodMode == 2)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    descriptor.IsValid = false;
                    return descriptor;
                }

                int submeshCount = Mathf.Max(1, mesh.subMeshCount);
                for (int submesh = 0; submesh < submeshCount; submesh++)
                {
                    Material material = materials[
                        Mathf.Min(submesh, materials.Length - 1)];
                    if (material == null || !material.enableInstancing)
                    {
                        descriptor.IsValid = false;
                        return descriptor;
                    }

                    descriptor.DrawParts.Add(
                        new DrawPart(
                            mesh,
                            material,
                            submesh,
                            localToRoot,
                            lodMode,
                            renderer.shadowCastingMode,
                            renderer.receiveShadows));
                }
            }

            descriptor.IsValid =
                descriptor.DrawParts.Count > 0 &&
                descriptor.BoundsParts.Count > 0;
            return descriptor;
        }

        private static Dictionary<Renderer, int> BuildLodLookup(GameObject prefab)
        {
            Dictionary<Renderer, int> result = new Dictionary<Renderer, int>();
            foreach (LODGroup group in
                     prefab.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] levels = group.GetLODs();
                if (levels.Length == 1)
                {
                    foreach (Renderer renderer in levels[0].renderers)
                    {
                        if (renderer != null)
                        {
                            result[renderer] = -1;
                        }
                    }
                    continue;
                }

                for (int levelIndex = 0;
                     levelIndex < levels.Length;
                     levelIndex++)
                {
                    int lodMode = levelIndex == 0
                        ? 0
                        : levelIndex == levels.Length - 1
                            ? 1
                            : 2;
                    foreach (Renderer renderer in levels[levelIndex].renderers)
                    {
                        if (renderer != null)
                        {
                            result[renderer] = lodMode;
                        }
                    }
                }
            }
            return result;
        }

        private static Matrix4x4 CreateRootMatrix(
            GameObject prefab,
            PrefabDescriptor descriptor,
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

            Quaternion targetAnchorRotation =
                ProceduralWorldGenerator.CalculateTargetAnchorRotation(
                    placement,
                    targetUp);
            Vector3 rootScale =
                prefab.transform.localScale * placement.UniformScale;
            Quaternion rootRotation = targetAnchorRotation;
            Vector3 rootPosition = placement.Position;

            if (descriptor.HasAnchor)
            {
                rootRotation =
                    targetAnchorRotation *
                    Quaternion.Inverse(descriptor.AnchorRotation);
                rootPosition -= rootRotation * Vector3.Scale(
                    rootScale,
                    descriptor.AnchorPosition);
            }

            Matrix4x4 rootMatrix = Matrix4x4.TRS(
                rootPosition,
                rootRotation,
                rootScale);
            if (descriptor.HasAnchor)
            {
                return rootMatrix;
            }

            float minimumY = float.PositiveInfinity;
            foreach (BoundsPart part in descriptor.BoundsParts)
            {
                Matrix4x4 matrix = rootMatrix * part.LocalToRoot;
                minimumY = Mathf.Min(
                    minimumY,
                    CalculateMinimumY(matrix, part.Bounds));
            }
            if (!float.IsPositiveInfinity(minimumY))
            {
                rootPosition.y += placement.Position.y - minimumY;
                rootMatrix = Matrix4x4.TRS(
                    rootPosition,
                    rootRotation,
                    rootScale);
            }
            return rootMatrix;
        }

        private static bool IsActiveInPrefabHierarchy(
            Transform candidate,
            Transform root)
        {
            Transform current = candidate;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }
                if (current == root)
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        private static float CalculateMinimumY(Matrix4x4 matrix, Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float result = float.PositiveInfinity;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        result = Mathf.Min(
                            result,
                            matrix.MultiplyPoint3x4(corner).y);
                    }
                }
            }
            return result;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;
            Vector3 axisX = matrix.MultiplyVector(
                new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(
                new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(
                new Vector3(0f, 0f, extents.z));
            extents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, extents * 2f);
        }

        private sealed class PrefabDescriptor
        {
            public bool IsValid;
            public bool HasAnchor;
            public Vector3 AnchorPosition;
            public Quaternion AnchorRotation = Quaternion.identity;
            public readonly List<BoundsPart> BoundsParts =
                new List<BoundsPart>();
            public readonly List<DrawPart> DrawParts = new List<DrawPart>();
        }

        private readonly struct BoundsPart
        {
            public BoundsPart(Bounds bounds, Matrix4x4 localToRoot)
            {
                Bounds = bounds;
                LocalToRoot = localToRoot;
            }

            public Bounds Bounds { get; }
            public Matrix4x4 LocalToRoot { get; }
        }

        private readonly struct DrawPart
        {
            public DrawPart(
                Mesh mesh,
                Material material,
                int submeshIndex,
                Matrix4x4 localToRoot,
                int lodMode,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows)
            {
                Mesh = mesh;
                Material = material;
                SubmeshIndex = submeshIndex;
                LocalToRoot = localToRoot;
                LodMode = lodMode;
                ShadowCastingMode = shadowCastingMode;
                ReceiveShadows = receiveShadows;
            }

            public Mesh Mesh { get; }
            public Material Material { get; }
            public int SubmeshIndex { get; }
            public Matrix4x4 LocalToRoot { get; }
            public int LodMode { get; }
            public ShadowCastingMode ShadowCastingMode { get; }
            public bool ReceiveShadows { get; }
        }

        private readonly struct BatchKey : IEquatable<BatchKey>
        {
            public BatchKey(
                WorldObjectCategory category,
                Mesh mesh,
                Material material,
                int submeshIndex,
                int lodMode,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows)
            {
                Category = category;
                Mesh = mesh;
                Material = material;
                SubmeshIndex = submeshIndex;
                LodMode = lodMode;
                ShadowCastingMode = shadowCastingMode;
                ReceiveShadows = receiveShadows;
            }

            public WorldObjectCategory Category { get; }
            public Mesh Mesh { get; }
            public Material Material { get; }
            public int SubmeshIndex { get; }
            public int LodMode { get; }
            public ShadowCastingMode ShadowCastingMode { get; }
            public bool ReceiveShadows { get; }

            public bool Equals(BatchKey other)
            {
                return Category == other.Category &&
                       Mesh == other.Mesh &&
                       Material == other.Material &&
                       SubmeshIndex == other.SubmeshIndex &&
                       LodMode == other.LodMode &&
                       ShadowCastingMode == other.ShadowCastingMode &&
                       ReceiveShadows == other.ReceiveShadows;
            }

            public override bool Equals(object obj)
            {
                return obj is BatchKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Category;
                    hash = hash * 397 ^
                           (Mesh != null ? Mesh.GetInstanceID() : 0);
                    hash = hash * 397 ^
                           (Material != null ? Material.GetInstanceID() : 0);
                    hash = hash * 397 ^ SubmeshIndex;
                    hash = hash * 397 ^ LodMode;
                    hash = hash * 397 ^ (int)ShadowCastingMode;
                    hash = hash * 397 ^ (ReceiveShadows ? 1 : 0);
                    return hash;
                }
            }
        }

        private sealed class ChunkBuilder
        {
            private readonly Dictionary<BatchKey, GroupBuilder> groups =
                new Dictionary<BatchKey, GroupBuilder>();

            public ChunkBuilder(Vector2Int coordinate)
            {
                Coordinate = coordinate;
            }

            private Vector2Int Coordinate { get; }

            public void Add(BatchKey key, Matrix4x4 matrix, Bounds meshBounds)
            {
                if (!groups.TryGetValue(key, out GroupBuilder group))
                {
                    group = new GroupBuilder(key);
                    groups.Add(key, group);
                }
                group.Add(matrix, meshBounds);
            }

            public RenderChunk Build()
            {
                List<RenderBatch> batches = new List<RenderBatch>();
                Bounds chunkBounds = default;
                bool hasBounds = false;
                foreach (GroupBuilder group in groups.Values)
                {
                    foreach (RenderBatch batch in group.Build())
                    {
                        batches.Add(batch);
                        if (!hasBounds)
                        {
                            chunkBounds = batch.Bounds;
                            hasBounds = true;
                        }
                        else
                        {
                            chunkBounds.Encapsulate(batch.Bounds);
                        }
                    }
                }
                return new RenderChunk(Coordinate, chunkBounds, batches);
            }
        }

        private sealed class GroupBuilder
        {
            private readonly BatchKey key;
            private readonly List<Matrix4x4> matrices = new List<Matrix4x4>();
            private readonly List<Bounds> bounds = new List<Bounds>();

            public GroupBuilder(BatchKey key)
            {
                this.key = key;
            }

            public void Add(Matrix4x4 matrix, Bounds meshBounds)
            {
                matrices.Add(matrix);
                bounds.Add(TransformBounds(matrix, meshBounds));
            }

            public IEnumerable<RenderBatch> Build()
            {
                for (int start = 0;
                     start < matrices.Count;
                     start += MaximumInstancesPerDraw)
                {
                    int count = Mathf.Min(
                        MaximumInstancesPerDraw,
                        matrices.Count - start);
                    Matrix4x4[] page = new Matrix4x4[count];
                    matrices.CopyTo(start, page, 0, count);
                    Bounds pageBounds = bounds[start];
                    for (int index = 1; index < count; index++)
                    {
                        pageBounds.Encapsulate(bounds[start + index]);
                    }
                    yield return new RenderBatch(key, page, pageBounds);
                }
            }
        }

        private sealed class RenderChunk
        {
            public RenderChunk(
                Vector2Int coordinate,
                Bounds bounds,
                List<RenderBatch> batches)
            {
                Coordinate = coordinate;
                Bounds = bounds;
                Batches = batches;
            }

            public Vector2Int Coordinate { get; }
            public Bounds Bounds { get; }
            public List<RenderBatch> Batches { get; }
        }

        private sealed class RenderBatch
        {
            public RenderBatch(
                BatchKey key,
                Matrix4x4[] matrices,
                Bounds bounds)
            {
                Category = key.Category;
                Mesh = key.Mesh;
                Material = key.Material;
                SubmeshIndex = key.SubmeshIndex;
                LodMode = key.LodMode;
                ShadowCastingMode = key.ShadowCastingMode;
                ReceiveShadows = key.ReceiveShadows;
                Matrices = matrices;
                Bounds = bounds;
            }

            public WorldObjectCategory Category { get; }
            public Mesh Mesh { get; }
            public Material Material { get; }
            public int SubmeshIndex { get; }
            public int LodMode { get; }
            public ShadowCastingMode ShadowCastingMode { get; }
            public bool ReceiveShadows { get; }
            public Matrix4x4[] Matrices { get; }
            public int Count => Matrices.Length;
            public Bounds Bounds { get; }
        }
    }
}
