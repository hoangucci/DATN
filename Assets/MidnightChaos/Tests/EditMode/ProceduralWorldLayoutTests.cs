using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace MidnightChaos.Procedural.Tests
{
    public sealed class ProceduralWorldLayoutTests
    {
        private const string SettingsResourcePath =
            "Procedural/ProceduralWorldSettings";

        private ProceduralWorldSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = UnityEngine.Resources.Load<ProceduralWorldSettings>(
                SettingsResourcePath);
            Assert.That(
                settings,
                Is.Not.Null,
                $"Missing Resources/{SettingsResourcePath}.asset");
        }

        [Test]
        public void SameSeedProducesExactlyTheSameLayout()
        {
            ProceduralWorldLayout first =
                ProceduralWorldLayoutBuilder.Build(settings, 12345);
            ProceduralWorldLayout second =
                ProceduralWorldLayoutBuilder.Build(settings, 12345);

            Assert.That(second.LayoutHash, Is.EqualTo(first.LayoutHash));
            Assert.That(second.TerrainHeights, Is.EqualTo(first.TerrainHeights));
            Assert.That(second.PlayerSpawnPoints, Is.EqualTo(first.PlayerSpawnPoints));
            Assert.That(second.EnemySpawnPoints, Is.EqualTo(first.EnemySpawnPoints));
            Assert.That(second.Objects.Count, Is.EqualTo(first.Objects.Count));

            for (int index = 0; index < first.Objects.Count; index++)
            {
                AssertPlacementEqual(first.Objects[index], second.Objects[index]);
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentLayoutHashes()
        {
            ProceduralWorldLayout first =
                ProceduralWorldLayoutBuilder.Build(settings, 12345);
            ProceduralWorldLayout second =
                ProceduralWorldLayoutBuilder.Build(settings, 54321);

            Assert.That(second.LayoutHash, Is.Not.EqualTo(first.LayoutHash));
        }

        [Test]
        public void LocalRenderingSettingsDoNotChangeLayoutHash()
        {
            ProceduralWorldSettings clone = Object.Instantiate(settings);
            try
            {
                SetPrivateField(clone, "useInstancedVegetation", false);
                SetPrivateField(clone, "useLayerDistanceCulling", false);
                SetPrivateField(clone, "vegetationCullDistance", 12f);
                SetPrivateField(clone, "treeCullDistance", 35f);
                SetPrivateField(clone, "cameraFarClipPlane", 250f);
                SetPrivateField(clone, "enableTreeParticles", true);

                ProceduralWorldLayout expected =
                    ProceduralWorldLayoutBuilder.Build(settings, 12345);
                ProceduralWorldLayout actual =
                    ProceduralWorldLayoutBuilder.Build(clone, 12345);

                Assert.That(actual.LayoutHash, Is.EqualTo(expected.LayoutHash));
                Assert.That(actual.Objects.Count, Is.EqualTo(expected.Objects.Count));
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void VegetationCatalogSupportsGameObjectFreeInstancing()
        {
            HashSet<GameObject> checkedPrefabs = new HashSet<GameObject>();
            foreach (GameObject prefab in settings.Vegetation.Prefabs)
            {
                Assert.That(prefab, Is.Not.Null);
                if (!checkedPrefabs.Add(prefab))
                {
                    continue;
                }

                Assert.That(
                    prefab.GetComponentsInChildren<Collider>(true),
                    Is.Empty,
                    $"Vegetation '{prefab.name}' must not have colliders.");
                Assert.That(
                    prefab.GetComponentsInChildren<Rigidbody>(true),
                    Is.Empty,
                    $"Vegetation '{prefab.name}' must not have rigidbodies.");

                MeshRenderer[] renderers =
                    prefab.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers, Is.Not.Empty);
                foreach (MeshRenderer renderer in renderers)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    Assert.That(filter, Is.Not.Null);
                    Assert.That(filter.sharedMesh, Is.Not.Null);
                    Assert.That(renderer.sharedMaterials, Is.Not.Empty);
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null);
                        Assert.That(
                            material.enableInstancing,
                            Is.True,
                            $"Material '{material.name}' on '{prefab.name}' " +
                            "must enable GPU instancing.");
                    }
                }
            }
        }

        [Test]
        public void EveryOuterEdgePointHasExactlyTheConfiguredHeight()
        {
            const int sampleCount = 64;
            float halfX = settings.MapSize.x * 0.5f;
            float halfZ = settings.MapSize.y * 0.5f;
            float expectedHeight = settings.UniformEdgeHeight;

            for (int index = 0; index <= sampleCount; index++)
            {
                float t = (float)index / sampleCount;
                float x = Mathf.Lerp(-halfX, halfX, t);
                float z = Mathf.Lerp(-halfZ, halfZ, t);

                AssertEdgeHeight(-halfX, z, expectedHeight);
                AssertEdgeHeight(halfX, z, expectedHeight);
                AssertEdgeHeight(x, -halfZ, expectedHeight);
                AssertEdgeHeight(x, halfZ, expectedHeight);
            }
        }

        [Test]
        public void EnvironmentPrefabsUseExpectedPlacementContract()
        {
            AssertCategoryHasAnchors(settings.Trees, "tree");
            AssertCategoryHasAnchors(settings.Rocks, "rock");
            AssertCategoryHasAnchors(settings.Ores, "ore");

            Assert.That(
                settings.Trees.NavigationMode,
                Is.EqualTo(ProceduralNavigationMode.DynamicCarving));
            Assert.That(
                settings.Rocks.NavigationMode,
                Is.EqualTo(ProceduralNavigationMode.DynamicCarving));
            Assert.That(
                settings.Ores.NavigationMode,
                Is.EqualTo(ProceduralNavigationMode.DynamicCarving));
            Assert.That(
                settings.Trees.SurfaceAlignment,
                Is.EqualTo(ProceduralSurfaceAlignment.Upright));
            Assert.That(
                settings.Rocks.SurfaceAlignment,
                Is.EqualTo(
                    ProceduralSurfaceAlignment.AlignToSurfaceNormal));
            Assert.That(
                settings.Ores.SurfaceAlignment,
                Is.EqualTo(
                    ProceduralSurfaceAlignment.AlignToSurfaceNormal));
            Assert.That(settings.Rocks.RandomTiltDegrees, Is.Zero);
            Assert.That(settings.Ores.RandomTiltDegrees, Is.Zero);
            Assert.That(
                settings.Vegetation.NavigationMode,
                Is.EqualTo(ProceduralNavigationMode.None));
        }

        [Test]
        public void DefaultSettingsRespectCountsBoundsAndTerrainHeight()
        {
            ProceduralWorldLayout layout =
                ProceduralWorldLayoutBuilder.Build(settings, 12345);

            Assert.That(
                layout.Objects.Count,
                Is.EqualTo(settings.ConfiguredEnvironmentCount),
                string.Join("\n", layout.Warnings));
            Assert.That(
                layout.PlayerSpawnPoints.Count,
                Is.EqualTo(settings.PlayerSpawnPointCount),
                string.Join("\n", layout.Warnings));
            Assert.That(
                layout.EnemySpawnPoints.Count,
                Is.EqualTo(settings.EnemySpawnPointCount),
                string.Join("\n", layout.Warnings));

            foreach (float height in layout.TerrainHeights)
            {
                Assert.That(float.IsNaN(height), Is.False);
                Assert.That(float.IsInfinity(height), Is.False);
            }

            foreach (Vector3 point in layout.PlayerSpawnPoints)
            {
                AssertPointIsOnTerrainAndInsideMap(
                    layout.Seed,
                    point,
                    settings.PlayerSpawnClearance);
            }

            foreach (Vector3 point in layout.EnemySpawnPoints)
            {
                AssertPointIsOnTerrainAndInsideMap(
                    layout.Seed,
                    point,
                    settings.EnemySpawnClearance);
                foreach (Vector3 playerPoint in layout.PlayerSpawnPoints)
                {
                    Assert.That(
                        PlanarDistance(point, playerPoint),
                        Is.GreaterThanOrEqualTo(
                            settings.EnemyDistanceFromPlayerSpawns - 0.0001f));
                }
            }

            foreach (ProceduralObjectPlacement placement in layout.Objects)
            {
                float objectClearance = GetClearance(placement.Category);
                AssertPointIsOnTerrainAndInsideMap(
                    layout.Seed,
                    placement.Position,
                    objectClearance);
                Assert.That(
                    placement.SurfaceNormal.magnitude,
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(placement.SurfaceNormal.y, Is.GreaterThan(0f));
                Assert.That(
                    placement.SurfaceNormal,
                    Is.EqualTo(
                        ProceduralWorldLayoutBuilder.EvaluateTerrainSurfaceNormal(
                            settings,
                            layout.Seed,
                            placement.Position.x,
                            placement.Position.z)));

                foreach (Vector3 playerPoint in layout.PlayerSpawnPoints)
                {
                    Assert.That(
                        PlanarDistance(placement.Position, playerPoint),
                        Is.GreaterThanOrEqualTo(
                            settings.PlayerSpawnClearance +
                            objectClearance -
                            0.0001f));
                }
            }
        }

        private void AssertPointIsOnTerrainAndInsideMap(
            int seed,
            Vector3 point,
            float clearance)
        {
            float maximumX =
                settings.MapSize.x * 0.5f - settings.EdgePadding - clearance;
            float maximumZ =
                settings.MapSize.y * 0.5f - settings.EdgePadding - clearance;
            Assert.That(Mathf.Abs(point.x), Is.LessThanOrEqualTo(maximumX));
            Assert.That(Mathf.Abs(point.z), Is.LessThanOrEqualTo(maximumZ));
            Assert.That(
                point.y,
                Is.EqualTo(
                    ProceduralWorldLayoutBuilder.EvaluateTerrainSurfaceHeight(
                        settings,
                        seed,
                        point.x,
                        point.z))
                    .Within(0.0001f));
        }

        private void AssertEdgeHeight(
            float x,
            float z,
            float expectedHeight)
        {
            Assert.That(
                ProceduralWorldLayoutBuilder.EvaluateHeight(
                    settings,
                    12345,
                    x,
                    z),
                Is.EqualTo(expectedHeight).Within(0.0001f));
        }

        private float GetClearance(ProceduralObjectCategory category)
        {
            return category switch
            {
                ProceduralObjectCategory.Tree => settings.Trees.ClearanceRadius,
                ProceduralObjectCategory.Rock => settings.Rocks.ClearanceRadius,
                ProceduralObjectCategory.Ore => settings.Ores.ClearanceRadius,
                ProceduralObjectCategory.Vegetation =>
                    settings.Vegetation.ClearanceRadius,
                _ => throw new AssertionException($"Unknown category {category}")
            };
        }

        private static void AssertCategoryHasAnchors(
            ProceduralCategorySettings category,
            string label)
        {
            Assert.That(category.Prefabs, Is.Not.Empty);
            foreach (GameObject prefab in category.Prefabs)
            {
                Assert.That(prefab, Is.Not.Null);
                Assert.That(
                    ProceduralPrefabContract.TryFindPlacementAnchor(
                        prefab.transform,
                        out Transform anchor),
                    Is.True,
                    $"{label} prefab '{prefab.name}' thiếu BottomPoint.");
                Assert.That(anchor, Is.Not.Null);
                if (category.NavigationMode ==
                    ProceduralNavigationMode.DynamicCarving)
                {
                    NavMeshObstacle obstacle =
                        prefab.GetComponentInChildren<NavMeshObstacle>(true);
                    Assert.That(
                        obstacle,
                        Is.Not.Null,
                        $"{label} prefab '{prefab.name}' thiếu " +
                        "NavMeshObstacle authored.");
                    Assert.That(
                        obstacle.carving,
                        Is.True,
                        $"{label} prefab '{prefab.name}' chưa bật Carving.");
                }
            }
        }

        private static float PlanarDistance(Vector3 first, Vector3 second)
        {
            return Vector2.Distance(
                new Vector2(first.x, first.z),
                new Vector2(second.x, second.z));
        }

        private static void SetPrivateField<T>(
            ProceduralWorldSettings target,
            string fieldName,
            T value)
        {
            FieldInfo field = typeof(ProceduralWorldSettings).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static void AssertPlacementEqual(
            ProceduralObjectPlacement expected,
            ProceduralObjectPlacement actual)
        {
            Assert.That(actual.Category, Is.EqualTo(expected.Category));
            Assert.That(actual.PrefabIndex, Is.EqualTo(expected.PrefabIndex));
            Assert.That(actual.Position, Is.EqualTo(expected.Position));
            Assert.That(actual.SurfaceNormal, Is.EqualTo(expected.SurfaceNormal));
            Assert.That(actual.EulerAngles, Is.EqualTo(expected.EulerAngles));
            Assert.That(actual.UniformScale, Is.EqualTo(expected.UniformScale));
            Assert.That(
                actual.NavigationMode,
                Is.EqualTo(expected.NavigationMode));
        }
    }
}
