using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class RuntimeNavMeshBuilder : MonoBehaviour
    {
        private NavMeshSurface surface;
        private NavMeshData runtimeData;
        private NavMeshTriangulation debugTriangulation;

        public bool IsBuilding { get; private set; }
        public bool IsReady { get; private set; }
        public string StatusText { get; private set; } = "Not built";

        public void Initialize(ProceduralWorldSettings settings)
        {
            surface = GetComponent<NavMeshSurface>();
            surface.agentTypeID = settings.NavMeshAgentTypeId;
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(0f, settings.BaseHeight, 0f);
            surface.size = new Vector3(
                settings.MapSize.x,
                settings.NavMeshVolumeHeight,
                settings.MapSize.y);
            int sourceMask = ~(1 << 2);
            int vegetationLayer = LayerMask.NameToLayer(
                ProceduralRenderUtility.VegetationLayerName);
            if (vegetationLayer >= 0)
            {
                sourceMask &= ~(1 << vegetationLayer);
            }
            int grassLayer = LayerMask.NameToLayer(
                ProceduralRenderUtility.GrassLayerName);
            if (grassLayer >= 0)
            {
                sourceMask &= ~(1 << grassLayer);
            }
            surface.layerMask = sourceMask;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
        }

        public IEnumerator Rebuild(ProceduralWorldSettings settings)
        {
            if (IsBuilding)
            {
                yield break;
            }

            Initialize(settings);
            IsBuilding = true;
            IsReady = false;
            StatusText = "Building...";
            ClearRuntimeData();

            Physics.SyncTransforms();
            NavMeshData newData = new NavMeshData(settings.NavMeshAgentTypeId)
            {
                name = "ProceduralDemo_RuntimeNavMesh"
            };

            AsyncOperation operation = surface.UpdateNavMesh(newData);
            while (!operation.isDone)
            {
                yield return null;
            }

            runtimeData = newData;
            surface.navMeshData = runtimeData;
            surface.AddData();

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            debugTriangulation = triangulation;
            IsReady = triangulation.vertices != null && triangulation.vertices.Length > 0;
            IsBuilding = false;
            StatusText = IsReady
                ? $"Ready ({triangulation.vertices.Length} vertices)"
                : "Build completed without walkable polygons";
        }

        public void Clear()
        {
            if (IsBuilding)
            {
                return;
            }

            ClearRuntimeData();
            debugTriangulation = default;
            IsReady = false;
            StatusText = "Not built";
        }

        private void ClearRuntimeData()
        {
            if (surface == null)
            {
                surface = GetComponent<NavMeshSurface>();
            }

            surface.RemoveData();
            surface.navMeshData = null;

            if (runtimeData != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(runtimeData);
                }
                else
                {
                    DestroyImmediate(runtimeData);
                }
                runtimeData = null;
            }
        }

        private void OnDestroy()
        {
            if (!IsBuilding)
            {
                ClearRuntimeData();
            }
        }

        private void OnDrawGizmosSelected()
        {
            surface ??= GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = IsReady
                ? new Color(0.15f, 1f, 0.35f, 0.9f)
                : new Color(0.15f, 0.75f, 1f, 0.8f);
            Gizmos.DrawWireCube(surface.center, surface.size);

            if (IsReady &&
                debugTriangulation.vertices != null &&
                debugTriangulation.indices != null)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = new Color(0.15f, 1f, 0.35f, 0.45f);
                for (int index = 0;
                     index + 2 < debugTriangulation.indices.Length;
                     index += 3)
                {
                    Vector3 first = debugTriangulation.vertices[
                        debugTriangulation.indices[index]];
                    Vector3 second = debugTriangulation.vertices[
                        debugTriangulation.indices[index + 1]];
                    Vector3 third = debugTriangulation.vertices[
                        debugTriangulation.indices[index + 2]];
                    Gizmos.DrawLine(first, second);
                    Gizmos.DrawLine(second, third);
                    Gizmos.DrawLine(third, first);
                }
            }

            Gizmos.matrix = previousMatrix;
        }
    }
}
