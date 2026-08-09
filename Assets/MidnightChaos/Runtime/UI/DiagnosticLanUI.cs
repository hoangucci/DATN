using System.Net;
using System.Net.Sockets;
using MidnightChaos.Combat;
using MidnightChaos.Crafting;
using MidnightChaos.Enemies;
using MidnightChaos.Equipment;
using MidnightChaos.Inventory;
using MidnightChaos.Networking;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LanSessionController))]
    public sealed class DiagnosticLanUI : MonoBehaviour
    {
        private const float PanelWidth = 390f;
        private LanSessionController session;
        private NetworkManager networkManager;
        private DiagnosticEnemySpawner enemySpawner;
        private string hostAddress = "127.0.0.1";
        private string portText = LanEndpointValidator.DefaultPort.ToString();
        private string inlineError = string.Empty;

        [SerializeField] private bool autoStartSinglePlayer = true;

        private void Awake()
        {
            session = GetComponent<LanSessionController>();
            networkManager = GetComponent<NetworkManager>();
            enemySpawner = GetComponent<DiagnosticEnemySpawner>();
            portText = session.DefaultPort.ToString();
        }

        private void Start()
        {
            if (autoStartSinglePlayer && session != null && !session.IsSessionActive)
            {
                StartCoroutine(AutoStartRoutine());
            }
        }

        private System.Collections.IEnumerator AutoStartRoutine()
        {
            yield return new WaitForSeconds(0.1f);
            if (session != null && !session.IsSessionActive && !session.OperationInProgress)
            {
                Debug.Log("[DiagnosticLanUI] Tự động khởi chạy Single Player Host...");
                StartSinglePlayer();
            }
        }

        private void OnGUI()
        {
            Rect panel = new Rect(
                16f,
                16f,
                PanelWidth,
                session.IsSessionActive ? 500f : 300f);
            GUI.Box(panel, "Midnight Chaos - Gate G FPS Foundation");

            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 28f, panel.width - 28f, panel.height - 40f));
            GUILayout.Label(
                "Diagnostic only - camera local, gameplay vẫn do Host xác nhận.");
            GUILayout.Space(6f);
            GUILayout.Label($"Status: {session.StatusText}");

            if (session.IsSessionActive)
            {
                GUILayout.Label($"Mode: {(session.IsHost ? "Host" : "Client")}");
                GUILayout.Label($"Connected: {session.ConnectedPlayerCount}");
                GUILayout.Label($"Inventory: Wood {GetLocalWood()}");
                GUILayout.Label($"Equipment: Sword {(GetLocalHasSword() ? "YES" : "NO")}");
                GUILayout.Label($"World Chaos Shards: {GetVisibleShardCount()}");

                if (session.IsHost)
                {
                    GUILayout.Label($"Host IPv4: {FindLocalIpv4()}");
                    DrawEnemyHostControls();
                }

                GUILayout.Space(10f);
                if (GUILayout.Button("Disconnect", GUILayout.Height(34f)))
                {
                    session.Shutdown();
                }
            }
            else
            {
                GUILayout.Label("Host IPv4:");
                hostAddress = GUILayout.TextField(hostAddress);
                GUILayout.Label("UDP port:");
                portText = GUILayout.TextField(portText);
                GUILayout.Space(8f);

                GUI.enabled = !session.OperationInProgress;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Single Player", GUILayout.Height(34f)))
                {
                    StartSinglePlayer();
                }

                if (GUILayout.Button("Host LAN", GUILayout.Height(34f)))
                {
                    StartHost();
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Join LAN", GUILayout.Height(34f)))
                {
                    StartClient();
                }

                GUI.enabled = true;
                GUILayout.Space(6f);
                GUILayout.Label("Move: WASD | Sprint: Shift | Jump: Space");
            }

            if (session.IsSessionActive)
            {
                GUILayout.Label(
                    "Look: Mouse | Esc: thả chuột | Chuột phải: khóa lại");
                GUILayout.Label(
                    $"Attack/Harvest: F hoặc chuột trái | Damage: {GetLocalDamage()}");
                GUILayout.Label("Tree hợp lệ: -1 hit và +1 Wood");
                GUILayout.Label(
                    $"Craft Sword: E gần Workbench | Cost: {GetLocalSwordCost()} Wood");
                GUILayout.Label(
                    "Enemy: Small 66 HP | Mature 120 HP | Alpha 264 HP");
                GUILayout.Label(
                    "Evolution: Small cần 2 charge; Mature cần thêm 3");
                GUILayout.Label(
                    "Giết quái gần nhau; để sống con đang nhận C1/C2");
                GUILayout.Label(
                    "Alpha chết phải tạo đúng 1 Chaos Shard");
            }

            string error = string.IsNullOrWhiteSpace(inlineError)
                ? session.LastError
                : inlineError;

            if (!string.IsNullOrWhiteSpace(error))
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.45f, 0.45f);
                GUILayout.Label(error);
                GUI.color = previous;
            }

            GUILayout.EndArea();
        }

        private void DrawEnemyHostControls()
        {
            if (enemySpawner == null)
            {
                GUILayout.Label("Enemy controls unavailable: missing spawner.");
                return;
            }

            GUILayout.Label(
                $"Enemies: {enemySpawner.ActiveEnemyCount}/" +
                $"{enemySpawner.MaximumActiveEnemies} | " +
                $"Move: {(enemySpawner.MovementEnabled ? "ON" : "OFF")}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Enemy", GUILayout.Height(34f)))
            {
                inlineError = string.Empty;
                enemySpawner.TrySpawnEnemy(out inlineError);
            }
            string movementLabel = enemySpawner.MovementEnabled
                ? "Enemy Move: OFF"
                : "Enemy Move: ON";
            if (GUILayout.Button(movementLabel, GUILayout.Height(34f)))
            {
                inlineError = string.Empty;
                enemySpawner.TrySetMovementEnabled(
                    !enemySpawner.MovementEnabled,
                    out inlineError);
            }
            GUILayout.EndHorizontal();
        }

        private NetworkObject GetLocalPlayerObject()
        {
            if (networkManager == null ||
                networkManager.LocalClient == null)
            {
                return null;
            }

            return networkManager.LocalClient.PlayerObject;
        }

        private int GetLocalWood()
        {
            NetworkObject playerObject = GetLocalPlayerObject();
            if (playerObject == null)
            {
                return 0;
            }

            DiagnosticNetworkInventory inventory =
                playerObject.GetComponent<DiagnosticNetworkInventory>();

            return inventory != null ? inventory.Wood : 0;
        }

        private bool GetLocalHasSword()
        {
            NetworkObject playerObject = GetLocalPlayerObject();
            if (playerObject == null)
            {
                return false;
            }

            DiagnosticPlayerEquipment equipment =
                playerObject.GetComponent<DiagnosticPlayerEquipment>();

            return equipment != null && equipment.HasSword;
        }

        private int GetLocalDamage()
        {
            NetworkObject playerObject = GetLocalPlayerObject();
            if (playerObject == null)
            {
                return 0;
            }

            DiagnosticMeleeCombat combat =
                playerObject.GetComponent<DiagnosticMeleeCombat>();

            return combat != null ? combat.CurrentDamage : 0;
        }

        private int GetLocalSwordCost()
        {
            NetworkObject playerObject = GetLocalPlayerObject();
            if (playerObject == null)
            {
                return DiagnosticCraftingInteractor.DefaultSwordWoodCost;
            }

            DiagnosticCraftingInteractor crafting =
                playerObject.GetComponent<DiagnosticCraftingInteractor>();

            return crafting != null
                ? crafting.SwordWoodCost
                : DiagnosticCraftingInteractor.DefaultSwordWoodCost;
        }

        private static int GetVisibleShardCount()
        {
            return FindObjectsByType<DiagnosticChaosShard>(
                FindObjectsSortMode.None).Length;
        }

        private void StartSinglePlayer()
        {
            inlineError = string.Empty;
            session.StartSinglePlayer(out inlineError);
        }

        private void StartHost()
        {
            if (!TryGetPort(out ushort port))
            {
                return;
            }

            inlineError = string.Empty;
            session.StartHost(port, out inlineError);
        }

        private void StartClient()
        {
            if (!TryGetPort(out ushort port))
            {
                return;
            }

            inlineError = string.Empty;
            session.StartClient(hostAddress, port, out inlineError);
        }

        private bool TryGetPort(out ushort port)
        {
            if (LanEndpointValidator.TryValidatePort(portText, out port, out inlineError))
            {
                return true;
            }

            return false;
        }

        private static string FindLocalIpv4()
        {
            try
            {
                foreach (IPAddress address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(address))
                    {
                        return address.ToString();
                    }
                }
            }
            catch (SocketException)
            {
                // The bootstrap remains usable through manually entered IP.
            }

            return "Không tự phát hiện được - dùng ipconfig";
        }
    }
}
