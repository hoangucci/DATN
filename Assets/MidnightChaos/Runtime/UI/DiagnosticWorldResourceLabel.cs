using MidnightChaos.Resources;
using UnityEngine;

namespace MidnightChaos.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DiagnosticResourceNode))]
    public sealed class DiagnosticWorldResourceLabel : MonoBehaviour
    {
        private DiagnosticResourceNode resourceNode;
        private GUIStyle labelStyle;

        private void Awake()
        {
            resourceNode = GetComponent<DiagnosticResourceNode>();
        }

        private void OnGUI()
        {
            if (!resourceNode.IsSpawned || Camera.main == null)
            {
                return;
            }

            Vector3 screenPoint = Camera.main.WorldToScreenPoint(
                transform.position + Vector3.up * 2.2f);

            if (screenPoint.z <= 0f)
            {
                return;
            }

            EnsureStyle();
            labelStyle.normal.textColor = resourceNode.IsDepleted
                ? new Color(0.6f, 0.6f, 0.6f)
                : new Color(0.55f, 1f, 0.55f);

            const float width = 210f;
            Rect labelRect = new Rect(
                screenPoint.x - width * 0.5f,
                Screen.height - screenPoint.y - 14f,
                width,
                28f);

            string text = resourceNode.IsDepleted
                ? "Tree - DEPLETED"
                : $"Tree {resourceNode.RemainingHits}/{resourceNode.MaximumHits} | LMB/F";

            GUI.Label(labelRect, text, labelStyle);
        }

        private void EnsureStyle()
        {
            if (labelStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 15
            };
        }
    }
}
