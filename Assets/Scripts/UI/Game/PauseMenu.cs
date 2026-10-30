
using UnityEngine;

#if !UNITY_SERVER
using Unity.Properties;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
#endif

namespace Unity.MP_FPS
{
#if !UNITY_SERVER
    [RequireComponent(typeof(UIDocument))]
#endif
    public class PauseMenu : MonoBehaviour
    {
#if !UNITY_SERVER
        static class UIElementNames
        {
            public const string ResumeButton = "ResumeButton";
            public const string MainMenuButton = "MainMenuButton";
            public const string QuitButton = "QuitButton";
        }

        Button m_ResumeButton;
        Button m_MainMenuButton;
        Button m_QuitButton;

        void OnEnable()
        {
            var document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                Debug.LogWarning("PauseMenu: UIDocument unavailable.");
                return;
            }

            var root = document.rootVisualElement;

            GameInput.Actions.UI.TogglePauseMenu.performed +=
                TogglePauseMenuVisibility;

            root.SetBinding("style.display", new DataBinding
            {
                dataSource = GameSettings.Instance,
                dataSourcePath =
                    new PropertyPath(GameSettings.PauseMenuStylePropertyName),
                bindingMode = BindingMode.ToTarget
            });

            m_ResumeButton = root.Q<Button>(UIElementNames.ResumeButton);

            if (m_ResumeButton != null)
            {
                m_ResumeButton.clicked -= OnResumePressed;
                m_ResumeButton.clicked += OnResumePressed;
            }

            m_MainMenuButton = root.Q<Button>(UIElementNames.MainMenuButton);

            if (m_MainMenuButton != null)
            {
                m_MainMenuButton.clicked -= OnMainMenuPressed;
                m_MainMenuButton.clicked += OnMainMenuPressed;
                m_MainMenuButton.SetEnabled(GameManager.CanUseMainMenu);
            }

            m_QuitButton = root.Q<Button>(UIElementNames.QuitButton);

            if (m_QuitButton != null)
            {
                m_QuitButton.clicked -= OnQuitPressed;
                m_QuitButton.clicked += OnQuitPressed;
            }
        }

        void OnDisable()
        {
            if (GameInput.Actions != null &&
                GameInput.Actions.UI != null)
            {
                GameInput.Actions.UI.TogglePauseMenu.performed -=
                    TogglePauseMenuVisibility;
            }

            if (m_ResumeButton != null)
                m_ResumeButton.clicked -= OnResumePressed;

            if (m_MainMenuButton != null)
                m_MainMenuButton.clicked -= OnMainMenuPressed;

            if (m_QuitButton != null)
                m_QuitButton.clicked -= OnQuitPressed;
        }

        void TogglePauseMenuVisibility(
            InputAction.CallbackContext context)
        {
            if (EventSystem.current != null)
            {
                var raycaster =
                    transform.parent != null
                        ? transform.parent.GetComponentInChildren<PanelRaycaster>()
                        : null;

                if (raycaster != null)
                {
                    EventSystem.current.SetSelectedGameObject(
                        raycaster.gameObject);
                }
            }

            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.IsPauseMenuOpen =
                    !GameSettings.Instance.IsPauseMenuOpen;
            }
        }

        static void OnResumePressed()
        {
            if (GameSettings.Instance != null)
                GameSettings.Instance.IsPauseMenuOpen = false;
        }

        static void OnMainMenuPressed()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMainMenuAsync();
                Utils.SetCursorVisible(true);
            }
        }

        static void OnQuitPressed()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.QuitAsync();
        }
#endif
    }
}
