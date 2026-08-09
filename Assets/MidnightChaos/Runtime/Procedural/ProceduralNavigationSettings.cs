using UnityEngine;

namespace MidnightChaos.Procedural
{
    [CreateAssetMenu(
        fileName = "ProceduralNavigationSettings",
        menuName = "Midnight Chaos/Procedural/Navigation Settings")]
    public sealed class ProceduralNavigationSettings : ScriptableObject
    {
        public const string ResourcePath =
            "Procedural/ProceduralNavigationSettings";

        [Tooltip("Agent Type ID dùng để build NavMeshSurface. Phải trùng Agent Type ID trên mọi enemy prefab được spawn.")]
        [SerializeField] private int navMeshAgentTypeId;
        [Tooltip("Chiều cao vùng thu thập source của runtime NavMesh, đặt đủ để bao toàn bộ địa hình.")]
        [SerializeField, Min(5f)] private float navMeshVolumeHeight = 40f;
        [Tooltip("Thời gian chờ sau khi build để NavMeshObstacle carving ổn định trước khi validate spawn point.")]
        [SerializeField, Range(0f, 2f)]
        private float navMeshCarvingSettleSeconds = 0.65f;
        [Tooltip("Bán kính tìm polygon NavMesh gần spawn point khi validate hoặc spawn runtime.")]
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 2.5f;

        public int NavMeshAgentTypeId => navMeshAgentTypeId;
        public float NavMeshVolumeHeight => Mathf.Max(5f, navMeshVolumeHeight);
        public float NavMeshCarvingSettleSeconds =>
            Mathf.Clamp(navMeshCarvingSettleSeconds, 0f, 2f);
        public float NavMeshSampleRadius => Mathf.Max(0.1f, navMeshSampleRadius);

        private void OnValidate()
        {
            navMeshVolumeHeight = NavMeshVolumeHeight;
            navMeshCarvingSettleSeconds = NavMeshCarvingSettleSeconds;
            navMeshSampleRadius = NavMeshSampleRadius;
        }
    }
}
