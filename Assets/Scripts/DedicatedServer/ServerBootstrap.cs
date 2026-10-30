
#if UNITY_SERVER

using System;
using System.Threading.Tasks;

using Unity.Entities;
using Unity.GhostBridge;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.MP_FPS
{
    public class ServerBootstrap : MonoBehaviour
    {
        bool m_Started;

        void Awake()
        {
            Debug.Log("[FPS2 Server] Awakening.");

            Application.runInBackground = true;

            Debug.Log("[FPS2 Server] Loading Persistent scene...");

            if (!SceneManager.GetSceneByName("Persistents").isLoaded)
            {
                SceneManager.LoadScene(
                    "Scenes/Persistents",
                    LoadSceneMode.Additive);
            }
        }

        async void Start()
        {
            if (m_Started)
                return;

            m_Started = true;

            World serverWorld = null;

            try
            {
                Debug.Log("[FPS2 Server] Starting.");

                if (GameSettings.Instance == null)
                {
                    throw new InvalidOperationException(
                        "GameSettings is not initialized.");
                }

                if (ConnectionSettings.Instance == null)
                {
                    throw new InvalidOperationException(
                        "ConnectionSettings is not initialized.");
                }

                GameSettings.Instance.GameState =
                    GlobalGameState.Loading;

                LoadingData.Instance.UpdateLoading(
                    LoadingData.LoadingSteps.InitializeConnection);

                ushort port = GetPortFromArgument();

                NetworkEndpoint listenEndpoint =
                    NetworkEndpoint.AnyIpv4.WithPort(port);

                ConnectionSettings.Instance.Port = port.ToString();

                GameConnection gameConnection =
                    GameConnection.GetServerConnectionSettings(
                        listenEndpoint);

                GameManager.SetGameConnection(gameConnection);

                Debug.Log(
                    $"[FPS2 Server] Configured endpoint: {listenEndpoint}");

                LoadingData.Instance.UpdateLoading(
                    LoadingData.LoadingSteps.CreateWorld);

                GameManager.DestroyLocalSimulationWorld();

                serverWorld = GhostBridgeBootstrap.Instance.ServerWorld;

                if (serverWorld == null || !serverWorld.IsCreated)
                {
                    throw new InvalidOperationException(
                        "GhostBridgeBootstrap did not provide a valid ServerWorld.");
                }

                GhostGameObjectUpdateSystem.GatherWorldSystems(
                    serverWorld);

                Debug.Log("[FPS2 Server] ServerWorld is available.");

                using var driverQuery =
                    serverWorld.EntityManager.CreateEntityQuery(
                        ComponentType.ReadWrite<NetworkStreamDriver>());

                if (driverQuery.CalculateEntityCount() != 1)
                {
                    throw new InvalidOperationException(
                        "Expected exactly one NetworkStreamDriver in ServerWorld.");
                }

                driverQuery.CompleteDependency();

                var serverDriver =
                    driverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW;

                if (GhostBridgeManager.Instance != null)
                {
                    GhostBridgeManager.Instance
                        .SetServerNetworkStreamDriver(serverDriver);
                }

                serverDriver.Listen(gameConnection.ListenEndpoint);

                Debug.Log(
                    $"[FPS2 Server] Listening on port {port}.");

                LoadingData.Instance.UpdateLoading(
                    LoadingData.LoadingSteps.LoadGameScene);

                await ScenesLoader.LoadGameplayAsync(
                    serverWorld,
                    null);

                Debug.Log("[FPS2 Server] Gameplay scenes loaded.");

                LoadingData.Instance.UpdateLoading(
                    LoadingData.LoadingSteps.LoadingDone);

                GameSettings.Instance.GameState =
                    GlobalGameState.InGame;

                Debug.Log(
                    "[FPS2 Server] Startup completed.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[FPS2 Server] Startup failed.");

                Debug.LogException(exception);

                if (serverWorld != null && serverWorld.IsCreated)
                {
                    serverWorld.Dispose();
                }

                Application.Quit();
            }
        }

        static ushort GetPortFromArgument()
        {
            const ushort defaultPort = 7979;

            string[] args = Environment.GetCommandLineArgs();

            foreach (string arg in args)
            {
                string value = arg.StartsWith(
                    "--port=",
                    StringComparison.OrdinalIgnoreCase)
                    ? arg.Substring("--port=".Length)
                    : arg.StartsWith(
                        "port=",
                        StringComparison.OrdinalIgnoreCase)
                        ? arg.Substring("port=".Length)
                        : null;

                if (value == null)
                    continue;

                if (ushort.TryParse(value, out ushort port) && port > 0)
                {
                    Debug.Log($"[FPS2 Server] Port argument: {port}");
                    return port;
                }

                Debug.LogWarning(
                    $"[FPS2 Server] Invalid port '{value}'. " +
                    $"Using default port {defaultPort}.");

                return defaultPort;
            }

            Debug.Log(
                $"[FPS2 Server] Using default port {defaultPort}.");

            return defaultPort;
        }
    }
}

#endif
