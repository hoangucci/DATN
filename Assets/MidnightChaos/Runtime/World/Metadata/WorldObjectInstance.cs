using UnityEngine;

namespace MidnightChaos.World
{
    /// <summary>
    /// Immutable deterministic layout data. This is deliberately not a
    /// MonoBehaviour, so GPU-instanced vegetation does not create GameObjects.
    /// </summary>
    public readonly struct WorldObjectInstance
    {
        public WorldObjectInstance(
            WorldObjectDefinition definition,
            int layoutIndex,
            Vector3 position,
            Vector3 eulerAngles,
            float uniformScale)
        {
            Definition = definition;
            LayoutIndex = layoutIndex;
            Position = position;
            EulerAngles = eulerAngles;
            UniformScale = uniformScale;
        }

        public WorldObjectDefinition Definition { get; }
        public string StableDefinitionId =>
            Definition == null ? string.Empty : Definition.StableId;
        public WorldObjectCategory Category => Definition.Category;
        public int LayoutIndex { get; }
        public Vector3 Position { get; }
        public Vector3 EulerAngles { get; }
        public float UniformScale { get; }
    }
}
