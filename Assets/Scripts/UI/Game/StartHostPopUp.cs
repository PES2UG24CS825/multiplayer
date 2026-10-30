
using UnityEngine;

#if !UNITY_SERVER
using Unity.Properties;
using UnityEngine.UIElements;
#endif

namespace Unity.MP_FPS.Client
{
#if !UNITY_SERVER
    [RequireComponent(typeof(UIDocument))]
#endif
    public class StartHostPopUp : MonoBehaviour
    {
#if !UNITY_SERVER
        static class UIElementNames
        {
            public const string PortInputField = "PortField";
            public const string StartButton = "StartButton";
            public const string CancelButton = "CancelButton";
        }

        VisualElement m_StartHostPopUp;
        Button m_StartButton;
        Button m_CancelButton;

        void OnEnable()
        {
            var document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                Debug.LogWarning("StartHostPopUp: UIDocument unavailable.");
                return;
            }

            m_StartHostPopUp = document.rootVisualElement;

            var portInputField =
                m_StartHostPopUp.Q<TextField>(UIElementNames.PortInputField);

            m_StartButton =
                m_StartHostPopUp.Q<Button>(UIElementNames.StartButton);

            m_CancelButton =
                m_StartHostPopUp.Q<Button>(UIElementNames.CancelButton);

            if (GameSettings.Instance == null ||
                ConnectionSettings.Instance == null ||
                portInputField == null ||
                m_StartButton == null ||
                m_CancelButton == null)
            {
                Debug.LogWarning(
                    "StartHostPopUp: Settings or UI elements are unavailable.");
                return;
            }

            m_StartHostPopUp.SetBinding("style.display", new DataBinding
            {
                dataSource = GameSettings.Instance,
                dataSourcePath = new PropertyPath(
                    GameSettings.StartHostStylePropertyName),
                bindingMode = BindingMode.ToTarget,
            });

            portInputField.SetBinding("value", new DataBinding
            {
                dataSource = ConnectionSettings.Instance,
                dataSourcePath = new PropertyPath(
                    nameof(ConnectionSettings.Port)),
                bindingMode = BindingMode.TwoWay,
            });

            m_StartButton.clicked += OnStartPressed;

            m_StartButton.SetBinding("enabledSelf", new DataBinding
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
            if (m_StartButton != null)
            {
                m_StartButton.clicked -= OnStartPressed;
                m_StartButton = null;
            }

            if (m_CancelButton != null)
            {
                m_CancelButton.clicked -= OnCancelPressed;
                m_CancelButton = null;
            }
        }

        static void OnStartPressed()
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
