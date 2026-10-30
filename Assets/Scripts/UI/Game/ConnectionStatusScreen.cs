
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
    public class ConnectionStatusScreen : MonoBehaviour
    {
#if !UNITY_SERVER
        static class UIElementNames
        {
            public const string CancelButton = "CancelButton";
        }

        Button m_CancelButton;

        void OnEnable()
        {
            var document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                Debug.LogWarning(
                    "ConnectionStatusScreen: UIDocument unavailable.");
                return;
            }

            var root = document.rootVisualElement;

            if (ConnectionSettings.Instance == null)
            {
                Debug.LogWarning(
                    "ConnectionStatusScreen: ConnectionSettings unavailable.");
                return;
            }

            root.SetBinding("style.display", new DataBinding
            {
                dataSource = ConnectionSettings.Instance,
                dataSourcePath = new PropertyPath(
                    ConnectionSettings.ConnectionStatusStylePropertyName),
                bindingMode = BindingMode.ToTarget,
            });

            m_CancelButton =
                root.Q<Button>(UIElementNames.CancelButton);

            if (m_CancelButton == null)
            {
                Debug.LogWarning(
                    "ConnectionStatusScreen: CancelButton not found.");
                return;
            }

            m_CancelButton.clicked += OnCancelPressed;
        }

        void OnDisable()
        {
            if (m_CancelButton != null)
            {
                m_CancelButton.clicked -= OnCancelPressed;
                m_CancelButton = null;
            }
        }

        static void OnCancelPressed()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMainMenuAsync();
            }
        }
#endif
    }
}
