using MidnightChaos.Crafting;
using UnityEngine;

namespace MidnightChaos.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DiagnosticCraftingStation))]
    public sealed class DiagnosticWorldCraftingLabel : MonoBehaviour
    {
        private GUIStyle labelStyle;

        private void OnGUI()
        {
            if (Camera.main == null)
            {
                return;
            }

            Vector3 screenPoint = Camera.main.WorldToScreenPoint(
                transform.position + Vector3.up * 1.2f);

            if (screenPoint.z <= 0f)
            {
                return;
            }

            EnsureStyle();

            const float width = 280f;
            Rect labelRect = new Rect(
                screenPoint.x - width * 0.5f,
                Screen.height - screenPoint.y - 14f,
                width,
                28f);

            GUI.Label(
                labelRect,
                $"Workbench | E | Sword: " +
                $"{DiagnosticCraftingInteractor.DefaultSwordWoodCost} Wood",
                labelStyle);
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
            labelStyle.normal.textColor = new Color(1f, 0.82f, 0.25f);
        }
    }
}
