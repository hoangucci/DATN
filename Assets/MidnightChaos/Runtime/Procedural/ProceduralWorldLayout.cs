using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidnightChaos.Procedural
{
    public enum ProceduralObjectCategory : byte
    {
        Tree = 0,
        Rock = 1,
        Ore = 2,
        Vegetation = 3
    }

    public static class ProceduralPrefabContract
    {
        public const string DefaultAnchorName = "BottomPoint";

        public static bool TryFindPlacementAnchor(
            Transform root,
            out Transform anchor)
        {
            anchor = null;
            if (root == null)
            {
                return false;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate != root && candidate.name == DefaultAnchorName)
                {
                    anchor = candidate;
                    return true;
                }
            }

            return false;
        }

        public static Vector3 GetRootRelativePosition(
            Transform root,
            Transform child)
        {
            return root == null || child == null
                ? Vector3.zero
                : root.InverseTransformPoint(child.position);
        }

        public static Quaternion GetRootRelativeRotation(
            Transform root,
            Transform child)
        {
            return root == null || child == null
                ? Quaternion.identity
                : Quaternion.Inverse(root.rotation) * child.rotation;
        }
    }

    public readonly struct ProceduralObjectPlacement
    {
        public ProceduralObjectPlacement(
            ProceduralObjectCategory category,
            int prefabIndex,
            Vector3 position,
            Vector3 surfaceNormal,
            Vector3 eulerAngles,
            float uniformScale,
            ProceduralNavigationMode navigationMode)
        {
            Category = category;
            PrefabIndex = prefabIndex;
            Position = position;
            SurfaceNormal = surfaceNormal;
            EulerAngles = eulerAngles;
            UniformScale = uniformScale;
            NavigationMode = navigationMode;
        }

        public ProceduralObjectCategory Category { get; }
        public int PrefabIndex { get; }
        public Vector3 Position { get; }
        public Vector3 SurfaceNormal { get; }
        public Vector3 EulerAngles { get; }
        public float UniformScale { get; }
        public ProceduralNavigationMode NavigationMode { get; }
    }

    public sealed class ProceduralWorldLayout
    {
        private readonly List<ProceduralObjectPlacement> objects =
            new List<ProceduralObjectPlacement>();
        private readonly List<Vector3> playerSpawnPoints = new List<Vector3>();
        private readonly List<Vector3> enemySpawnPoints = new List<Vector3>();
        private readonly List<string> warnings = new List<string>();

        internal ProceduralWorldLayout(
            int seed,
            int generatorVersion,
            Vector2 mapSize,
            int terrainSegments,
            float[] terrainHeights)
        {
            Seed = seed;
            GeneratorVersion = generatorVersion;
            MapSize = mapSize;
            TerrainSegments = terrainSegments;
            TerrainHeights = terrainHeights;
        }

        public int Seed { get; }
        public int GeneratorVersion { get; }
        public Vector2 MapSize { get; }
        public int TerrainSegments { get; }
        public float[] TerrainHeights { get; }
        public IReadOnlyList<ProceduralObjectPlacement> Objects => objects;
        public IReadOnlyList<Vector3> PlayerSpawnPoints => playerSpawnPoints;
        public IReadOnlyList<Vector3> EnemySpawnPoints => enemySpawnPoints;
        public IReadOnlyList<string> Warnings => warnings;
        public ulong LayoutHash { get; internal set; }

        internal List<ProceduralObjectPlacement> MutableObjects => objects;
        internal List<Vector3> MutablePlayerSpawnPoints => playerSpawnPoints;
        internal List<Vector3> MutableEnemySpawnPoints => enemySpawnPoints;
        internal List<string> MutableWarnings => warnings;
    }

    public static class ProceduralWorldLayoutBuilder
    {
        private const uint PlayerSpawnStream = 0x1C4A91D3u;
        private const uint EnemySpawnStream = 0x72E6B159u;
        private const uint TreeStream = 0xA13F3D21u;
        private const uint RockStream = 0xB82C47E5u;
        private const uint OreStream = 0xC9745A17u;
        private const uint VegetationStream = 0xD5E80B49u;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private readonly struct Reservation
        {
            public Reservation(Vector2 position, float radius)
            {
                Position = position;
                Radius = radius;
            }

            public Vector2 Position { get; }
            public float Radius { get; }
        }

        private sealed class SpatialReservationGrid
        {
            private readonly Dictionary<long, List<Reservation>> cells =
                new Dictionary<long, List<Reservation>>();
            private readonly float cellSize;

            public SpatialReservationGrid(float maximumRadius)
            {
                cellSize = Mathf.Max(0.5f, maximumRadius * 2f);
            }

            public bool IsFree(Vector2 point, float radius)
            {
                int centerX = Mathf.FloorToInt(point.x / cellSize);
                int centerY = Mathf.FloorToInt(point.y / cellSize);

                for (int y = centerY - 1; y <= centerY + 1; y++)
                {
                    for (int x = centerX - 1; x <= centerX + 1; x++)
                    {
                        if (!cells.TryGetValue(Key(x, y), out List<Reservation> values))
                        {
                            continue;
                        }

                        foreach (Reservation value in values)
                        {
                            float required = radius + value.Radius;
                            if ((value.Position - point).sqrMagnitude < required * required)
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }

            public void Add(Vector2 point, float radius)
            {
                int x = Mathf.FloorToInt(point.x / cellSize);
                int y = Mathf.FloorToInt(point.y / cellSize);
                long key = Key(x, y);

                if (!cells.TryGetValue(key, out List<Reservation> values))
                {
                    values = new List<Reservation>();
                    cells.Add(key, values);
                }

                values.Add(new Reservation(point, radius));
            }

            private static long Key(int x, int y)
            {
                return ((long)x << 32) ^ unchecked((uint)y);
            }
        }

        public static ProceduralWorldLayout Build(
            ProceduralWorldSettings settings,
            int seed)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            Vector2 mapSize = settings.MapSize;
            int segments = settings.TerrainSegments;
            float[] heights = BuildTerrainHeights(settings, seed);
            ProceduralWorldLayout layout = new ProceduralWorldLayout(
                seed,
                settings.GeneratorVersion,
                mapSize,
                segments,
                heights);

            float maximumRadius = Mathf.Max(
                settings.PlayerSpawnClearance,
                settings.EnemySpawnClearance,
                settings.Trees.ClearanceRadius,
                settings.Rocks.ClearanceRadius,
                settings.Ores.ClearanceRadius,
                settings.Vegetation.ClearanceRadius);
            SpatialReservationGrid reservations =
                new SpatialReservationGrid(maximumRadius);

            PlaceSpawnPoints(
                layout.MutablePlayerSpawnPoints,
                settings.PlayerSpawnPointCount,
                settings.PlayerSpawnClearance,
                PlayerSpawnStream,
                settings,
                seed,
                reservations,
                null,
                0f,
                "player spawn",
                layout.MutableWarnings);

            PlaceSpawnPoints(
                layout.MutableEnemySpawnPoints,
                settings.EnemySpawnPointCount,
                settings.EnemySpawnClearance,
                EnemySpawnStream,
                settings,
                seed,
                reservations,
                layout.PlayerSpawnPoints,
                settings.EnemyDistanceFromPlayerSpawns,
                "enemy spawn",
                layout.MutableWarnings);

            PlaceCategory(
                layout,
                ProceduralObjectCategory.Tree,
                settings.Trees,
                TreeStream,
                settings,
                seed,
                reservations);
            PlaceCategory(
                layout,
                ProceduralObjectCategory.Rock,
                settings.Rocks,
                RockStream,
                settings,
                seed,
                reservations);
            PlaceCategory(
                layout,
                ProceduralObjectCategory.Ore,
                settings.Ores,
                OreStream,
                settings,
                seed,
                reservations);
            PlaceCategory(
                layout,
                ProceduralObjectCategory.Vegetation,
                settings.Vegetation,
                VegetationStream,
                settings,
                seed,
                reservations);

            layout.LayoutHash = CalculateHash(layout, settings);
            return layout;
        }

        public static float EvaluateHeight(
            ProceduralWorldSettings settings,
            int seed,
            float x,
            float z)
        {
            float scale = settings.NoiseScale;
            float first = ValueNoise(x * scale, z * scale, seed, 0x19A3F5D7u);
            float second = ValueNoise(
                x * scale * 2.07f,
                z * scale * 2.07f,
                seed,
                0x51D92E6Bu);
            float third = ValueNoise(
                x * scale * 4.13f,
                z * scale * 4.13f,
                seed,
                0xA7C3B149u);

            float normalizedNoise = first * 0.58f + second * 0.29f + third * 0.13f;
            float centeredNoise = normalizedNoise * 2f - 1f;

            Vector2 mapSize = settings.MapSize;
            float edge = Mathf.Max(
                Mathf.Abs(x) / (mapSize.x * 0.5f),
                Mathf.Abs(z) / (mapSize.y * 0.5f));
            float edgeT = Mathf.InverseLerp(settings.EdgeFalloffStart, 1f, edge);
            edgeT = edgeT * edgeT * (3f - 2f * edgeT);

            if (edgeT >= 1f)
            {
                return settings.UniformEdgeHeight;
            }

            // Fade noise to zero across the falloff band. Edge Falloff Start
            // and Edge Drop retain their roles while the complete outer rim
            // converges to one exact, configurable elevation.
            float noiseHeight =
                centeredNoise * settings.HeightAmplitude * (1f - edgeT);
            return settings.BaseHeight +
                   noiseHeight -
                   edgeT * settings.EdgeDrop +
                   edgeT * settings.EdgeHeightOffset;
        }

        public static float EvaluateTerrainSurfaceHeight(
            ProceduralWorldSettings settings,
            int seed,
            float x,
            float z)
        {
            Vector2 size = settings.MapSize;
            int segments = settings.TerrainSegments;
            float gridX = Mathf.Clamp(
                (x / size.x + 0.5f) * segments,
                0f,
                segments);
            float gridZ = Mathf.Clamp(
                (z / size.y + 0.5f) * segments,
                0f,
                segments);
            int cellX = Mathf.Min(Mathf.FloorToInt(gridX), segments - 1);
            int cellZ = Mathf.Min(Mathf.FloorToInt(gridZ), segments - 1);
            float localX = gridX - cellX;
            float localZ = gridZ - cellZ;

            float x0 = ((float)cellX / segments - 0.5f) * size.x;
            float x1 = ((float)(cellX + 1) / segments - 0.5f) * size.x;
            float z0 = ((float)cellZ / segments - 0.5f) * size.y;
            float z1 = ((float)(cellZ + 1) / segments - 0.5f) * size.y;
            float height00 = EvaluateHeight(settings, seed, x0, z0);
            float height10 = EvaluateHeight(settings, seed, x1, z0);
            float height01 = EvaluateHeight(settings, seed, x0, z1);
            float height11 = EvaluateHeight(settings, seed, x1, z1);

            // Match the two triangles emitted by ProceduralWorldGenerator.
            if (localX + localZ <= 1f)
            {
                return height00 +
                       localX * (height10 - height00) +
                       localZ * (height01 - height00);
            }

            return height11 +
                   (1f - localX) * (height01 - height11) +
                   (1f - localZ) * (height10 - height11);
        }

        public static Vector3 EvaluateTerrainSurfaceNormal(
            ProceduralWorldSettings settings,
            int seed,
            float x,
            float z)
        {
            Vector2 size = settings.MapSize;
            int segments = settings.TerrainSegments;
            float gridX = Mathf.Clamp(
                (x / size.x + 0.5f) * segments,
                0f,
                segments);
            float gridZ = Mathf.Clamp(
                (z / size.y + 0.5f) * segments,
                0f,
                segments);
            int cellX = Mathf.Min(Mathf.FloorToInt(gridX), segments - 1);
            int cellZ = Mathf.Min(Mathf.FloorToInt(gridZ), segments - 1);
            float localX = gridX - cellX;
            float localZ = gridZ - cellZ;

            float x0 = ((float)cellX / segments - 0.5f) * size.x;
            float x1 = ((float)(cellX + 1) / segments - 0.5f) * size.x;
            float z0 = ((float)cellZ / segments - 0.5f) * size.y;
            float z1 = ((float)(cellZ + 1) / segments - 0.5f) * size.y;
            Vector3 vertex00 = new Vector3(
                x0,
                EvaluateHeight(settings, seed, x0, z0),
                z0);
            Vector3 vertex10 = new Vector3(
                x1,
                EvaluateHeight(settings, seed, x1, z0),
                z0);
            Vector3 vertex01 = new Vector3(
                x0,
                EvaluateHeight(settings, seed, x0, z1),
                z1);
            Vector3 vertex11 = new Vector3(
                x1,
                EvaluateHeight(settings, seed, x1, z1),
                z1);

            Vector3 normal = localX + localZ <= 1f
                ? Vector3.Cross(vertex01 - vertex00, vertex10 - vertex00)
                : Vector3.Cross(vertex01 - vertex10, vertex11 - vertex10);
            if (normal.y < 0f)
            {
                normal = -normal;
            }

            return normal.sqrMagnitude > 0.000001f
                ? normal.normalized
                : Vector3.up;
        }

        private static float[] BuildTerrainHeights(
            ProceduralWorldSettings settings,
            int seed)
        {
            int segments = settings.TerrainSegments;
            int side = segments + 1;
            float[] heights = new float[side * side];
            Vector2 size = settings.MapSize;

            for (int z = 0; z <= segments; z++)
            {
                float worldZ = ((float)z / segments - 0.5f) * size.y;
                for (int x = 0; x <= segments; x++)
                {
                    float worldX = ((float)x / segments - 0.5f) * size.x;
                    heights[z * side + x] = EvaluateHeight(
                        settings,
                        seed,
                        worldX,
                        worldZ);
                }
            }

            return heights;
        }

        private static void PlaceSpawnPoints(
            List<Vector3> destination,
            int targetCount,
            float clearance,
            uint stream,
            ProceduralWorldSettings settings,
            int seed,
            SpatialReservationGrid reservations,
            IReadOnlyList<Vector3> avoidPoints,
            float avoidDistance,
            string label,
            List<string> warnings)
        {
            DeterministicRandom random = new DeterministicRandom(
                DeterministicRandom.DeriveSeed(seed, stream));
            int maximumAttempts = Mathf.Max(1, targetCount) * settings.AttemptsPerObject;

            for (int attempt = 0; attempt < maximumAttempts && destination.Count < targetCount; attempt++)
            {
                if (!TryCreateCandidate(
                        ref random,
                        settings,
                        seed,
                        clearance,
                        out Vector3 candidate,
                        out _))
                {
                    continue;
                }

                Vector2 planar = new Vector2(candidate.x, candidate.z);
                if (!reservations.IsFree(planar, clearance) ||
                    IsNearAny(candidate, avoidPoints, avoidDistance))
                {
                    continue;
                }

                destination.Add(candidate);
                reservations.Add(planar, clearance);
            }

            if (destination.Count < targetCount)
            {
                warnings.Add(
                    $"Placed {destination.Count}/{targetCount} {label} points. " +
                    "Increase map area, attempts, or reduce clearance.");
            }
        }

        private static void PlaceCategory(
            ProceduralWorldLayout layout,
            ProceduralObjectCategory category,
            ProceduralCategorySettings categorySettings,
            uint stream,
            ProceduralWorldSettings settings,
            int seed,
            SpatialReservationGrid reservations)
        {
            if (categorySettings.Count <= 0 || categorySettings.Prefabs.Length == 0)
            {
                if (categorySettings.Count > 0)
                {
                    layout.MutableWarnings.Add(
                        $"{category} count is {categorySettings.Count}, but its prefab list is empty.");
                }
                return;
            }

            DeterministicRandom random = new DeterministicRandom(
                DeterministicRandom.DeriveSeed(seed, stream));
            int placed = 0;
            int maximumAttempts = categorySettings.Count * settings.AttemptsPerObject;

            for (int attempt = 0; attempt < maximumAttempts && placed < categorySettings.Count; attempt++)
            {
                if (!TryCreateCandidate(
                        ref random,
                        settings,
                        seed,
                        categorySettings.ClearanceRadius,
                        out Vector3 candidate,
                        out Vector3 surfaceNormal))
                {
                    continue;
                }

                Vector2 planar = new Vector2(candidate.x, candidate.z);
                if (!reservations.IsFree(planar, categorySettings.ClearanceRadius))
                {
                    continue;
                }

                Vector2 scaleRange = categorySettings.UniformScaleRange;
                float tilt = categorySettings.RandomTiltDegrees;
                Vector3 euler = new Vector3(
                    random.Range(-tilt, tilt),
                    random.Range(0f, 360f),
                    random.Range(-tilt, tilt));

                layout.MutableObjects.Add(
                    new ProceduralObjectPlacement(
                        category,
                        random.Range(0, categorySettings.Prefabs.Length),
                        candidate,
                        surfaceNormal,
                        euler,
                        random.Range(scaleRange.x, scaleRange.y),
                        categorySettings.NavigationMode));
                reservations.Add(planar, categorySettings.ClearanceRadius);
                placed++;
            }

            if (placed < categorySettings.Count)
            {
                layout.MutableWarnings.Add(
                    $"Placed {placed}/{categorySettings.Count} {category} objects. " +
                    "Increase map area, attempts, or reduce clearance.");
            }
        }

        private static bool TryCreateCandidate(
            ref DeterministicRandom random,
            ProceduralWorldSettings settings,
            int seed,
            float clearance,
            out Vector3 candidate,
            out Vector3 surfaceNormal)
        {
            Vector2 mapSize = settings.MapSize;
            float padding = settings.EdgePadding + clearance;
            float halfX = mapSize.x * 0.5f - padding;
            float halfZ = mapSize.y * 0.5f - padding;

            if (halfX <= 0f || halfZ <= 0f)
            {
                candidate = default;
                surfaceNormal = Vector3.up;
                return false;
            }

            float x = random.Range(-halfX, halfX);
            float z = random.Range(-halfZ, halfZ);
            float height = EvaluateTerrainSurfaceHeight(settings, seed, x, z);
            candidate = new Vector3(x, height, z);
            surfaceNormal = EvaluateTerrainSurfaceNormal(settings, seed, x, z);
            float slope = Vector3.Angle(surfaceNormal, Vector3.up);
            return slope <= settings.MaximumSlopeDegrees;
        }

        private static bool IsNearAny(
            Vector3 candidate,
            IReadOnlyList<Vector3> points,
            float minimumDistance)
        {
            if (points == null || minimumDistance <= 0f)
            {
                return false;
            }

            float minimumDistanceSquared = minimumDistance * minimumDistance;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 delta = new Vector2(
                    points[index].x - candidate.x,
                    points[index].z - candidate.z);
                if (delta.sqrMagnitude < minimumDistanceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ValueNoise(
            float x,
            float y,
            int seed,
            uint salt)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = x - x0;
            float ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);

            float a = Hash01(x0, y0, seed, salt);
            float b = Hash01(x0 + 1, y0, seed, salt);
            float c = Hash01(x0, y0 + 1, seed, salt);
            float d = Hash01(x0 + 1, y0 + 1, seed, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Hash01(int x, int y, int seed, uint salt)
        {
            uint value = unchecked((uint)seed) ^ salt;
            value ^= unchecked((uint)x) * 0x9E3779B9u;
            value = (value << 17) | (value >> 15);
            value ^= unchecked((uint)y) * 0x85EBCA6Bu;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            return (value >> 8) * (1f / 16777216f);
        }

        private static ulong CalculateHash(
            ProceduralWorldLayout layout,
            ProceduralWorldSettings settings)
        {
            ulong hash = FnvOffset;
            AddHash(ref hash, layout.Seed);
            AddHash(ref hash, layout.GeneratorVersion);
            AddHash(ref hash, layout.MapSize.x);
            AddHash(ref hash, layout.MapSize.y);
            AddHash(ref hash, layout.TerrainSegments);

            // Placements store a catalog index. Hash the ordered catalog too,
            // so Host and Client reject a world if those indices resolve to
            // different prefab assets in their builds.
            AddCatalogHash(ref hash, settings.Trees);
            AddCatalogHash(ref hash, settings.Rocks);
            AddCatalogHash(ref hash, settings.Ores);
            AddCatalogHash(ref hash, settings.Vegetation);

            foreach (float height in layout.TerrainHeights)
            {
                AddHash(ref hash, height);
            }

            foreach (Vector3 point in layout.PlayerSpawnPoints)
            {
                AddHash(ref hash, point);
            }
            foreach (Vector3 point in layout.EnemySpawnPoints)
            {
                AddHash(ref hash, point);
            }
            foreach (ProceduralObjectPlacement placement in layout.Objects)
            {
                AddHash(ref hash, (int)placement.Category);
                AddHash(ref hash, placement.PrefabIndex);
                AddHash(ref hash, placement.Position);
                AddHash(ref hash, placement.SurfaceNormal);
                AddHash(ref hash, placement.EulerAngles);
                AddHash(ref hash, placement.UniformScale);
                AddHash(ref hash, (int)placement.NavigationMode);
            }

            return hash;
        }

        private static void AddCatalogHash(
            ref ulong hash,
            ProceduralCategorySettings category)
        {
            GameObject[] prefabs = category.Prefabs;
            AddHash(ref hash, (int)category.SurfaceAlignment);
            AddHash(ref hash, (int)category.NavigationMode);
            AddHash(ref hash, category.LodCullScreenHeightOverride);
            AddHash(ref hash, prefabs.Length);
            foreach (GameObject prefab in prefabs)
            {
                AddHash(ref hash, prefab == null ? "<null>" : prefab.name);
                if (prefab != null &&
                    ProceduralPrefabContract.TryFindPlacementAnchor(
                        prefab.transform,
                        out Transform anchor))
                {
                    AddHash(
                        ref hash,
                        ProceduralPrefabContract.GetRootRelativePosition(
                            prefab.transform,
                            anchor));
                    Quaternion rotation =
                        ProceduralPrefabContract.GetRootRelativeRotation(
                            prefab.transform,
                            anchor);
                    AddHash(ref hash, rotation.x);
                    AddHash(ref hash, rotation.y);
                    AddHash(ref hash, rotation.z);
                    AddHash(ref hash, rotation.w);
                }
                else
                {
                    AddHash(ref hash, "<no-anchor>");
                }
            }
        }

        private static void AddHash(ref ulong hash, string value)
        {
            if (value == null)
            {
                AddHash(ref hash, -1);
                return;
            }

            AddHash(ref hash, value.Length);
            foreach (char character in value)
            {
                unchecked
                {
                    hash ^= (byte)character;
                    hash *= FnvPrime;
                    hash ^= (byte)(character >> 8);
                    hash *= FnvPrime;
                }
            }
        }

        private static void AddHash(ref ulong hash, Vector3 value)
        {
            AddHash(ref hash, value.x);
            AddHash(ref hash, value.y);
            AddHash(ref hash, value.z);
        }

        private static void AddHash(ref ulong hash, float value)
        {
            AddHash(ref hash, Mathf.RoundToInt(value * 10000f));
        }

        private static void AddHash(ref ulong hash, int value)
        {
            unchecked
            {
                uint bits = (uint)value;
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(bits >> shift);
                    hash *= FnvPrime;
                }
            }
        }
    }
}
