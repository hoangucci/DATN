using MidnightChaos.Enemies;
using UnityEditor;
using UnityEngine;

namespace MidnightChaos.Editor
{
    [CustomEditor(typeof(ChaosEvolutionProfile))]
    public sealed class ChaosEvolutionProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "groupExtraEnemies");
            serializedObject.ApplyModifiedProperties();

            ChaosEvolutionProfile profile =
                (ChaosEvolutionProfile)target;
            int minimumGroupSize =
                profile.MinimumRequiredEnemyGroupSize;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gameplay Group", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int requestedGroupSize = EditorGUILayout.IntField(
                new GUIContent(
                    "Required Group Size",
                    "Số enemy trong một group. Giá trị được clamp để group " +
                    "luôn có đủ enemy tiến hóa tới Final Tier."),
                profile.RequiredEnemyGroupSize);
            if (EditorGUI.EndChangeCheck())
            {
                requestedGroupSize = Mathf.Max(
                    minimumGroupSize,
                    requestedGroupSize);
                serializedObject.Update();
                serializedObject.FindProperty("groupExtraEnemies").intValue =
                    requestedGroupSize - minimumGroupSize;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(profile);
            }
            EditorGUILayout.HelpBox(
                $"Minimum from tier thresholds: {minimumGroupSize}. " +
                $"Extra enemies: {profile.GroupExtraEnemies}. " +
                $"Final group size: {profile.RequiredEnemyGroupSize}. " +
                "Tune Charges To Next " +
                "Tier to change the minimum; edit Required Group Size above " +
                "to change the final size directly.",
                MessageType.Info);
        }
    }
}
