
using System;
using System.Threading.Tasks;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Services.Multiplayer;

namespace Unity.MP_FPS
{
    /// <summary>
    /// Configures the Netcode for Entities network driver
    /// using Unity Multiplayer Services network configuration.
    /// </summary>
    class EntityNetworkHandler : INetworkHandler
    {
        readonly TaskCompletionSource<NetworkEndpoint>
            m_ConnectEndpoint =
                new TaskCompletionSource<NetworkEndpoint>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        readonly TaskCompletionSource<NetworkEndpoint>
            m_ListenEndpoint =
                new TaskCompletionSource<NetworkEndpoint>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        readonly TaskCompletionSource<NetworkType>
            m_SessionConnectionType =
                new TaskCompletionSource<NetworkType>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        readonly object m_Lock = new object();

        bool m_Started;
        bool m_Stopped;

        public Task<NetworkEndpoint> ConnectEndpoint =>
            m_ConnectEndpoint.Task;

        public Task<NetworkEndpoint> ListenEndpoint =>
            m_ListenEndpoint.Task;

        public Task<NetworkType> SessionConnectionType =>
            m_SessionConnectionType.Task;

        public Task StartAsync(NetworkConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(
                    nameof(configuration));
            }

            lock (m_Lock)
            {
                if (m_Started)
                {
                    throw new InvalidOperationException(
                        "EntityNetworkHandler has already been started. " +
                        "Create a new handler for another session.");
                }

                if (m_Stopped)
                {
                    throw new InvalidOperationException(
                        "EntityNetworkHandler has already been stopped.");
                }

                // Prevent another StartAsync call from entering.
                m_Started = true;
            }

            try
            {
                ValidateConfiguration(configuration);

                NetworkEndpoint connectEndpoint =
                    GetConnectEndpoint(configuration);

                NetworkEndpoint listenEndpoint =
                    GetListenEndpoint(configuration);

                if (!connectEndpoint.IsValid)
                {
                    throw new InvalidOperationException(
                        "The configured connect endpoint is invalid.");
                }

                if (!listenEndpoint.IsValid)
                {
                    throw new InvalidOperationException(
                        "The configured listen endpoint is invalid.");
                }

                // Register the driver constructor before the
                // networking world creates its NetworkDriver.
                NetworkStreamReceiveSystem.DriverConstructor =
                    new EntityDriverConstructor(configuration);

                m_ConnectEndpoint.TrySetResult(connectEndpoint);
                m_ListenEndpoint.TrySetResult(listenEndpoint);
                m_SessionConnectionType.TrySetResult(
                    configuration.Type);

                UnityEngine.Debug.Log(
                    $"[NetworkHandler] Started. " +
                    $"Type: {configuration.Type}, " +
                    $"Role: {configuration.Role}, " +
                    $"Connect: {connectEndpoint}, " +
                    $"Listen: {listenEndpoint}");

                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                m_ConnectEndpoint.TrySetException(exception);
                m_ListenEndpoint.TrySetException(exception);
                m_SessionConnectionType.TrySetException(exception);

                UnityEngine.Debug.LogException(exception);

                throw;
            }
        }

        static void ValidateConfiguration(
            NetworkConfiguration configuration)
        {
#if UNITY_WEBGL && !UNITY_EDITOR

            if (configuration.Type != NetworkType.Relay)
            {
                throw new NotSupportedException(
                    "WebGL clients must use Relay networking. " +
                    "Direct UDP networking is not supported.");
            }

            if (configuration.Role != NetworkRole.Client)
            {
                throw new NotSupportedException(
                    "WebGL builds must run as clients.");
            }

#endif

            switch (configuration.Type)
            {
                case NetworkType.Direct:

                    if (configuration.Role == NetworkRole.Client &&
                        !configuration.DirectNetworkPublishAddress.IsValid)
                    {
                        throw new InvalidOperationException(
                            "The direct client publish endpoint is invalid.");
                    }

                    if (configuration.Role == NetworkRole.Host &&
                        !configuration.DirectNetworkListenAddress.IsValid)
                    {
                        throw new InvalidOperationException(
                            "The direct host listen endpoint is invalid.");
                    }

                    break;

                case NetworkType.Relay:

                    if (configuration.Role == NetworkRole.Host)
                    {
                        if (!configuration.RelayServerData.Endpoint.IsValid)
                        {
                            throw new InvalidOperationException(
                                "The Relay server endpoint is invalid.");
                        }
                    }
                    else if (configuration.Role == NetworkRole.Client)
                    {
                        if (!configuration.RelayClientData.Endpoint.IsValid)
                        {
                            throw new InvalidOperationException(
                                "The Relay client endpoint is invalid.");
                        }
                    }
                    else
                    {
                        throw new NotSupportedException(
                            "Unsupported Relay network role.");
                    }

                    break;

                default:

                    throw new NotSupportedException(
                        $"Unsupported network type: {configuration.Type}");
            }
        }

        static NetworkEndpoint GetConnectEndpoint(
            NetworkConfiguration configuration)
        {
            switch (configuration.Type)
            {
                case NetworkType.Direct:

                    switch (configuration.Role)
                    {
                        case NetworkRole.Host:

                            return NetworkEndpoint.LoopbackIpv4
                                .WithPort(
                                    configuration.DirectNetworkListenAddress.Port);

                        case NetworkRole.Client:

                            return configuration.DirectNetworkPublishAddress;

                        default:

                            throw new NotSupportedException(
                                "Unsupported direct network role.");
                    }

                case NetworkType.Relay:

                    switch (configuration.Role)
                    {
                        case NetworkRole.Host:

                            return NetworkEndpoint.LoopbackIpv4
                                .WithPort(
                                    configuration.RelayServerData.Endpoint.Port);

                        case NetworkRole.Client:

                            return configuration.RelayClientData.Endpoint;

                        default:

                            throw new NotSupportedException(
                                "Unsupported Relay network role.");
                    }

                default:

                    throw new NotSupportedException(
                        $"Unsupported network type: {configuration.Type}");
            }
        }

        static NetworkEndpoint GetListenEndpoint(
            NetworkConfiguration configuration)
        {
            switch (configuration.Type)
            {
                case NetworkType.Direct:

                    return configuration.DirectNetworkListenAddress;

                case NetworkType.Relay:

                    // This is a placeholder listen endpoint.
                    // It is NOT a dedicated-server Relay allocation.
                    // Actual Relay networking must be configured
                    // by the registered network driver.

                    return NetworkEndpoint.AnyIpv4;

                default:

                    throw new NotSupportedException(
                        $"Unsupported network type: {configuration.Type}");
            }
        }

        public Task StopAsync()
        {
            lock (m_Lock)
            {
                m_Stopped = true;
            }

            // NetworkDriver and network-world shutdown are managed
            // by the Netcode and session lifecycle systems.

            UnityEngine.Debug.Log(
                "[NetworkHandler] Stop requested.");

            return Task.CompletedTask;
        }
    }
}
