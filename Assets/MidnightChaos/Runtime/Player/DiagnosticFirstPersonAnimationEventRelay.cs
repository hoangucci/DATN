using UnityEngine;

namespace MidnightChaos.Player
{
    [DisallowMultipleComponent]
    public sealed class DiagnosticFirstPersonAnimationEventRelay : MonoBehaviour
    {
        // The original Muck clips call UseHitbox at 0.2666667 seconds. This
        // receiver intentionally performs no gameplay action: Midnight Chaos
        // keeps damage and harvesting Host-authoritative in DiagnosticMeleeCombat.
        public void UseHitbox()
        {
        }
    }
}
