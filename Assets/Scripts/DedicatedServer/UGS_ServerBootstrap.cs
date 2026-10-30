
#if UNITY_SERVER && UGS_SERVER

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
    public class UGSDedicatedServerBootstrap : MonoBehaviour
    {
        bool m_Started;

        async void Awake()
        {
            Application.runInBackground = true;

            Debug.Log("[FPS2 Server] Starting dedicated server bootstrap.");

            try
            {
                if (!SceneManager.GetSceneByName("Persistents").isLoaded)
                {
                    await LoadPersistentSceneAsync();
                }

                await StartDedicatedServerAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[FPS2 Server] Dedicated server startup failed.");

                Debug.LogException(exception);

                Application.Quit();
            }
        }

        static Task LoadPersistentSceneAsync()
        {
            var completion = new TaskCompletionSource<bool>();

            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != "Persistents")
                    return;

                SceneManager.sceneLoaded -= OnSceneLoaded;
                completion.TrySetResult(true);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;

            SceneManager.LoadScene(
                "Scenes/Persistents",
                LoadSceneMode.Additive);

            return completion.Task;
        }

        async Task StartDedicatedServerAsync()
        {
            if (m_Started)
                return;

            m_Started = true;

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

            if (!ushort.TryParse(
                    ConnectionSettings.Instance.Port,
                    out ushort port) ||
                port == 0)
            {
                throw new InvalidOperationException(
                    "Configure a valid server port in ConnectionSettings.");
            }

            GameSettings.Instance.GameState =
                GlobalGameState.Loading;

            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.CreateWorld);

            GameManager.DestroyLocalSimulationWorld();

            World serverWorld =
                ClientServerBootstrap.CreateServerWorld("ServerWorld");

            if (serverWorld == null || !serverWorld.IsCreated)
            {
                throw new InvalidOperationException(
                    "Could not create the server world.");
            }

            GhostGameObjectUpdateSystem.GatherWorldSystems(serverWorld);

            using var driverQuery =
                serverWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<NetworkStreamDriver>());

            if (driverQuery.CalculateEntityCount() != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one NetworkStreamDriver in ServerWorld.");
            }

            var serverDriver =
                driverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW;

            if (GhostBridgeManager.Instance != null)
            {
                GhostBridgeManager.Instance
                    .SetServerNetworkStreamDriver(serverDriver);
            }

            NetworkEndpoint listenEndpoint =
                NetworkEndpoint.AnyIpv4.WithPort(port);

            // Use the factory instead of calling the private constructor.
            GameConnection gameConnection =
                GameConnection.GetServerConnectionSettings(
                    listenEndpoint);

            GameManager.SetGameConnection(gameConnection);

            Debug.Log(
                $"[FPS2 Server] Listening on port {port}.");

            serverDriver.Listen(listenEndpoint);

            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.LoadGameScene);

            await ScenesLoader.LoadGameplayAsync(serverWorld, null);

            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.LoadingDone);

            GameSettings.Instance.GameState =
                GlobalGameState.InGame;

            Debug.Log(
                $"[FPS2 Server] Gameplay loaded. Endpoint: {listenEndpoint}");
        }
    }
}

#endif
