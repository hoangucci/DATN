using MidnightChaos.Enemies;
using MidnightChaos.Inventory;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(DiagnosticChaosShard))]
    public sealed class DiagnosticWorldChaosShardLabel : MonoBehaviour
    {
        private NetworkObject networkObject;
        private DiagnosticWorldPickup pickup;
        private GUIStyle labelStyle;

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
            pickup = GetComponent<DiagnosticWorldPickup>();
        }

        private void OnGUI()
        {
            if (!networkObject.IsSpawned ||
                Camera.main == null ||
                pickup == null ||
                pickup.Item != VerticalSliceItemId.ChaosShard)
            {
                return;
            }

            Vector3 screenPoint = Camera.main.WorldToScreenPoint(
                transform.position + Vector3.up * 0.8f);

            if (screenPoint.z <= 0f)
            {
                return;
            }

            EnsureStyle();

            const float width = 260f;
            Rect labelRect = new Rect(
                screenPoint.x - width * 0.5f,
                Screen.height - screenPoint.y - 14f,
                width,
                28f);

            GUI.Label(
                labelRect,
                $"{pickup.Item} x{pickup.Amount}",
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
            labelStyle.normal.textColor =
                new Color(0.92f, 0.42f, 1f);
        }
    }
}
