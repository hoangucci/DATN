using MidnightChaos.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace MidnightChaos.Procedural.Tests
{
    public sealed class EnemyDefinitionTests
    {
        [Test]
        public void EnemyStateValuesRemainSerializationCompatible()
        {
            Assert.That((byte)DiagnosticEnemyState.Patrol, Is.EqualTo(0));
            Assert.That((byte)DiagnosticEnemyState.Chase, Is.EqualTo(1));
            Assert.That((byte)DiagnosticEnemyState.Attack, Is.EqualTo(2));
            Assert.That((byte)DiagnosticEnemyState.Recover, Is.EqualTo(3));
            Assert.That((byte)DiagnosticEnemyState.Dead, Is.EqualTo(4));
        }

        [Test]
        public void DiagnosticDefaultsMatchDayThreeContract()
        {
            EnemyDefinition definition =
                ScriptableObject.CreateInstance<EnemyDefinition>();
            GameObject visual = new GameObject("Visual");

            try
            {
                definition.ConfigureForDiagnostics(visual);

                Assert.That(definition.StableId, Is.EqualTo("fire_mage_melee"));
                Assert.That(definition.VisualPrefab, Is.SameAs(visual));
                Assert.That(definition.PatrolRadius, Is.EqualTo(15f));
                Assert.That(definition.PatrolSpeed, Is.EqualTo(2f));
                Assert.That(definition.MinimumPatrolWait, Is.EqualTo(1f));
                Assert.That(definition.MaximumPatrolWait, Is.EqualTo(3f));
                Assert.That(definition.DetectionRange, Is.EqualTo(15f));
                Assert.That(definition.LoseTargetRange, Is.EqualTo(22f));
                Assert.That(definition.ChaseSpeed, Is.EqualTo(3.5f));
                Assert.That(definition.RepathInterval, Is.EqualTo(0.3f));
                Assert.That(
                    definition.AttackConeHalfAngleDegrees,
                    Is.EqualTo(50f));
                Assert.That(
                    definition.AttackImpactDelaySeconds,
                    Is.EqualTo(0.35f));
                Assert.That(definition.AttackPoseSeconds, Is.EqualTo(0.65f));
                Assert.That(
                    definition.AttackAnimationState,
                    Is.EqualTo("Slap Attack"));
                Assert.That(
                    definition.SpawnAnimationState,
                    Is.EqualTo("Spawn"));
                Assert.That(
                    definition.HitAnimationState,
                    Is.EqualTo("Take Damage"));
                Assert.That(
                    definition.DeathAnimationState,
                    Is.EqualTo("Die"));
                Assert.That(definition.HitReactionSeconds, Is.EqualTo(0.3f));
                Assert.That(
                    definition.HitVisualCooldownSeconds,
                    Is.EqualTo(0.25f));
                Assert.That(
                    definition.DeathPresentationSeconds,
                    Is.EqualTo(1.7f));
                Assert.That(
                    definition.SpawnPresentationSeconds,
                    Is.EqualTo(0.85f));
            }
            finally
            {
                Object.DestroyImmediate(visual);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void VisualRefreshDoesNotReplaceAuthoredVisual()
        {
            EnemyDefinition definition =
                ScriptableObject.CreateInstance<EnemyDefinition>();
            GameObject authored = new GameObject("Authored");
            GameObject replacement = new GameObject("Replacement");

            try
            {
                definition.ConfigureForDiagnostics(authored);
                definition.ConfigureVisualIfMissing(replacement);

                Assert.That(definition.VisualPrefab, Is.SameAs(authored));
            }
            finally
            {
                Object.DestroyImmediate(authored);
                Object.DestroyImmediate(replacement);
                Object.DestroyImmediate(definition);
            }
        }
    }
}
