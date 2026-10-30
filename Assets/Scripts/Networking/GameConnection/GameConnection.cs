
using System;
using System.Threading.Tasks;

using Unity.Networking.Transport;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Unity.MP_FPS
{
    /// <summary>
    /// Holds session and endpoint information for a game connection.
    /// Supports player Relay sessions and direct network endpoints.
    /// </summary>
    public class GameConnection
    {
        public ISession Session { get; private set; }

        public NetworkEndpoint ListenEndpoint { get; private set; }

        public NetworkEndpoint ConnectEndpoint { get; private set; }

        public NetworkType SessionConnectionType { get; private set; }

        private GameConnection()
        {
        }

        // ---------------------------------------------------------
        // PLAYER RELAY SESSION
        // ---------------------------------------------------------

        /// <summary>
        /// Creates a player-hosted Relay session.
        /// This is NOT a dedicated-server Relay allocation.
        /// </summary>
        public static async Task<GameConnection> CreateRelaySessionAsync()
        {
            await StartServicesAsync();

            if (GameSettings.Instance == null)
            {
                throw new InvalidOperationException(
                    "GameSettings is not initialized.");
            }

            var handler = new EntityNetworkHandler();

            var options = new SessionOptions
            {
                Name = GameSettings.Instance.SessionName,
                MaxPlayers = GameManager.MaxPlayer
            }
            .WithRelayNetwork()
            .WithNetworkHandler(handler);

            ISession session =
                await MultiplayerService.Instance.CreateSessionAsync(options);

            try
            {
                var connection = new GameConnection
                {
                    Session = session
                };

                await PopulateNetworkSettingsAsync(connection, handler);

                Debug.Log(
                    $"[GameConnection] Relay session created. " +
                    $"Join code: {session.Code}");

                return connection;
            }
            catch
            {
                await session.LeaveAsync();
                throw;
            }
        }

        /// <summary>
        /// Compatibility method for existing project code.
        /// </summary>
        public static Task<GameConnection> CreateorJoinGameAsync()
        {
            return CreateRelaySessionAsync();
        }

        // ---------------------------------------------------------
        // JOIN EXISTING RELAY SESSION
        // ---------------------------------------------------------

        /// <summary>
        /// Joins an existing Relay session using its join code.
        /// </summary>
        public static async Task<GameConnection> JoinGameAsync(
            string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                throw new ArgumentException(
                    "Enter a valid session join code.",
                    nameof(joinCode));
            }

            await StartServicesAsync();

            var handler = new EntityNetworkHandler();

            var options = new JoinSessionOptions()
                .WithNetworkHandler(handler);

            ISession session =
                await MultiplayerService.Instance.JoinSessionByCodeAsync(
                    joinCode.Trim().ToUpperInvariant(),
                    options);

            try
            {
                var connection = new GameConnection
                {
                    Session = session
                };

                await PopulateNetworkSettingsAsync(connection, handler);

                Debug.Log(
                    $"[GameConnection] Joined session: {session.Name}");

                return connection;
            }
            catch
            {
                await session.LeaveAsync();
                throw;
            }
        }

        // ---------------------------------------------------------
        // DIRECT HOST
        // ---------------------------------------------------------

        /// <summary>
        /// Creates direct UDP host connection settings.
        /// Does not create a Relay session.
        /// </summary>
        public static Task<GameConnection> HostGameAsync()
        {
            ushort port = GetConfiguredPort();

            var endpoint =
                NetworkEndpoint.AnyIpv4.WithPort(port);

            return Task.FromResult(new GameConnection
            {
                ListenEndpoint = endpoint,

                ConnectEndpoint =
                    NetworkEndpoint.LoopbackIpv4.WithPort(port),

                SessionConnectionType = NetworkType.Direct
            });
        }

        // ---------------------------------------------------------
        // DIRECT CLIENT
        // ---------------------------------------------------------

        /// <summary>
        /// Creates direct UDP client connection settings.
        /// Not supported in WebGL builds.
        /// </summary>
        public static Task<GameConnection> ConnectGameAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR

            throw new NotSupportedException(
                "Direct UDP connections are not supported in WebGL. " +
                "Use a supported WebSocket/Relay transport.");

#else

            ushort port = GetConfiguredPort();

            string ipAddress =
                ConnectionSettings.Instance.IPAddress;

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                throw new InvalidOperationException(
                    "ConnectionSettings.IPAddress is required.");
            }

            NetworkEndpoint endpoint =
                NetworkEndpoint.Parse(ipAddress, port);

            if (!endpoint.IsValid)
            {
                throw new InvalidOperationException(
                    "The configured server IP address is invalid.");
            }

            return Task.FromResult(new GameConnection
            {
                ListenEndpoint = NetworkEndpoint.AnyIpv4,

                ConnectEndpoint = endpoint,

                SessionConnectionType = NetworkType.Direct
            });

#endif
        }

        /// <summary>
        /// Compatibility overload for existing project code.
        /// </summary>
        public static Task<GameConnection> ConnectGameAsync(
            string joinCode)
        {
            return JoinGameAsync(joinCode);
        }

        // ---------------------------------------------------------
        // DEDICATED SERVER ENDPOINT
        // ---------------------------------------------------------

        /// <summary>
        /// Configures a direct-network dedicated server endpoint.
        /// This does not create a Relay allocation or authenticate
        /// a dedicated server.
        /// </summary>
        public static GameConnection GetServerConnectionSettings(
            NetworkEndpoint listenEndpoint)
        {
            if (!listenEndpoint.IsValid)
            {
                throw new ArgumentException(
                    "The server listening endpoint is invalid.",
                    nameof(listenEndpoint));
            }

            return new GameConnection
            {
                ListenEndpoint = listenEndpoint,

                ConnectEndpoint = listenEndpoint,

                SessionConnectionType = NetworkType.Direct
            };
        }

        // ---------------------------------------------------------
        // NETWORK CONFIGURATION
        // ---------------------------------------------------------

        private static async Task PopulateNetworkSettingsAsync(
            GameConnection connection,
            EntityNetworkHandler handler)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            connection.ConnectEndpoint =
                await handler.ConnectEndpoint;

            connection.ListenEndpoint =
                await handler.ListenEndpoint;

            connection.SessionConnectionType =
                await handler.SessionConnectionType;

            if (!connection.ConnectEndpoint.IsValid)
            {
                throw new InvalidOperationException(
                    "The connection endpoint is invalid.");
            }
        }

        // ---------------------------------------------------------
        // UNITY SERVICES INITIALIZATION
        // ---------------------------------------------------------

        /// <summary>
        /// Initializes Unity Gaming Services and authenticates
        /// the player using anonymous authentication.
        ///
        /// This is a player authentication flow, not dedicated-server
        /// authentication.
        /// </summary>
        private static async Task StartServicesAsync()
        {
            if (UnityServices.State ==
                ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (UnityServices.State !=
                ServicesInitializationState.Initialized)
            {
                throw new InvalidOperationException(
                    "Unity Services initialization failed.");
            }

            if (!AuthenticationService.Instance.IsAuthorized)
            {
                await AuthenticationService.Instance
                    .SignInAnonymouslyAsync();
            }
        }

        // ---------------------------------------------------------
        // HELPERS
        // ---------------------------------------------------------

        private static ushort GetConfiguredPort()
        {
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
                    "ConnectionSettings.Port must contain a valid port.");
            }

            return port;
        }

        public string GetJoinCode()
        {
            return Session?.Code;
        }
    }
}
