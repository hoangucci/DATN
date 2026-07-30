using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DiagnosticChaosShard : NetworkBehaviour
    {
    }
}
