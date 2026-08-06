using System;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MidnightChaos.Procedural
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class ProceduralDemoBootstrap : MonoBehaviour
    {
        private const string SettingsResourcePath =
            "Procedural/ProceduralWorldSettings";

        [SerializeField] private ProceduralWorldSettings settings;

        private void Awake()
        {
            settings ??= UnityEngine.Resources.Load<ProceduralWorldSettings>(
                SettingsResourcePath);
            if (settings == null)
            {
                Debug.LogError(
                    "[Procedural] Missing Resources/Procedural/" +
                    "ProceduralWorldSettings.asset.",
                    this);
                enabled = false;
                return;
            }

            GameObject networkRoot = gameObject;
            networkRoot.name = "NetworkManager";
            NetworkManager networkManager =
                GetOrAdd<NetworkManager>(networkRoot);
            UnityTransport transport = GetOrAdd<UnityTransport>(networkRoot);

            ConfigureNetworkManager(networkManager, transport);

            GameObject generatorObject = FindOrCreate(
                "ProceduralWorldGenerator");
            ProceduralWorldGenerator generator =
                GetOrAdd<ProceduralWorldGenerator>(generatorObject);

            GameObject navMeshObject = FindOrCreate("RuntimeNavMeshBuilder");
            GetOrAdd<NavMeshSurface>(navMeshObject);
            RuntimeNavMeshBuilder navMeshBuilder =
                GetOrAdd<RuntimeNavMeshBuilder>(navMeshObject);
            navMeshBuilder.Initialize(settings);

            GameObject playerSpawnObject = FindOrCreate("PlayerSpawnManager");
            ProceduralSpawnPointRegistry spawnPoints =
                GetOrAdd<ProceduralSpawnPointRegistry>(playerSpawnObject);

            GameObject enemySpawnObject = FindOrCreate("EnemySpawnManager");
            ProceduralEnemySpawnManager enemySpawnManager =
                GetOrAdd<ProceduralEnemySpawnManager>(enemySpawnObject);
            enemySpawnManager.Initialize(
                networkManager,
                settings,
                spawnPoints,
                navMeshBuilder);

            ProceduralWorldCoordinator world =
                GetOrAdd<ProceduralWorldCoordinator>(networkRoot);
            world.Initialize(
                networkManager,
                settings,
                generator,
                navMeshBuilder,
                spawnPoints,
                enemySpawnManager);

            ProceduralLanController lan =
                GetOrAdd<ProceduralLanController>(networkRoot);
            lan.Initialize(networkManager, transport);

            GameObject uiObject = FindOrCreate("UI Debug");
            ProceduralDemoUI ui = GetOrAdd<ProceduralDemoUI>(uiObject);
            ui.Initialize(
                networkManager,
                settings,
                lan,
                world,
                enemySpawnManager);

            ConfigureFallbackCamera(
                FindOrCreate("Camera fallback"),
                settings);
            ConfigureDirectionalLight(FindOrCreate("Directional Light"));
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

            if (settings.EnemyPrefab == null)
            {
                Debug.LogError(
                    "[Procedural] Enemy Prefab is missing in World Settings.",
                    settings);
                return;
            }

            try
            {
                networkManager.AddNetworkPrefab(settings.EnemyPrefab);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[Procedural] Failed to register Enemy Prefab: " +
                    $"{exception.Message}",
                    settings);
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
            ProceduralWorldSettings settings)
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
