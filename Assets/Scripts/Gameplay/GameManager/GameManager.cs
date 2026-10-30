
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Unity.Entities;
using Unity.GhostBridge;
using Unity.NetCode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.MP_FPS
{
    public partial class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public const int MaxPlayer = 32;
        public const string MainMenuSceneName = "MainMenu";
        public const string GameSceneName = "GameScene";

        public static GameConnection GameConnection { get; private set; }

        public static bool CanUseMainMenu =>
            SceneManager.GetActiveScene().name == MainMenuSceneName;

        public static event Action<string> SessionCodeReady;
        public static event Action<string> SessionStatusChanged;

        string m_RequestedJoinCode;
        bool m_CreatingRelaySession;
        bool m_StartingConnection;

        bool m_ShowRelayJoinCodeOverlay;
        string m_RelayJoinCodeForOverlay = "";
        bool m_WaitingForRemotePlayer;

        Task m_LoadingGame;
        CancellationTokenSource m_LoadingGameCancel;

        Task m_LoadingMainMenu;
        CancellationTokenSource m_LoadingMainMenuCancel;

        public UnityEngine.Audio.AudioMixer AudioMixer;
        public int MaxSoundEmitters;
        public int MaxSoundGameObjects;
        public SoundGameObjectPool SoundGameObjects;

        ISoundSystem m_SoundSystem;

        public ISoundSystem SoundSystem => m_SoundSystem;

        bool m_IsHeadless;
        bool m_IsDedicatedServer;

        public bool IsHeadless => m_IsHeadless;
        public bool IsDedicatedServer => m_IsDedicatedServer;

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Debug.LogError(
                    $"Multiple instances of '{this}' violate the Singleton pattern!",
                    this);

                Destroy(gameObject);
                return;
            }

            Instance = this;

            var commandLineArgs =
                new List<string>(Environment.GetCommandLineArgs());

#if UNITY_SERVER
            m_IsDedicatedServer = true;
#else
            m_IsDedicatedServer =
                commandLineArgs.Contains("-dedicatedServer");
