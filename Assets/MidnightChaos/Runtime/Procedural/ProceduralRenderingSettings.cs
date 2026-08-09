using UnityEngine;

namespace MidnightChaos.Procedural
{
    [CreateAssetMenu(
        fileName = "ProceduralRenderingSettings",
        menuName = "Midnight Chaos/Procedural/Rendering Settings")]
    public sealed class ProceduralRenderingSettings : ScriptableObject
    {
        public const string ResourcePath =
            "Procedural/ProceduralRenderingSettings";

        [Header("Spawn Debug Visualization")]
        [Tooltip("Hiện marker của các điểm spawn đã generate. Chỉ ảnh hưởng hiển thị local.")]
        [SerializeField] private bool showSpawnMarkers = true;
        [Tooltip("Kích thước marker debug của spawn point.")]
        [SerializeField, Min(0.05f)] private float spawnMarkerScale = 0.65f;

        [Header("Rendering Performance (Local Only)")]
        [Tooltip("Dùng Camera.layerCullDistances cho các layer procedural. Chỉ thay đổi hiển thị local, không thay đổi layout, physics hoặc Layout Hash.")]
        [SerializeField] private bool useLayerDistanceCulling = true;
        [Tooltip("Far Clip Plane áp dụng cho camera procedural. Với map lớn phải giữ đủ để terrain không bị cắt.")]
        [SerializeField, Min(50f)] private float cameraFarClipPlane = 1000f;
        [Tooltip("Khoảng render tối đa của vegetation/flower.")]
        [SerializeField, Min(1f)] private float vegetationCullDistance = 55f;
        [Tooltip("0 giữ nguyên LODGroup cull threshold của prefab. Giá trị nhỏ như 0.001 giúp vegetation nhỏ vẫn hiện.")]
        [SerializeField, Range(0f, 0.2f)]
        private float vegetationLodCullScreenHeightOverride = 0.001f;
        [Tooltip("Khoảng đổi từ LOD0 sang LOD thấp của vegetation instanced.")]
        [SerializeField, Min(1f)] private float vegetationLodSwitchDistance = 28f;
        [Tooltip("Khoảng render tối đa của Grass GPU-instanced.")]
        [SerializeField, Min(1f)] private float grassCullDistance = 45f;
        [Tooltip("0 giữ nguyên LODGroup cull threshold của Grass prefab.")]
        [SerializeField, Range(0f, 0.2f)]
        private float grassLodCullScreenHeightOverride = 0.001f;
        [Tooltip("Khoảng đổi từ LOD0 sang LOD thấp của Grass instanced.")]
        [SerializeField, Min(1f)] private float grassLodSwitchDistance = 6f;
        [Tooltip("Khoảng render tối đa của cây.")]
        [SerializeField, Min(1f)] private float treeCullDistance = 200f;
        [Tooltip("Khoảng render tối đa cho prop nhỏ.")]
        [SerializeField, Min(1f)] private float smallPropCullDistance = 90f;
        [Tooltip("Khoảng render tối đa của đá và quặng tương tác.")]
        [SerializeField, Min(1f)] private float resourceCullDistance = 130f;
        [Tooltip("Render vegetation và Grass bằng GPU instancing theo chunk, không tạo một GameObject cho mỗi cây cỏ/hoa.")]
        [SerializeField] private bool useInstancedVegetation = true;
        [Tooltip("Kích thước ô dùng để distance-cull vegetation theo nhóm. Giá trị nhỏ giảm overdraw nhưng tăng số draw group.")]
        [SerializeField, Range(8f, 64f)] private float vegetationChunkSize = 24f;
        [Tooltip("Tắt cast/receive shadow trên vegetation trang trí.")]
        [SerializeField] private bool disableVegetationShadows = true;
        [Tooltip("Tắt cast/receive shadow trên Grass trang trí. Độc lập với Vegetation.")]
        [SerializeField] private bool disableGrassShadows = true;
        [Tooltip("Bật ParticleSystem lá trên từng tree prefab. 2.000 cây tương đương 2.000 particle simulations.")]
        [SerializeField] private bool enableTreeParticles;

        public bool ShowSpawnMarkers => showSpawnMarkers;
        public float SpawnMarkerScale => Mathf.Max(0.05f, spawnMarkerScale);
        public bool UseLayerDistanceCulling => useLayerDistanceCulling;
        public float CameraFarClipPlane => Mathf.Max(50f, cameraFarClipPlane);
        public float VegetationCullDistance =>
            Mathf.Max(1f, vegetationCullDistance);
        public float VegetationLodCullScreenHeightOverride => Mathf.Clamp(
            vegetationLodCullScreenHeightOverride,
            0f,
            0.2f);
        public float VegetationLodSwitchDistance => Mathf.Clamp(
            vegetationLodSwitchDistance,
            1f,
            VegetationCullDistance);
        public float GrassCullDistance => Mathf.Max(1f, grassCullDistance);
        public float GrassLodCullScreenHeightOverride => Mathf.Clamp(
            grassLodCullScreenHeightOverride,
            0f,
            0.2f);

        public float GetLodCullScreenHeightOverride(
            MidnightChaos.World.WorldObjectCategory category)
        {
            return category switch
            {
                MidnightChaos.World.WorldObjectCategory.Vegetation =>
                    VegetationLodCullScreenHeightOverride,
                MidnightChaos.World.WorldObjectCategory.Grass =>
                    GrassLodCullScreenHeightOverride,
                _ => 0f
            };
        }
        public float GrassLodSwitchDistance => Mathf.Clamp(
            grassLodSwitchDistance,
            1f,
            GrassCullDistance);
        public float TreeCullDistance => Mathf.Max(1f, treeCullDistance);
        public float SmallPropCullDistance =>
            Mathf.Max(1f, smallPropCullDistance);
        public float ResourceCullDistance =>
            Mathf.Max(1f, resourceCullDistance);
        public bool UseInstancedVegetation => useInstancedVegetation;
        public float VegetationChunkSize => Mathf.Clamp(
            vegetationChunkSize,
            8f,
            64f);
        public bool DisableVegetationShadows => disableVegetationShadows;
        public bool DisableGrassShadows => disableGrassShadows;
        public bool EnableTreeParticles => enableTreeParticles;

        private void OnValidate()
        {
            spawnMarkerScale = SpawnMarkerScale;
            cameraFarClipPlane = CameraFarClipPlane;
            vegetationCullDistance = VegetationCullDistance;
            vegetationLodCullScreenHeightOverride =
                VegetationLodCullScreenHeightOverride;
            vegetationLodSwitchDistance = VegetationLodSwitchDistance;
            grassCullDistance = GrassCullDistance;
            grassLodCullScreenHeightOverride =
                GrassLodCullScreenHeightOverride;
            grassLodSwitchDistance = GrassLodSwitchDistance;
            treeCullDistance = TreeCullDistance;
            smallPropCullDistance = SmallPropCullDistance;
            resourceCullDistance = ResourceCullDistance;
            vegetationChunkSize = VegetationChunkSize;
        }
    }
}
