using MidnightChaos.Enemies;
using MidnightChaos.Inventory;
using UnityEditor;
using UnityEngine;

namespace MidnightChaos.Editor
{
    [CustomEditor(typeof(VerticalSliceGameplaySettings))]
    public sealed class VerticalSliceGameplaySettingsEditor :
        UnityEditor.Editor
    {
        private const string EvolutionProfilePath =
            "Assets/MidnightChaos/Resources/Procedural/" +
            "ChaosEvolutionProfile.asset";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            VerticalSliceGameplaySettings settings =
                (VerticalSliceGameplaySettings)target;
            ChaosEvolutionProfile profile =
                AssetDatabase.LoadAssetAtPath<ChaosEvolutionProfile>(
                    EvolutionProfilePath);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Resolved Gameplay Group Size",
                EditorStyles.boldLabel);
            if (profile == null)
            {
                EditorGUILayout.HelpBox(
                    "ChaosEvolutionProfile asset was not found; Auto group " +
                    "size cannot be previewed.",
                    MessageType.Warning);
                return;
            }

            int resolvedSize = settings.ResolveGameplayGroupSize(profile);
            EditorGUILayout.LabelField(
                $"{settings.GroupSizeMode}: {resolvedSize}");
            EditorGUILayout.LabelField(
                $"Evolution minimum: " +
                $"{profile.MinimumRequiredEnemyGroupSize}");

            if (settings.GroupSizeMode == GameplayGroupSizeMode.Manual &&
                settings.ManualGameplayGroupSize <
                profile.MinimumRequiredEnemyGroupSize)
            {
                EditorGUILayout.HelpBox(
                    "Manual Group Size is below the minimum required to " +
                    "guarantee Final Tier evolution. Runtime will still use " +
                    "the requested Manual value.",
                    MessageType.Warning);
            }
        }
    }
}
