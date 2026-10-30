
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Multiplayer;

namespace Unity.MP_FPS
{
    struct EntityNetworkConfiguration
    {
        public NetworkRole Role;
        public NetworkType Type;
        public RelayServerData RelayClientData;
        public RelayServerData RelayServerData;
    }

    class EntityDriverConstructor : INetworkStreamDriverConstructor
    {
        private EntityNetworkConfiguration m_Configuration;

        public EntityDriverConstructor(NetworkConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (configuration.Type != NetworkType.Relay)
            {
                throw new NotSupportedException(
                    "WebGL clients must use Relay networking.");
            }

            if (configuration.Role != NetworkRole.Client)
            {
                throw new NotSupportedException(
                    "WebGL must run as a client. The server must run " +
                    "in a separate server-capable process.");
            }
#endif

            m_Configuration = new EntityNetworkConfiguration
            {
                Role = configuration.Role,
                Type = configuration.Type,
                RelayClientData = configuration.RelayClientData,
                RelayServerData = configuration.RelayServerData
            };
        }

        public EntityDriverConstructor(NetworkRole role)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new NotSupportedException(
                "WebGL requires a Relay client configuration.");
#else
            m_Configuration = new EntityNetworkConfiguration
            {
                Role = role,
                Type = NetworkType.Direct
            };
#endif
        }

        public void CreateClientDriver(
            World world,
            ref NetworkDriverStore driverStore,
            NetDebug netDebug)
        {
            var networkSettings = GameNetworkSettings(
                sendQueueCapacity: 64,
                receiveQueueCapacity: 64);

#if UNITY_EDITOR || NETCODE_DEBUG
            if (NetworkSimulatorSettings.Enabled)
            {
                NetworkSimulatorSettings.SetSimulatorSettings(
                    ref networkSettings);
            }
#endif

            if (m_Configuration.Type == NetworkType.Relay)
            {
                networkSettings.WithRelayParameters(
                    ref m_Configuration.RelayClientData);

#if UNITY_WEBGL && !UNITY_EDITOR
                DefaultDriverBuilder.RegisterClientWebSocketDriver(
                    world,
                    ref driverStore,
                    netDebug,
                    networkSettings);
#else
                DefaultDriverBuilder.RegisterClientUdpDriver(
                    world,
                    ref driverStore,
                    netDebug,
                    networkSettings);
#endif
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                throw new NotSupportedException(
                    "Direct UDP networking is not supported in WebGL.");
#else
                DefaultDriverBuilder.RegisterClientDriver(
                    world,
                    ref driverStore,
                    netDebug,
                    networkSettings);
#endif
            }
        }

        public void CreateServerDriver(
            World world,
            ref NetworkDriverStore driverStore,
            NetDebug netDebug)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new NotSupportedException(
                "WebGL is client-only. Run the authoritative server " +
                "in a separate server-capable process.");
#else
            const int expectedMaxPlayerCount = 32;

            var networkSettings = GameNetworkSettings(
                sendQueueCapacity: expectedMaxPlayerCount * 4,
                receiveQueueCapacity: expectedMaxPlayerCount * 16);

#if UNITY_EDITOR || NETCODE_DEBUG
            if (NetworkSimulatorSettings.Enabled)
            {
                NetworkSimulatorSettings.SetSimulatorSettings(
                    ref networkSettings);
            }
#endif

            if (m_Configuration.Role == NetworkRole.Host)
            {
                DefaultDriverBuilder.RegisterServerIpcDriver(
                    world,
                    ref driverStore,
                    netDebug,
                    networkSettings);
            }

            if (m_Configuration.Type == NetworkType.Relay)
            {
                networkSettings.WithRelayParameters(
                    ref m_Configuration.RelayServerData);

                // Dedicated server uses UDP for Relay when the
                // Relay allocation provides UDP server connection data.
                DefaultDriverBuilder.RegisterServerUdpDriver(
                    world,
                    ref driverStore,
                    netDebug,
                    networkSettings);

                return;
            }

            // Direct dedicated-server connections use UDP.
            DefaultDriverBuilder.RegisterServerUdpDriver(
                world,
                ref driverStore,
                netDebug,
                networkSettings);
#endif
        }

        private static NetworkSettings GameNetworkSettings(
            int sendQueueCapacity,
            int receiveQueueCapacity)
        {
            var settings = new NetworkSettings(Allocator.Temp);

            settings.WithNetworkConfigParameters(
                connectTimeoutMS: 500,
                maxConnectAttempts: 8,
                disconnectTimeoutMS: 3500,
                reconnectionTimeoutMS: 1500,
                sendQueueCapacity: sendQueueCapacity,
                receiveQueueCapacity: receiveQueueCapacity);

            return settings;
        }
    }
}