#endif

            m_IsHeadless =
                m_IsDedicatedServer ||
                Application.isBatchMode ||
                commandLineArgs.Contains("-batchmode");

            ConfigVar.Init();

            if (m_IsHeadless)
            {
                m_SoundSystem = new SoundSystemNull();
                return;
            }

            m_SoundSystem = new SoundSystem();

            var cameraObject = MainCameraSingleton.Instance;

            if (cameraObject == null)
            {
                Debug.LogError(
                    "[GameManager] MainCameraSingleton is missing.");
                return;
            }

            var audioListener =
                cameraObject.GetComponent<AudioListener>();

            if (audioListener != null)
            {
                SoundGameObjects = new SoundGameObjectPool(
                    "SoundSystemSources",
                    MaxSoundGameObjects);

                m_SoundSystem.Init(
                    audioListener.transform,
                    MaxSoundEmitters,
                    SoundGameObjects,
                    AudioMixer);
            }
        }

        async void Start()
        {
            Application.runInBackground = true;

            // The dedicated-server bootstrap owns server startup.
            // Do not create a client world or load the player menu here.
            if (m_IsDedicatedServer)
                return;

            var cameraObject = MainCameraSingleton.Instance;

            if (cameraObject != null)
            {
                var camera = cameraObject.GetComponent<Camera>();

                if (camera != null)
                    camera.enabled = true;

                var audioListener =
                    cameraObject.GetComponent<AudioListener>();

                if (audioListener != null && m_SoundSystem != null)
                {
                    m_SoundSystem.SetListenerTransform(
                        audioListener.transform);
                }
            }

            GameSettings.Instance.MainMenuSceneLoaded = false;

            if (SceneManager.GetActiveScene().name == MainMenuSceneName)
            {
                m_LoadingMainMenuCancel =
                    new CancellationTokenSource();

                try
                {
                    m_LoadingMainMenu = StartMainMenuAsync(
                        m_LoadingMainMenuCancel.Token);

                    await m_LoadingMainMenu;
                }
                catch (OperationCanceledException)
                {
                    // Expected when menu loading is cancelled.
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                finally
                {
                    m_LoadingMainMenuCancel?.Dispose();
                    m_LoadingMainMenuCancel = null;
                }
            }

            if (!SceneManager.GetSceneByName("Persistents").isLoaded)
            {
                SceneManager.LoadScene(
                    "Scenes/Persistents",
                    LoadSceneMode.Additive);
            }
        }

        void Update()
        {
            if (m_SoundSystem != null && !m_IsHeadless)
                m_SoundSystem.UpdateSoundSystem(false);
        }

        async Task StartMainMenuAsync(
            CancellationToken cancellationToken)
        {
            DestroyLocalSimulationWorld();

            var clientWorld =
                ClientServerBootstrap.CreateClientWorld("ClientWorld");

            await ScenesLoader.LoadGameplayAsync(null, clientWorld);

            GameSettings.Instance.MainMenuSceneLoaded = true;

            cancellationToken.ThrowIfCancellationRequested();
        }

        public void CreateRelaySession()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SessionStatusChanged?.Invoke(
                "WebGL is client-only. Join a session created by the server.");
            return;
#endif

            if (m_IsDedicatedServer)
            {
                SessionStatusChanged?.Invoke(
                    "The dedicated server starts through its server bootstrap.");
                return;
            }

            if (GameSettings.Instance.GameState != GlobalGameState.MainMenu)
            {
                SessionStatusChanged?.Invoke(
                    "Return to the main menu before creating a session.");
                return;
            }

            if (m_StartingConnection || m_CreatingRelaySession)
                return;

            m_CreatingRelaySession = true;
            SessionStatusChanged?.Invoke("Creating Relay session...");

            StartGameAsync(CreationType.CreateOrJoin);
        }

        public void JoinSessionByCode(string joinCode)
        {
            if (m_IsDedicatedServer)
            {
                SessionStatusChanged?.Invoke(
                    "The dedicated server does not join as a player.");
                return;
            }

            if (GameSettings.Instance.GameState != GlobalGameState.MainMenu)
            {
                SessionStatusChanged?.Invoke(
                    "Return to the main menu before joining a session.");
                return;
            }

            if (m_StartingConnection || m_CreatingRelaySession)
                return;

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                SessionStatusChanged?.Invoke(
                    "Enter the session join code first.");
                return;
            }

            m_RequestedJoinCode =
                joinCode.Trim().ToUpperInvariant();

            SessionStatusChanged?.Invoke("Joining session...");

            StartGameAsync(CreationType.ConnectAndJoin);
        }

        public async void StartGameAsync(CreationType creationType)
        {
            if (m_IsDedicatedServer)
                return;

            if (GameSettings.Instance.GameState != GlobalGameState.MainMenu)
            {
                Debug.Log(
                    "[StartGameAsync] Cannot start while already loading or in-game.");

                m_CreatingRelaySession = false;
                return;
            }

            if (m_StartingConnection)
                return;

            m_StartingConnection = true;

            try
            {
                if (creationType == CreationType.Host)
                {
                    GameSettings.Instance.CancellableUserInputPopUp =
                        new AwaitableCompletionSource();

                    GameSettings.Instance.MainMenuState =
                        MainMenuState.StartHostPopup;

                    try
                    {
                        await GameSettings.Instance
                            .CancellableUserInputPopUp.Awaitable;
                    }
                    finally
                    {
                        GameSettings.Instance.MainMenuState =
                            MainMenuState.MainMenuScreen;
                    }
                }

                BeginEnteringGame();

                m_LoadingGameCancel =
                    new CancellationTokenSource();

                m_LoadingGame = StartGameAsync(
                    creationType,
                    m_LoadingGameCancel.Token);

                await m_LoadingGame;

                FinishLoadingGame();

                SessionStatusChanged?.Invoke(
                    "Connection and loading complete.");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[StartGameAsync] Loading was cancelled.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[StartGameAsync] Connection failed.");
                Debug.LogException(exception);

                SessionStatusChanged?.Invoke(
                    "Connection failed. Check the join code and Unity Services.");

                m_RequestedJoinCode = null;
                m_ShowRelayJoinCodeOverlay = false;
                m_WaitingForRemotePlayer = false;
                m_RelayJoinCodeForOverlay = "";

                ReturnToMainMenuAsync();
            }
            finally
            {
                m_LoadingGameCancel?.Dispose();
                m_LoadingGameCancel = null;

                m_StartingConnection = false;
                m_CreatingRelaySession = false;
            }
        }

        void BeginEnteringGame()
        {
            GameSettings.Instance.GameState = GlobalGameState.Loading;

            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.StartLoading);
        }

        async Task StartGameAsync(
            CreationType creationType,
            CancellationToken cancellationToken)
        {
            if (m_LoadingMainMenuCancel != null ||
                GameSettings.Instance.MainMenuSceneLoaded)
            {
                if (m_LoadingMainMenuCancel != null)
                {
                    m_LoadingMainMenuCancel.Cancel();

                    try
                    {
                        if (m_LoadingMainMenu != null)
                            await m_LoadingMainMenu;
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancelling menu loading.
                    }
                }

                if (GameSettings.Instance.MainMenuSceneLoaded)
                    await DisconnectAndUnloadWorlds();

                cancellationToken.ThrowIfCancellationRequested();
            }

            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.InitializeConnection);

            switch (creationType)
            {
                case CreationType.CreateOrJoin:
                    GameConnection =
                        await Unity.MP_FPS.GameConnection
                            .CreateRelaySessionAsync();
                    break;

                case CreationType.Host:
                    GameConnection =
                        await Unity.MP_FPS.GameConnection
                            .HostGameAsync();
                    break;

                case CreationType.ConnectAndJoin:
                {
                    string code = m_RequestedJoinCode;
                    m_RequestedJoinCode = null;

                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        GameConnection =
                            await Unity.MP_FPS.GameConnection
                                .JoinGameAsync(code);
                    }
                    else
                    {
#if UNITY_WEBGL && !UNITY_EDITOR
                        throw new NotSupportedException(
                            "WebGL clients must join using a session code.");
#else
                        GameConnection =
                            await Unity.MP_FPS.GameConnection
                                .ConnectGameAsync();
#endif
                    }

                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(creationType),
                        creationType,
                        "Unsupported creation type.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            World server = null;
            World client = null;

            ISession session = GameConnection.Session;

            if (session != null)
            {
                session.RemovedFromSession += OnSessionLeft;

                string actualCode = GameConnection.GetJoinCode();

                ConnectionSettings.Instance.SessionCode = actualCode;

                if (!string.IsNullOrWhiteSpace(actualCode))
                {
                    Debug.Log($"Session join code: {actualCode}");
                    SessionCodeReady?.Invoke(actualCode);
                }

                if (session.IsHost &&
                    creationType == CreationType.CreateOrJoin)
                {
                    m_RelayJoinCodeForOverlay = actualCode ?? "";
                    m_ShowRelayJoinCodeOverlay = true;
                    m_WaitingForRemotePlayer = true;

                    SessionStatusChanged?.Invoke(
                        "Waiting for another player...");
                }

                CreateEntityWorlds(
                    session,
                    GameConnection.SessionConnectionType,
                    out server,
                    out client);
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                throw new NotSupportedException(
                    "WebGL requires a Relay session.");
#else
                CreateEntityWorlds(
                    creationType == CreationType.Host,
                    out server,
                    out client);
#endif
            }

            if (server != null)
            {
                using var driverQuery =
                    server.EntityManager.CreateEntityQuery(
                        ComponentType.ReadWrite<NetworkStreamDriver>());

                if (driverQuery.CalculateEntityCount() != 1)
                {
                    throw new InvalidOperationException(
                        "Expected exactly one server NetworkStreamDriver.");
                }

                var serverDriver =
                    driverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW;

                if (GhostBridgeManager.Instance != null)
                {
                    GhostBridgeManager.Instance
                        .SetServerNetworkStreamDriver(serverDriver);
                }

                serverDriver.Listen(GameConnection.ListenEndpoint);

                await ScenesLoader.LoadGameplayAsync(server, null);
            }

            if (client != null)
            {
                ConnectionSettings.Instance.ConnectionEndpoint =
                    GameConnection.ConnectEndpoint;

                await WaitForPlayerConnectionAsync(cancellationToken);

                await ScenesLoader.LoadGameplayAsync(null, client);

                await WaitForGhostReplicationAsync(
                    client,
                    cancellationToken);
            }

            if (session != null &&
                session.IsHost &&
                creationType == CreationType.CreateOrJoin)
            {
                try
                {
                    while (session.PlayerCount < 2)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        await Awaitable.NextFrameAsync(
                            cancellationToken);
                    }

                    SessionStatusChanged?.Invoke(
                        "Player joined. Game is ready.");
                }
                finally
                {
                    m_WaitingForRemotePlayer = false;
                    m_ShowRelayJoinCodeOverlay = false;
                }
            }
        }

        void OnGUI()
        {
            if (!m_ShowRelayJoinCodeOverlay ||
                string.IsNullOrWhiteSpace(m_RelayJoinCodeForOverlay))
            {
                return;
            }

            float width = Mathf.Min(440f, Screen.width - 32f);
            float height = 112f;

            var box = new Rect(
                (Screen.width - width) * 0.5f,
                24f,
                width,
                height);

            GUI.Box(box, GUIContent.none);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            var codeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };

            GUI.Label(
                new Rect(box.x + 8, box.y + 8, box.width - 16, 30),
                "WAITING FOR CLIENT CONNECTION",
                titleStyle);

            GUI.Label(
                new Rect(box.x + 8, box.y + 43, box.width - 16, 42),
                "JOIN CODE: " + m_RelayJoinCodeForOverlay,
                codeStyle);
        }

        public static async Task WaitForGhostReplicationAsync(
            World world,
            CancellationToken cancellationToken = default)
        {
            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.WorldReplication);

            using var ghostCountQuery =
                world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<GhostCount>());

            int waitedForTicks = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ghostCountQuery.TryGetSingleton<GhostCount>(
                    out var ghostCount))
                {
                    float progress =
                        ghostCount.GhostCountOnServer == 0 ||
                        waitedForTicks > 60
                            ? 1f
                            : 0f;

                    LoadingData.Instance.UpdateLoading(
                        LoadingData.LoadingSteps.WorldReplication,
                        progress);

                    if (progress > 0.99f)
                        return;
                }

                await Awaitable.NextFrameAsync(cancellationToken);
                waitedForTicks++;
            }
        }

        static async Task WaitForAttachedCameraAsync(
            World world,
            CancellationToken cancellationToken = default)
        {
            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.WaitingOnPlayer);

            using var cameraQuery =
                world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<MainCamera>());

            while (!cameraQuery.HasSingleton<MainCamera>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Awaitable.NextFrameAsync(cancellationToken);
            }

            await Awaitable.NextFrameAsync(cancellationToken);
        }

        static void CreateEntityWorlds(
            ISession session,
            NetworkType connectionType,
            out World serverWorld,
            out World clientWorld)
        {
            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.CreateWorld);

            DestroyLocalSimulationWorld();

