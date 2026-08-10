using System;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using MidnightChaos.Enemies;
using MidnightChaos.Inventory;
using MidnightChaos.Player;
using MidnightChaos.Resources;

namespace MidnightChaos.Procedural
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class ProceduralDemoBootstrap : MonoBehaviour
    {
        private const string SettingsResourcePath =
            "Procedural/ProceduralWorldSettings";
        private static bool fallbackWarningLogged;

        [SerializeField] private ProceduralWorldSettings settings;
        [SerializeField] private ProceduralRenderingSettings renderingSettings;
        [SerializeField] private ProceduralNavigationSettings navigationSettings;
        [SerializeField] private VerticalSliceGameplaySettings gameplaySettings;
        [SerializeField] private ChaosEvolutionProfile evolutionProfile;
        [SerializeField, Tooltip(
            "Enable only in ProceduralCombatDemo. ProceduralDemo remains a no-player generation showcase.")]
        private bool enablePlayers;
        [SerializeField, Tooltip(
            "Network player prefab registered and spawned only after the Host world/NavMesh is ready.")]
        private GameObject playerPrefab;
        [SerializeField, Tooltip(
            "Network Chaos Shard prefab used by Enemy Evolution when an Alpha dies.")]
        private GameObject chaosShardPrefab;

        private void Awake()
        {
            ResolveSettings();
            if (settings == null || renderingSettings == null ||
                navigationSettings == null || gameplaySettings == null ||
                evolutionProfile == null)
            {
                Debug.LogError(
                    "[Procedural] Missing one or more Resources/Procedural " +
                    "config assets (World, Rendering, Navigation, Gameplay, " +
                    "Chaos Evolution).",
                    this);
                enabled = false;
                return;
            }

            ConfigurePrefabDependencies();

            GameObject networkRoot = gameObject;
            networkRoot.name = "NetworkManager";
            NetworkManager networkManager =
                GetOrAdd<NetworkManager>(networkRoot);
            UnityTransport transport = GetOrAdd<UnityTransport>(networkRoot);

            ConfigureNetworkManager(networkManager, transport);
            DiagnosticChaosEvolutionService evolutionService =
                GetOrAdd<DiagnosticChaosEvolutionService>(networkRoot);
            evolutionService.Configure(
                gameplaySettings,
                evolutionProfile,
                chaosShardPrefab);

            GameObject generatorObject = FindOrCreate(
                "ProceduralWorldGenerator");
            ProceduralWorldGenerator generator =
                GetOrAdd<ProceduralWorldGenerator>(generatorObject);

            GameObject navMeshObject = FindOrCreate("RuntimeNavMeshBuilder");
            GetOrAdd<NavMeshSurface>(navMeshObject);
            RuntimeNavMeshBuilder navMeshBuilder =
                GetOrAdd<RuntimeNavMeshBuilder>(navMeshObject);
            navMeshBuilder.Initialize(settings, navigationSettings);

            GameObject playerSpawnObject = FindOrCreate("PlayerSpawnManager");
            ProceduralSpawnPointRegistry spawnPoints =
                GetOrAdd<ProceduralSpawnPointRegistry>(playerSpawnObject);

            GameObject enemySpawnObject = FindOrCreate("EnemySpawnManager");
            ProceduralEnemySpawnManager enemySpawnManager =
                GetOrAdd<ProceduralEnemySpawnManager>(enemySpawnObject);
            enemySpawnManager.Initialize(
                networkManager,
                gameplaySettings,
                evolutionProfile,
                navigationSettings,
                spawnPoints,
                navMeshBuilder);

            ProceduralWorldCoordinator world =
                GetOrAdd<ProceduralWorldCoordinator>(networkRoot);
            world.Initialize(
                networkManager,
                settings,
                renderingSettings,
                gameplaySettings,
                navigationSettings,
                generator,
                navMeshBuilder,
                spawnPoints,
                enemySpawnManager);

            if (enablePlayers)
            {
                ProceduralPlayerSpawnManager playerSpawnManager =
                    GetOrAdd<ProceduralPlayerSpawnManager>(playerSpawnObject);
                playerSpawnManager.Initialize(
                    networkManager,
                    navigationSettings,
                    spawnPoints,
                    world,
                    playerPrefab);

                ProceduralVerticalSliceController verticalSlice =
                    GetOrAdd<ProceduralVerticalSliceController>(networkRoot);
                verticalSlice.Initialize(
                    networkManager,
                    settings,
                    gameplaySettings,
                    world,
                    enemySpawnManager);
            }

            ProceduralLanController lan =
                GetOrAdd<ProceduralLanController>(networkRoot);
            lan.Initialize(networkManager, transport);

            GameObject uiObject = FindOrCreate("UI Debug");
            ProceduralDemoUI ui = GetOrAdd<ProceduralDemoUI>(uiObject);
            ui.Initialize(
                networkManager,
                gameplaySettings,
                evolutionProfile,
                lan,
                world,
                enemySpawnManager);

            ConfigureFallbackCamera(
                FindOrCreate("Camera fallback"),
                renderingSettings,
                enablePlayers);
            ConfigureDirectionalLight(FindOrCreate("Directional Light"));
        }

        private void ResolveSettings()
        {
            bool usedFallback = false;
            if (settings == null)
            {
                settings = UnityEngine.Resources.Load<ProceduralWorldSettings>(
                    SettingsResourcePath);
                usedFallback = true;
            }
            if (renderingSettings == null)
            {
                renderingSettings =
                    UnityEngine.Resources.Load<ProceduralRenderingSettings>(
                        ProceduralRenderingSettings.ResourcePath);
                usedFallback = true;
            }
            if (navigationSettings == null)
            {
                navigationSettings =
                    UnityEngine.Resources.Load<ProceduralNavigationSettings>(
                        ProceduralNavigationSettings.ResourcePath);
                usedFallback = true;
            }
            if (gameplaySettings == null)
            {
                gameplaySettings =
                    UnityEngine.Resources.Load<VerticalSliceGameplaySettings>(
                        VerticalSliceGameplaySettings.ResourcePath);
                usedFallback = true;
            }
            if (evolutionProfile == null)
            {
                evolutionProfile =
                    UnityEngine.Resources.Load<ChaosEvolutionProfile>(
                        ChaosEvolutionProfile.ResourcePath);
                usedFallback = true;
            }
            if (!usedFallback || fallbackWarningLogged)
            {
                return;
            }
            fallbackWarningLogged = true;
            Debug.LogWarning(
                "[Settings] ProceduralDemoBootstrap had missing serialized " +
                "settings; using Resources compatibility fallback.",
                this);
        }

        private void ConfigurePrefabDependencies()
        {
            if (playerPrefab != null)
            {
                playerPrefab.GetComponent<VerticalSlicePlayerActions>()
                    ?.Configure(settings, gameplaySettings);
                playerPrefab.GetComponent<DiagnosticResourceGatherer>()
                    ?.Configure(gameplaySettings);
            }

            GameObject enemyPrefab = gameplaySettings.EnemyPrefab;
            if (enemyPrefab != null)
            {
                enemyPrefab.GetComponent<DiagnosticEnemyEvolution>()
                    ?.Configure(evolutionProfile);
            }

            ConfigureWorldItemPrefab(
                gameplaySettings.WorldItemNetworkPrefab);
            if (chaosShardPrefab !=
                gameplaySettings.WorldItemNetworkPrefab)
            {
                ConfigureWorldItemPrefab(chaosShardPrefab);
            }
        }

        private void ConfigureWorldItemPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }
            prefab.GetComponent<DiagnosticWorldPickup>()
                ?.Configure(settings, gameplaySettings);
        }

        private void ConfigureNetworkManager(
            NetworkManager networkManager,
            UnityTransport transport)
        {
            // NetworkManager normally receives this serialized object from the
            // Inspector. ProceduralDemo adds the component at runtime, so NGO's
            // Awake runs before a config exists and leaves it null.
            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = null;
            networkManager.NetworkConfig.ConnectionApproval = false;
            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.NetworkConfig.ForceSamePrefabs = true;
            networkManager.NetworkConfig.ProtocolVersion =
                ProceduralLanController.ProtocolVersion;

            if (gameplaySettings.EnemyPrefab == null)
            {
                Debug.LogError(
                    "[Procedural] Enemy Prefab is missing in Vertical Slice " +
                    "Gameplay Settings.",
                    gameplaySettings);
                return;
            }

            try
            {
                networkManager.AddNetworkPrefab(gameplaySettings.EnemyPrefab);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[Procedural] Failed to register Enemy Prefab: " +
                    $"{exception.Message}",
                    gameplaySettings);
            }

            GameObject worldItemPrefab =
                gameplaySettings.WorldItemNetworkPrefab != null
                    ? gameplaySettings.WorldItemNetworkPrefab
                    : chaosShardPrefab;
            if (worldItemPrefab != null)
            {
                try
                {
                    networkManager.AddNetworkPrefab(worldItemPrefab);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                    $"[Procedural] Failed to register World Item Prefab: " +
                        $"{exception.Message}",
                        this);
                }
            }
            else
            {
                Debug.LogError(
                    "[Procedural] Chaos Shard Prefab is missing. " +
                    "Evolution charge transfer remains available, but an " +
                    "Alpha cannot drop its shard.",
                    this);
            }

            if (!enablePlayers)
            {
                return;
            }
            if (playerPrefab == null)
            {
                Debug.LogError(
                    "[Procedural] Player Prefab is missing in combat demo bootstrap.",
                    this);
                return;
            }

            try
            {
                networkManager.AddNetworkPrefab(playerPrefab);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[Procedural] Failed to register Player Prefab: " +
                    $"{exception.Message}",
                    this);
            }
        }

        private static GameObject FindOrCreate(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            return existing != null ? existing : new GameObject(objectName);
        }

        private static T GetOrAdd<T>(GameObject target)
            where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void ConfigureFallbackCamera(
            GameObject cameraObject,
            ProceduralRenderingSettings settings,
            bool configurePlayerFollow)
        {
            Camera camera = GetOrAdd<Camera>(cameraObject);
            GetOrAdd<AudioListener>(cameraObject);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 95f, -105f);
            cameraObject.transform.rotation = Quaternion.Euler(38f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.Skybox;
            ProceduralRenderUtility.ConfigureCamera(
                camera,
                settings,
                cameraObject);
            if (configurePlayerFollow)
            {
                DiagnosticCameraFollow follow =
                    GetOrAdd<DiagnosticCameraFollow>(cameraObject);
                follow.Configure(settings);
            }
        }

        private static void ConfigureDirectionalLight(GameObject lightObject)
        {
            Light light = GetOrAdd<Light>(lightObject);
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }
    }
}
