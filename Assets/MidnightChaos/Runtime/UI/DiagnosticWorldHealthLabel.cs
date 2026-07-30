using MidnightChaos.Combat;
using MidnightChaos.Enemies;
using MidnightChaos.Player;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkHealth))]
    public sealed class DiagnosticWorldHealthLabel : MonoBehaviour
    {
        private NetworkObject networkObject;
        private NetworkHealth health;
        private DiagnosticNetworkPlayer player;
        private DiagnosticMeleeEnemy enemy;
        private DiagnosticEnemyEvolution evolution;
        private GUIStyle labelStyle;

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
            health = GetComponent<NetworkHealth>();
            player = GetComponent<DiagnosticNetworkPlayer>();
            enemy = GetComponent<DiagnosticMeleeEnemy>();
            evolution = GetComponent<DiagnosticEnemyEvolution>();
        }

        private void OnGUI()
        {
            if (!networkObject.IsSpawned || Camera.main == null)
            {
                return;
            }

            float heightOffset = evolution != null
                ? evolution.WorldLabelHeightOffset
                : 1.5f;
            Vector3 screenPoint = Camera.main.WorldToScreenPoint(
                transform.position + Vector3.up * heightOffset);

            if (screenPoint.z <= 0f)
            {
                return;
            }

            EnsureStyle();
            labelStyle.normal.textColor = health.IsDead
                ? new Color(1f, 0.35f, 0.35f)
                : Color.white;

            const float width = 300f;
            Rect labelRect = new Rect(
                screenPoint.x - width * 0.5f,
                Screen.height - screenPoint.y - 14f,
                width,
                28f);

            string subject = player != null
                ? $"P{networkObject.OwnerClientId}"
                : health.DisplayName;

            string state = enemy != null
                ? $" [{enemy.CurrentState}]"
                : string.Empty;

            string evolutionState = evolution != null
                ? evolution.CurrentStage == DiagnosticEnemyStage.Alpha
                    ? " [Alpha]"
                    : $" [{evolution.CurrentStage} " +
                      $"C{evolution.CurrentCharge}/" +
                      $"{evolution.ChargeRequirement}]"
                : string.Empty;

            GUI.Label(
                labelRect,
                $"{subject}{evolutionState}{state}  " +
                $"HP {health.CurrentHealth}/{health.MaxHealth}",
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
        }
    }
}