#if UNITY_EDITOR
            if (connectionType == NetworkType.Relay &&
                MultiplayerPlayModePreferences.RequestedNumThinClients > 0)
            {
                Debug.Log(
                    "[CreateEntityWorlds] ThinClient is disabled when Relay is used.");

                MultiplayerPlayModePreferences.RequestedNumThinClients = 0;
            }
#endif

            serverWorld = null;
            clientWorld = null;

#if !UNITY_WEBGL || UNITY_EDITOR
            if (session.IsHost)
            {
                serverWorld =
                    ClientServerBootstrap.CreateServerWorld("ServerWorld");

                GhostGameObjectUpdateSystem.GatherWorldSystems(serverWorld);
            }
#else
            Debug.Log(
                "[CreateEntityWorlds] WebGL: creating client world only.");
#endif

            clientWorld =
                ClientServerBootstrap.CreateClientWorld("ClientWorld");

            GhostGameObjectUpdateSystem.GatherWorldSystems(clientWorld);
        }

        static void CreateEntityWorlds(
            bool isHost,
            out World serverWorld,
            out World clientWorld)
        {
            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.CreateWorld);

            DestroyLocalSimulationWorld();

            serverWorld = null;
            clientWorld = null;

            if (isHost)
            {
                serverWorld =
                    ClientServerBootstrap.CreateServerWorld("ServerWorld");

                GhostGameObjectUpdateSystem.GatherWorldSystems(serverWorld);
            }

            clientWorld =
                ClientServerBootstrap.CreateClientWorld("ClientWorld");

            GhostGameObjectUpdateSystem.GatherWorldSystems(clientWorld);
        }

        public static async Task WaitForPlayerConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.WaitingConnection);

            ConnectionSettings.Instance.GameConnectionState =
                ConnectionState.State.Connecting;

            while (ConnectionSettings.Instance.GameConnectionState ==
                   ConnectionState.State.Connecting)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Awaitable.NextFrameAsync(cancellationToken);
            }
        }

        void FinishLoadingGame()
        {
            m_ShowRelayJoinCodeOverlay = false;
            m_WaitingForRemotePlayer = false;

            LoadingData.Instance.UpdateLoading(
                LoadingData.LoadingSteps.LoadingDone);

            GameSettings.Instance.GameState = GlobalGameState.InGame;
        }

        public static void DestroyLocalSimulationWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Flags == WorldFlags.Game)
                {
                    world.Dispose();
                    break;
                }
            }
        }

        public static void SetGameConnection(GameConnection gameConnection)
        {
            GameConnection = gameConnection;
        }
    }
}
