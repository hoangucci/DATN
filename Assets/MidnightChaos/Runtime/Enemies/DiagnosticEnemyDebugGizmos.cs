using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MidnightChaos.Enemies
{
    public enum EnemyGizmoDisplayMode : byte
    {
        Off = 0,
        SelectedOnly = 1,
        Always = 2
    }

    [DisallowMultipleComponent]
    public sealed class DiagnosticEnemyDebugGizmos : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField, Tooltip(
            "Off hides everything, Selected Only draws for selected enemies, Always draws continuously.")]
        private EnemyGizmoDisplayMode displayMode =
            EnemyGizmoDisplayMode.SelectedOnly;

        [SerializeField] private bool showPatrolArea = true;
        [SerializeField] private bool showDetectionRanges = true;
        [SerializeField] private bool showAttackRange = true;
        [SerializeField, Tooltip(
            "Shows the most recently locked attack direction and target position.")]
        private bool showLockedAttack = true;
        [SerializeField] private bool showTargetAndCandidate = true;
        [SerializeField] private bool showLineOfSight = true;
        [SerializeField] private bool showCurrentDestination = true;
        [SerializeField] private bool showNavMeshPath = true;
        [SerializeField] private bool showAgentStatus = true;

        private DiagnosticMeleeEnemy enemy;

        private void Awake()
        {
            enemy = GetComponent<DiagnosticMeleeEnemy>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (displayMode == EnemyGizmoDisplayMode.Always)
            {
                DrawAll();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (displayMode == EnemyGizmoDisplayMode.SelectedOnly)
            {
                DrawAll();
            }
        }

        private void DrawAll()
        {
            enemy ??= GetComponent<DiagnosticMeleeEnemy>();
            if (enemy == null || enemy.Definition == null)
            {
                return;
            }

            EnemyDefinition definition = enemy.Definition;
            if (showPatrolArea)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(
                    enemy.PatrolCenter,
                    definition.PatrolRadius);
                Gizmos.DrawSphere(enemy.PatrolCenter, 0.18f);
            }

            if (showDetectionRanges)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, definition.DetectionRange);
                Gizmos.color = new Color(1f, 0.45f, 0f);
                Gizmos.DrawWireSphere(transform.position, definition.LoseTargetRange);
            }

            if (showAttackRange)
            {
                DrawAttackCone(enemy, definition);
            }

            if (showLockedAttack && enemy.HasLockedAttackSample)
            {
                Gizmos.color = enemy.LastAttackImpactHit
                    ? Color.green
                    : new Color(1f, 0.25f, 0.1f);
                float reach = definition.AttackReach *
                              (enemy.Evolution != null
                                  ? enemy.Evolution.AttackReachMultiplier
                                  : 1f);
                Vector3 direction = enemy.LockedAttackDirection.normalized;
                Gizmos.DrawLine(
                    transform.position,
                    transform.position + direction * reach);
                Gizmos.DrawWireSphere(
                    enemy.LockedAttackTargetPosition,
                    0.18f);
            }

            if (showTargetAndCandidate)
            {
                DrawTargetLine(enemy.TargetPlayer, Color.green);
                if (enemy.LastRetargetCandidate != enemy.TargetPlayer)
                {
                    DrawTargetLine(
                        enemy.LastRetargetCandidate,
                        new Color(0.8f, 0.2f, 1f));
                }
            }

            if (showLineOfSight && enemy.HasLineOfSightSample)
            {
                Gizmos.color = enemy.LastLineOfSightVisible
                    ? Color.green
                    : Color.red;
                Gizmos.DrawLine(
                    enemy.LastLineOfSightStart,
                    enemy.LastLineOfSightHitPoint);
                Gizmos.DrawSphere(enemy.LastLineOfSightHitPoint, 0.12f);
                if (!enemy.LastLineOfSightVisible)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
                    Gizmos.DrawLine(
                        enemy.LastLineOfSightHitPoint,
                        enemy.LastLineOfSightEnd);
                }
            }

            if (showCurrentDestination && enemy.HasCurrentDestination)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, enemy.CurrentDestination);
                Gizmos.DrawWireCube(
                    enemy.CurrentDestination,
                    Vector3.one * 0.35f);
            }

            if (showNavMeshPath)
            {
                DrawPath(enemy.Agent);
            }

            if (showAgentStatus)
            {
                bool valid = enemy.Agent != null &&
                             enemy.Agent.enabled &&
                             enemy.Agent.isOnNavMesh;
                Gizmos.color = valid ? Color.green : Color.magenta;
                Gizmos.DrawWireCube(
                    transform.position + Vector3.up * 0.25f,
                    Vector3.one * 0.35f);
                Handles.Label(
                    transform.position + Vector3.up * 2.2f,
                    $"{enemy.CurrentState} | NavMesh: " +
                    (valid ? "Ready" : "Invalid"));
            }
        }

        private void DrawTargetLine(
            Unity.Netcode.NetworkObject target,
            Color color)
        {
            if (target == null)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawLine(transform.position, target.transform.position);
            Gizmos.DrawSphere(target.transform.position + Vector3.up, 0.12f);
        }

        private static void DrawPath(NavMeshAgent agent)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            Vector3[] corners = agent.path.corners;
            if (corners == null || corners.Length == 0)
            {
                return;
            }

            Gizmos.color = new Color(0.1f, 0.45f, 1f);
            Vector3 previous = agent.transform.position;
            foreach (Vector3 corner in corners)
            {
                Gizmos.DrawLine(previous, corner);
                Gizmos.DrawSphere(corner, 0.1f);
                previous = corner;
            }
        }

        private static void DrawAttackCone(
            DiagnosticMeleeEnemy source,
            EnemyDefinition definition)
        {
            float reach = definition.AttackReach *
                          (source.Evolution != null
                              ? source.Evolution.AttackReachMultiplier
                              : 1f);
            Vector3 direction = source.transform.forward;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = Vector3.forward;
            }

            float halfAngle = definition.AttackConeHalfAngleDegrees;
            Vector3 left = Quaternion.AngleAxis(-halfAngle, Vector3.up) *
                           direction;
            Vector3 right = Quaternion.AngleAxis(halfAngle, Vector3.up) *
                            direction;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                source.transform.position,
                source.transform.position + left * reach);
            Gizmos.DrawLine(
                source.transform.position,
                source.transform.position + right * reach);
            Handles.color = Color.red;
            Handles.DrawWireArc(
                source.transform.position,
                Vector3.up,
                left,
                halfAngle * 2f,
                reach);
        }
#endif
    }
}
