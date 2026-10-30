
using UnityEngine;

#if !UNITY_SERVER
using Unity.Properties;
using UnityEngine.UIElements;
#endif

namespace Unity.MP_FPS.Client
{
    namespace Unity.StarterKit.FPS
    {
#if !UNITY_SERVER
        [RequireComponent(typeof(UIDocument))]
#endif
        public class DirectConnectPopUp : MonoBehaviour
        {
#if !UNITY_SERVER
            static class UIElementNames
            {
                public const string IPAddressInputField = "IPAddressField";
                public const string PortInputField = "PortField";
                public const string ConfirmButton = "ConfirmButton";
                public const string CancelButton = "CancelButton";
            }

            VisualElement m_DirectConnectPopUp;
            Button m_ConfirmButton;
            Button m_CancelButton;

            void OnEnable()
            {
                var document = GetComponent<UIDocument>();

                if (document == null || document.rootVisualElement == null)
                {
                    Debug.LogWarning(
                        "DirectConnectPopUp: UIDocument is unavailable.");
                    return;
                }

                m_DirectConnectPopUp = document.rootVisualElement;

                var ipAddressInputField =
                    m_DirectConnectPopUp.Q<TextField>(
                        UIElementNames.IPAddressInputField);

                var portInputField =
                    m_DirectConnectPopUp.Q<TextField>(
                        UIElementNames.PortInputField);

                m_ConfirmButton =
                    m_DirectConnectPopUp.Q<Button>(
                        UIElementNames.ConfirmButton);

                m_CancelButton =
                    m_DirectConnectPopUp.Q<Button>(
                        UIElementNames.CancelButton);

                if (ipAddressInputField == null ||
                    portInputField == null ||
                    m_ConfirmButton == null ||
                    m_CancelButton == null)
                {
                    Debug.LogWarning(
                        "DirectConnectPopUp: One or more UI elements are missing.");
                    return;
                }

                m_DirectConnectPopUp.SetBinding("style.display", new DataBinding
                {
                    dataSource = GameSettings.Instance,
                    dataSourcePath = new PropertyPath(
                        GameSettings.DirectConnectStylePropertyName),
                    bindingMode = BindingMode.ToTarget,
                });

                ipAddressInputField.SetBinding("value", new DataBinding
                {
                    dataSource = ConnectionSettings.Instance,
                    dataSourcePath = new PropertyPath(
                        nameof(ConnectionSettings.IPAddress)),
                    bindingMode = BindingMode.TwoWay,
                });

                portInputField.SetBinding("value", new DataBinding
                {
                    dataSource = ConnectionSettings.Instance,
                    dataSourcePath = new PropertyPath(
                        nameof(ConnectionSettings.Port)),
                    bindingMode = BindingMode.TwoWay,
                });

                m_ConfirmButton.clicked += OnConfirmPressed;

                m_ConfirmButton.SetBinding("enabledSelf", new DataBinding
                {
                    dataSource = ConnectionSettings.Instance,
                    dataSourcePath = new PropertyPath(
                        nameof(ConnectionSettings.IsNetworkEndpointValid)),
                    bindingMode = BindingMode.ToTarget,
                });

                m_CancelButton.clicked += OnCancelPressed;
            }

            void OnDisable()
            {
                if (m_ConfirmButton != null)
                {
                    m_ConfirmButton.clicked -= OnConfirmPressed;
                    m_ConfirmButton = null;
                }

                if (m_CancelButton != null)
                {
                    m_CancelButton.clicked -= OnCancelPressed;
                    m_CancelButton = null;
                }
            }

            static void OnConfirmPressed()
            {
                GameSettings.Instance.CancellableUserInputPopUp.SetResult();
            }

            static void OnCancelPressed()
            {
                GameSettings.Instance.CancellableUserInputPopUp.SetCanceled();

                ConnectionSettings.Instance.IPAddress =
                    ConnectionSettings.DefaultServerAddress;

                ConnectionSettings.Instance.Port =
                    ConnectionSettings.DefaultServerPort.ToString();
            }
#endif
        }
    }
}
