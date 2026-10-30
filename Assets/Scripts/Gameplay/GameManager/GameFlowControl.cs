
using System;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Unity.MP_FPS
{
    public partial class GameManager : MonoBehaviour
    {
        /// <summary>
        /// Safely return to the main menu.
        /// </summary>
        public async void ReturnToMainMenuAsync()
        {
            Debug.Log($"[{nameof(ReturnToMainMenuAsync)}] Called.");

            if (!CanUseMainMenu)
            {
                QuitAsync();
                return;
            }

            if (m_LoadingGameCancel != null)
            {
                Debug.Log(
                    $"[{nameof(ReturnToMainMenuAsync)}] Cancelling loading game.");

                m_LoadingGameCancel.Cancel();

                try
                {
                    if (m_LoadingGame != null)
                        await m_LoadingGame;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling a loading operation.
                }

                Debug.Log(
                    $"[{nameof(ReturnToMainMenuAsync)}] Loading cancelled.");
            }

            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.UnloadingGame);

            GameSettings.Instance.GameState = GlobalGameState.Loading;
            GameSettings.Instance.IsPauseMenuOpen = false;

            await DisconnectAndUnloadWorlds();

            // Restart main menu loading.
            Start();

            Utils.SetCursorVisible(true);

            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.BackToMainMenu);

            GameSettings.Instance.GameState = GlobalGameState.MainMenu;
        }

        /// <summary>
        /// Safely shut down the game.
        /// </summary>
        public async void QuitAsync()
        {
            try
            {
                await LeaveSessionAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        async Task LeaveSessionAsync()
        {
            if (GameConnection == null || GameConnection.Session == null)
                return;

            var session = GameConnection.Session;

            session.RemovedFromSession -= OnSessionLeft;

            if (session.IsHost)
            {
                await session.AsHost().DeleteAsync();
            }
            else
            {
                await session.LeaveAsync();
            }

            GameConnection = null;
        }

        void OnSessionLeft()
        {
            GameConnection = null;
            ReturnToMainMenuAsync();
        }

        static async Task DestroyGameSessionWorlds()
        {
            // Avoid disposing worlds during an update.
            await Awaitable.EndOfFrameAsync();

            for (int i = World.All.Count - 1; i >= 0; i--)
            {
                var world = World.All[i];

                if (world.IsServer() || world.IsClient())
                {
                    world.Dispose();
                }
            }
        }

        async Task DisconnectAndUnloadWorlds()
        {
            NetworkStreamReceiveSystem.DriverConstructor = null;

            ConnectionSettings.Instance.GameConnectionState =
                ConnectionState.State.Disconnected;

            bool requestedDisconnect = false;

            foreach (var world in World.All)
            {
                if (!world.IsClient())
                    continue;

                using var query =
                    world.EntityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<NetworkId>());

                if (query.TryGetSingletonEntity<NetworkId>(
                    out var networkId))
                {
                    requestedDisconnect = true;

                    world.EntityManager.AddComponentData(
                        networkId,
                        new NetworkStreamRequestDisconnect());
                }
            }

            if (requestedDisconnect)
                await Awaitable.NextFrameAsync();

            await LeaveSessionAsync();
            await DestroyGameSessionWorlds();

            if (GhostBridgeBootstrap.Instance != null)
            {
                Debug.Log(
                    $"[{nameof(DisconnectAndUnloadWorlds)}] " +
                    "Destroying multiplayer worlds.");

                GhostBridgeBootstrap.Instance.DestroyMultiplayerWorlds();
            }
            else
            {
                Debug.Log(
                    $"[{nameof(DisconnectAndUnloadWorlds)}] " +
                    "Invoking static callbacks.");

                GhostSingleton.InvokeAndClearStaticCallbacks();
            }

            await ScenesLoader.UnloadGameplayScenesAsync();
        }
    }
}
