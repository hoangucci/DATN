using System.Collections.Generic;
using MidnightChaos.Networking;
using MidnightChaos.Inventory;
using MidnightChaos.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralDemoUI : MonoBehaviour
    {
        private enum DebugDisplayMode
        {
            Minimal,
            Detailed,
            Diagnostics
        }

        private const float PanelWidth = 410f;
        private const float PanelMargin = 16f;
        private const float PanelContentInset = 14f;
        private const float PanelTitleHeight = 28f;
        private const float ActionBarHeight = 82f;

        [Header("Debug UI")]
        [Tooltip("Hiện hoặc ẩn toàn bộ procedural debug overlay. Không ảnh hưởng generation, LAN hoặc gameplay.")]
        [SerializeField] private bool showUI = true;
        [Tooltip("Đặt cấu hình section mặc định. Sau đó có thể bật hoặc tắt từng section thủ công.")]
        [SerializeField] private DebugDisplayMode displayMode =
            DebugDisplayMode.Minimal;

        [Header("Sections")]
        [SerializeField] private bool showNetwork = true;
        [SerializeField] private bool showWorld = true;
        [SerializeField] private bool showObjects;
        [SerializeField] private bool showGrassDiagnostics;
        [SerializeField] private bool showRendering;
        [SerializeField] private bool showSpawnPoints;
        [SerializeField] private bool showPerformance = true;
        [SerializeField] private bool showLayoutHash;
        [SerializeField, HideInInspector] private int appliedDisplayMode = -1;

        private NetworkManager networkManager;
        private VerticalSliceGameplaySettings gameplaySettings;
        private ChaosEvolutionProfile evolutionProfile;
        private ProceduralLanController lan;
        private ProceduralWorldCoordinator world;
        private ProceduralEnemySpawnManager enemies;
        private Vector2 statusScrollPosition;
        private string hostAddress = "127.0.0.1";
        private string portText = "7777";
        private string inlineError = string.Empty;

        public void Initialize(
            NetworkManager configuredNetworkManager,
            VerticalSliceGameplaySettings configuredGameplaySettings,
            ChaosEvolutionProfile configuredEvolutionProfile,
            ProceduralLanController configuredLan,
            ProceduralWorldCoordinator configuredWorld,
            ProceduralEnemySpawnManager configuredEnemies)
        {
            networkManager = configuredNetworkManager;
            gameplaySettings = configuredGameplaySettings;
            evolutionProfile = configuredEvolutionProfile;
            lan = configuredLan;
            world = configuredWorld;
            enemies = configuredEnemies;
            portText = lan.DefaultPort.ToString();
        }

        private void OnValidate()
        {
            if (appliedDisplayMode == (int)displayMode)
            {
                return;
            }

            ApplyDisplayModeDefaults();
            appliedDisplayMode = (int)displayMode;
            statusScrollPosition = Vector2.zero;
        }

        private void ApplyDisplayModeDefaults()
        {
            showNetwork = true;
            showWorld = true;
            showPerformance = true;

            switch (displayMode)
            {
                case DebugDisplayMode.Minimal:
                    showObjects = false;
                    showGrassDiagnostics = false;
                    showRendering = false;
                    showSpawnPoints = false;
                    showLayoutHash = false;
                    break;

                case DebugDisplayMode.Detailed:
                    showObjects = true;
                    showGrassDiagnostics = false;
                    showRendering = false;
                    showSpawnPoints = true;
                    showLayoutHash = true;
                    break;

                case DebugDisplayMode.Diagnostics:
                    showObjects = true;
                    showGrassDiagnostics = true;
                    showRendering = true;
                    showSpawnPoints = true;
                    showLayoutHash = true;
                    break;
            }
        }

        private void OnGUI()
        {
            if (!showUI || lan == null || world == null)
            {
                return;
            }

            bool sessionActive = lan.IsSessionActive;
            float panelWidth = Mathf.Min(
                PanelWidth,
                Mathf.Max(220f, Screen.width - PanelMargin * 2f));
            float preferredHeight = sessionActive
                ? GetPreferredActivePanelHeight()
                : 285f;
            float panelHeight = Mathf.Min(
                preferredHeight,
                Mathf.Max(220f, Screen.height - PanelMargin * 2f));
            Rect panel = new Rect(
                PanelMargin,
                PanelMargin,
                panelWidth,
                panelHeight);
            GUI.Box(panel, "Procedural Generation + LAN Synchronization");

            GUILayout.BeginArea(
                new Rect(
                    panel.x + PanelContentInset,
                    panel.y + PanelTitleHeight,
                    panel.width - PanelContentInset * 2f,
                    panel.height - PanelTitleHeight - 12f));

            if (sessionActive)
            {
                DrawActiveSession(panelHeight);
            }
            else
            {
                GUILayout.Label($"LAN: {lan.StatusText}");
                DrawConnectionControls();
                DrawInlineError();
            }

            GUILayout.EndArea();
        }

        private float GetPreferredActivePanelHeight()
        {
            return displayMode switch
            {
                DebugDisplayMode.Minimal => 390f,
                DebugDisplayMode.Detailed => 500f,
                DebugDisplayMode.Diagnostics => 560f,
                _ => 390f
            };
        }

        private void DrawActiveSession(float panelHeight)
        {
            float contentHeight =
                panelHeight - PanelTitleHeight - 12f;
            float statusHeight = Mathf.Max(
                60f,
                contentHeight - ActionBarHeight);

            statusScrollPosition = GUILayout.BeginScrollView(
                statusScrollPosition,
                false,
                false,
                GUILayout.Height(statusHeight));
            DrawVisibleStatus();
            DrawInlineError();
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            DrawHostControls();
            GUILayout.Space(6f);
            if (GUILayout.Button("Disconnect", GUILayout.Height(32f)))
            {
                inlineError = string.Empty;
                lan.Shutdown();
            }
        }

        private void DrawVisibleStatus()
        {
            bool hasPreviousSection = false;
            if (showNetwork)
            {
                DrawSectionSpacing(ref hasPreviousSection);
                DrawNetworkSection();
            }
            if (showWorld)
            {
                DrawSectionSpacing(ref hasPreviousSection);
                DrawWorldSection();
            }
            if (showObjects)
            {
                DrawSectionSpacing(ref hasPreviousSection);
                DrawObjectSection();
            }
            if (showGrassDiagnostics)
            {
                DrawSectionSpacing(ref hasPreviousSection);
                DrawGrassSection();
            }
            if (showRendering)
            {
                DrawSectionSpacing(ref hasPreviousSection);
                DrawRenderingSection();
            }
            if (showSpawnPoints)
            {
                DrawSectionSpacing(ref hasPreviousSection);
                DrawSpawnPointSection();
            }
            if (showPerformance)
            {
                DrawSectionSpacing(ref hasPreviousSection);
                DrawPerformanceSection();
            }
            if (showLayoutHash)
            {
                DrawSectionSpacing(ref hasPreviousSection);
                DrawLayoutHashSection();
            }
        }

        private static void DrawSectionSpacing(ref bool hasPreviousSection)
        {
            if (hasPreviousSection)
            {
                GUILayout.Space(6f);
            }
            hasPreviousSection = true;
        }

        private void DrawNetworkSection()
        {
            string role = networkManager.IsHost
                ? "Host"
                : networkManager.IsServer
                    ? "Server"
                    : "Client";
            GUILayout.Label($"LAN: {lan.StatusText}");
            GUILayout.Label($"Network Role: {role}");
        }

        private void DrawWorldSection()
        {
            GUILayout.Label($"World State: {world.StatusText}");
            GUILayout.Label($"Seed: {world.CurrentSeed}");
            if (displayMode != DebugDisplayMode.Minimal)
            {
                GUILayout.Label($"Revision: {world.Revision}");
            }
            GUILayout.Label($"Network Enemies: {enemies.ActiveEnemyCount}");
            if (networkManager.IsServer &&
                displayMode == DebugDisplayMode.Minimal)
            {
                GUILayout.Label(
                    $"Groups Active: {enemies.ActiveGameplayGroupCount}/" +
                    $"{enemies.TotalGameplayGroupCount} | " +
                    $"Gameplay Enemies Active: " +
                    $"{enemies.ActiveGameplayEnemyCount}");
            }
            else if (networkManager.IsServer)
            {
                GUILayout.Label(
                    $"Groups: Total {enemies.TotalGameplayGroupCount} | " +
                    $"Dormant {enemies.DormantGameplayGroupCount} | " +
                    $"Active {enemies.ActiveGameplayGroupCount} | " +
                    $"Suspended {enemies.SuspendedGameplayGroupCount} | " +
                    $"Completed {enemies.CompletedGameplayGroupCount}");
                string sizeMode = enemies.GroupSizeMode ==
                                  GameplayGroupSizeMode.Auto
                    ? "Auto"
                    : "Manual";
                GUILayout.Label(
                    $"Group Size: {sizeMode}: " +
                    $"{enemies.ResolvedGameplayGroupSize} | " +
                    $"Max Active Groups: " +
                    $"{gameplaySettings.MaximumActiveGroups}");
                GUILayout.Label(
                    $"Gameplay Enemies: Active " +
                    $"{enemies.ActiveGameplayEnemyCount} | " +
                    $"Suspended {enemies.SuspendedGameplayEnemyCount} | " +
                    $"Alive Total {enemies.AliveGameplayEnemyCount}");
                GUILayout.Label(
                    $"Debug Enemy Limit: " +
                    $"{gameplaySettings.MaximumActiveEnemies}");
            }
            GUILayout.Label(
                networkManager.IsServer
                    ? $"NavMesh: {(world.IsWorldReady ? "Ready" : "Waiting")}"
                    : "NavMesh: Host only");

            if (networkManager.IsServer &&
                !string.IsNullOrWhiteSpace(enemies.LastSpawnMessage))
            {
                GUILayout.Label(enemies.LastSpawnMessage);
            }
            else if (!networkManager.IsServer)
            {
                GUILayout.Label(
                    "Recreate và Spawn Enemy chỉ khả dụng trên Host.");
            }
        }

        private void DrawObjectSection()
        {
            GUILayout.Label($"Objects: {world.GeneratedObjectCount}");
            GUILayout.Label(
                $"Trees: {world.GeneratedTreeCount} | " +
                $"Rocks: {world.GeneratedRockCount} | " +
                $"Ores: {world.GeneratedOreCount}");
            GUILayout.Label(
                $"Vegetation: {world.GeneratedVegetationCount} | " +
                $"Grass: {world.GeneratedGrassCount}");
        }

        private void DrawGrassSection()
        {
            GUILayout.Label($"Grass Clusters: {world.GrassClusterCount}");
            GUILayout.Label(
                $"Target: {world.GrassTargetCount} | " +
                $"Placed: {world.GrassSuccessfullyPlacedCount} | " +
                $"Rejected: {world.GrassRejectedPlacementCount}");
            foreach (KeyValuePair<string, int> pair in
                     world.GrassClusterCountsByStableId)
            {
                GUILayout.Label($"  {pair.Key} Clusters: {pair.Value}");
            }
        }

        private void DrawRenderingSection()
        {
            GUILayout.Label(
                $"Plant Render: instanced={world.InstancedVegetationCount}");
            GUILayout.Label(
                $"Vegetation GOs={world.GeneratedVegetationGameObjectCount} | " +
                $"Grass GOs={world.GeneratedGrassGameObjectCount}");
            GUILayout.Label(
                $"Chunks: {world.VisibleVegetationChunkCount}/" +
                $"{world.VegetationChunkCount} | " +
                $"draws: {world.SubmittedVegetationDrawCount}/" +
                $"{world.VegetationDrawBatchCount}");
        }

        private void DrawSpawnPointSection()
        {
            GUILayout.Label(
                $"Player Spawn Points: {world.ValidPlayerSpawnCount}/" +
                $"{world.PlannedPlayerSpawnCount} (markers only)");
            GUILayout.Label(
                $"Enemy Spawn Points: {world.ValidEnemySpawnCount}/" +
                $"{world.PlannedEnemySpawnCount}");
        }

        private void DrawPerformanceSection()
        {
            GUILayout.Label(
                $"Generation Time: {world.GenerationTimeSeconds:0.000}s");
        }

        private void DrawLayoutHashSection()
        {
            GUILayout.Label($"Layout Hash: {world.LayoutHash:X16}");
            if (!networkManager.IsServer)
            {
                GUILayout.Label(
                    $"Matches Host: {(world.LayoutMatchesHost ? "YES" : "NO")}");
            }
        }

        private void DrawConnectionControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Host IPv4:");
            hostAddress = GUILayout.TextField(hostAddress);
            GUILayout.Label("UDP port:");
            portText = GUILayout.TextField(portText);
            GUILayout.Space(8f);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !lan.OperationInProgress;
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
            GUI.enabled = previousEnabled;
            GUILayout.Space(8f);
            GUILayout.Label("Scene demo không spawn Player.");
            GUILayout.Label("Host quyết định seed; Client chỉ dựng lại từ seed đó.");
        }

        private void DrawHostControls()
        {
            bool canRecreate = networkManager.IsServer && !world.IsGenerating;
            bool canSpawnEnemy = canRecreate && world.IsWorldReady;
            bool previousEnabled = GUI.enabled;

            GUILayout.BeginHorizontal();
            GUI.enabled = previousEnabled && canRecreate;
            if (GUILayout.Button("Recreate", GUILayout.Height(36f)))
            {
                inlineError = string.Empty;
                world.TryRecreate(out inlineError);
            }
            GUI.enabled = previousEnabled && canSpawnEnemy;
            if (GUILayout.Button("Spawn Enemy", GUILayout.Height(36f)))
            {
                inlineError = string.Empty;
                enemies.TrySpawnEnemy(out inlineError);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;
        }

        private void DrawInlineError()
        {
            string error = !string.IsNullOrWhiteSpace(inlineError)
                ? inlineError
                : !string.IsNullOrWhiteSpace(world.LastError)
                    ? world.LastError
                    : lan.LastError;
            if (string.IsNullOrWhiteSpace(error))
            {
                return;
            }

            GUILayout.Space(6f);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.42f, 0.42f);
            GUILayout.Label(error);
            GUI.color = previous;
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
