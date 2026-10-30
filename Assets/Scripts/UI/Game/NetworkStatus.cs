
using System.Collections;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.MP_FPS.Client
{
    [RequireComponent(typeof(UIDocument))]
    public class NetworkStatus : MonoBehaviour
    {
        static class UIElementNames
        {
            public const string ConnectionStatus = "ConnectionStatus";
            public const string NetworkingRole = "NetworkingRole";
            public const string SessionName = "SessionName";
        }

        VisualElement m_Root;
        Label m_ConnectionStatus;
        Label m_NetworkingRole;
        Label m_SessionName;

        Coroutine m_StatusCoroutine;

        void OnEnable()
        {
#if UNITY_SERVER
            // Dedicated server does not use client UI.
            return;
#else
            var document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                Debug.LogWarning("NetworkStatus: UIDocument is unavailable.");
                return;
            }

            m_Root = document.rootVisualElement;

            m_ConnectionStatus = m_Root.Q<Label>(
                UIElementNames.ConnectionStatus);

            m_NetworkingRole = m_Root.Q<Label>(
                UIElementNames.NetworkingRole);

            m_SessionName = m_Root.Q<Label>(
                UIElementNames.SessionName);

            if (m_ConnectionStatus == null ||
                m_NetworkingRole == null ||
                m_SessionName == null)
            {
                Debug.LogWarning(
                    "NetworkStatus: One or more UI labels are missing.");
                return;
            }

            m_StatusCoroutine =
                StartCoroutine(UpdateNetworkStatusInfo());
#endif
        }

        void OnDisable()
        {
#if !UNITY_SERVER
            if (m_StatusCoroutine != null)
            {
                StopCoroutine(m_StatusCoroutine);
                m_StatusCoroutine = null;
            }
#endif
        }

#if !UNITY_SERVER
        IEnumerator UpdateNetworkStatusInfo()
        {
            while (true)
            {
                if (m_ConnectionStatus == null ||
                    m_NetworkingRole == null ||
                    m_SessionName == null)
                {
                    yield break;
                }

                m_ConnectionStatus.text = "Connection: ";

                switch (ConnectionSettings.Instance.GameConnectionState)
                {
                    case ConnectionState.State.Disconnected:
                        m_ConnectionStatus.text += "Offline";
                        m_NetworkingRole.text = string.Empty;
                        m_SessionName.text = string.Empty;
                        break;

                    case ConnectionState.State.Connecting:
                        m_ConnectionStatus.text += "Connecting ...";
                        m_NetworkingRole.text = string.Empty;
                        m_SessionName.text = string.Empty;
                        break;

                    case ConnectionState.State.Connected:
                        m_ConnectionStatus.text += "Connected";

                        m_NetworkingRole.text =
                            ClientServerBootstrap.HasServerWorld
                                ? "Role: Server"
                                : "Role: Client";

                        if (GameManager.GameConnection != null &&
                            GameManager.GameConnection.Session != null)
                        {
                            m_SessionName.text =
                                $"Session: {GameManager.GameConnection.Session.Name}";
                        }
                        else
                        {
                            m_SessionName.text = string.Empty;
                        }
                        break;
                }

                yield return new WaitForSeconds(1.0f);
            }
        }
#endif
    }
}
