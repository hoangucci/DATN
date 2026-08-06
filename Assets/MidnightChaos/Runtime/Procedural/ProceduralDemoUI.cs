using MidnightChaos.Networking;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralDemoUI : MonoBehaviour
    {
        private const float PanelWidth = 410f;

        private NetworkManager networkManager;
        private ProceduralWorldSettings settings;
        private ProceduralLanController lan;
        private ProceduralWorldCoordinator world;
        private ProceduralEnemySpawnManager enemies;
        private string hostAddress = "127.0.0.1";
        private string portText = "7777";
        private string inlineError = string.Empty;

        public void Initialize(
            NetworkManager configuredNetworkManager,
            ProceduralWorldSettings configuredSettings,
            ProceduralLanController configuredLan,
            ProceduralWorldCoordinator configuredWorld,
            ProceduralEnemySpawnManager configuredEnemies)
        {
            networkManager = configuredNetworkManager;
            settings = configuredSettings;
            lan = configuredLan;
            world = configuredWorld;
            enemies = configuredEnemies;
            portText = settings.DefaultPort.ToString();
        }

        private void OnGUI()
        {
            if (lan == null || world == null)
            {
                return;
            }

            float panelHeight = lan.IsSessionActive ? 520f : 285f;
            Rect panel = new Rect(16f, 16f, PanelWidth, panelHeight);
            GUI.Box(panel, "Procedural Generation + LAN Synchronization");

            GUILayout.BeginArea(
                new Rect(
                    panel.x + 14f,
                    panel.y + 28f,
                    panel.width - 28f,
                    panel.height - 40f));

            GUILayout.Label($"LAN: {lan.StatusText}");
            if (!lan.IsSessionActive)
            {
                DrawConnectionControls();
            }
            else
            {
                DrawWorldStatus();
                DrawHostControls();

                GUILayout.Space(8f);
                if (GUILayout.Button("Disconnect", GUILayout.Height(32f)))
                {
                    inlineError = string.Empty;
                    lan.Shutdown();
                }
            }

            string error = !string.IsNullOrWhiteSpace(inlineError)
                ? inlineError
                : !string.IsNullOrWhiteSpace(world.LastError)
                    ? world.LastError
                    : lan.LastError;
            if (!string.IsNullOrWhiteSpace(error))
            {
                GUILayout.Space(6f);
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.42f, 0.42f);
                GUILayout.Label(error);
                GUI.color = previous;
            }

            GUILayout.EndArea();
        }

        private void DrawConnectionControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Host IPv4:");
            hostAddress = GUILayout.TextField(hostAddress);
            GUILayout.Label("UDP port:");
            portText = GUILayout.TextField(portText);
            GUILayout.Space(8f);

            GUI.enabled = !lan.OperationInProgress;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Host", GUILayout.Height(36f)))
            {
                if (TryGetPort(out ushort port))
                {
                    inlineError = string.Empty;
                    lan.StartHost(port, out inlineError);
                }
            }
            if (GUILayout.Button("Join Client", GUILayout.Height(36f)))
            {
                if (TryGetPort(out ushort port))
                {
                    inlineError = string.Empty;
                    lan.StartClient(hostAddress, port, out inlineError);
                }
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Space(8f);
            GUILayout.Label("Scene demo không spawn Player.");
            GUILayout.Label("Host quyết định seed; Client chỉ dựng lại từ seed đó.");
        }

        private void DrawWorldStatus()
        {
            string role = networkManager.IsHost
                ? "Host"
                : networkManager.IsServer
                    ? "Server"
                    : "Client";

            GUILayout.Space(8f);
            GUILayout.Label($"Network Role: {role}");
            GUILayout.Label($"World State: {world.StatusText}");
            GUILayout.Label($"Seed: {world.CurrentSeed}");
            GUILayout.Label($"Revision: {world.Revision}");
            GUILayout.Label($"Objects: {world.GeneratedObjectCount}");
            GUILayout.Label(
                $"Trees: {world.GeneratedTreeCount} | " +
                $"Rocks: {world.GeneratedRockCount} | " +
                $"Ores: {world.GeneratedOreCount} | " +
                $"Vegetation: {world.GeneratedVegetationCount}");
            GUILayout.Label(
                $"Vegetation Render: instanced={world.InstancedVegetationCount}, " +
                $"GameObjects={world.GeneratedVegetationGameObjectCount}");
            GUILayout.Label(
                $"Chunks: {world.VisibleVegetationChunkCount}/" +
                $"{world.VegetationChunkCount} | " +
                $"draws: {world.SubmittedVegetationDrawCount}/" +
                $"{world.VegetationDrawBatchCount}");
            GUILayout.Label($"Enemies: {enemies.ActiveEnemyCount}");
            GUILayout.Label(
                $"Player Spawn Points: {world.ValidPlayerSpawnCount}/" +
                $"{world.PlannedPlayerSpawnCount} (markers only)");
            GUILayout.Label(
                $"Enemy Spawn Points: {world.ValidEnemySpawnCount}/" +
                $"{world.PlannedEnemySpawnCount}");
            GUILayout.Label($"Generation Time: {world.GenerationTimeSeconds:0.000}s");
            GUILayout.Label(
                networkManager.IsServer
                    ? $"NavMesh: {(world.IsWorldReady ? "Ready" : "Waiting")}" 
                    : "NavMesh: Host only");
            GUILayout.Label($"Layout Hash: {world.LayoutHash:X16}");
            if (!networkManager.IsServer)
            {
                GUILayout.Label(
                    $"Matches Host: {(world.LayoutMatchesHost ? "YES" : "NO")}");
            }
        }

        private void DrawHostControls()
        {
            GUILayout.Space(10f);
            bool canRecreate =
                networkManager.IsServer &&
                !world.IsGenerating;
            bool canSpawnEnemy = canRecreate && world.IsWorldReady;

            GUILayout.BeginHorizontal();
            GUI.enabled = canRecreate;
            if (GUILayout.Button("Recreate", GUILayout.Height(36f)))
            {
                inlineError = string.Empty;
                world.TryRecreate(out inlineError);
            }
            GUI.enabled = canSpawnEnemy;
            if (GUILayout.Button("Spawn Enemy", GUILayout.Height(36f)))
            {
                inlineError = string.Empty;
                enemies.TrySpawnEnemy(out inlineError);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            if (networkManager.IsServer)
            {
                GUILayout.Label(
                    $"Enemy cap: {enemies.ActiveEnemyCount}/" +
                    $"{settings.MaximumActiveEnemies}");
                GUILayout.Label(enemies.LastSpawnMessage);
            }
            else
            {
                GUILayout.Label("Recreate và Spawn Enemy chỉ khả dụng trên Host.");
            }
        }

        private bool TryGetPort(out ushort port)
        {
            return LanEndpointValidator.TryValidatePort(
                portText,
                out port,
                out inlineError);
        }
    }
}
