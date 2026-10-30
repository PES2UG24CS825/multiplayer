
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
    public class MainMenu : MonoBehaviour
    {
#if !UNITY_SERVER
        static class UIElementNames
        {
            public const string HidingBackground = "HidingBackground";
            public const string NameInputField = "PlayerNameField";
            public const string ChooseCharacterOption = "ChooseCharacterOption";
            public const string ConnectionModeOption = "ConnetionModeOption";
            public const string SessionNameLabel = "SessionName";
            public const string SessionInputField = "SessionNameField";
            public const string CreateGame = "CreateJoinGame";
            public const string StartHost = "StartHost";
            public const string ConnectToServer = "ConnectToServer";
            public const string QuitGame = "QuitButton";
        }

        VisualElement m_MainMenu;
        RadioButtonGroup m_ChooseCharacterGroup;
        RadioButtonGroup m_ConnectionModeGroup;
        Label m_SessionNameLabel;
        TextField m_SessionNameField;
        TextField m_JoinCodeInput;

        Button m_CreateGameButton;
        Button m_StartHostButton;
        Button m_ConnectToServerButton;
        Button m_QuitButton;

        bool m_Subscribed;

        void OnEnable()
        {
            var document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                Debug.LogWarning("MainMenu: UIDocument unavailable.");
                return;
            }

            if (GameSettings.Instance == null)
            {
                Debug.LogWarning("MainMenu: GameSettings unavailable.");
                return;
            }

            m_MainMenu = document.rootVisualElement;

            BindMainMenu();
            BindPlayerName();
            BindCharacterSelection();
            BindConnectionMode();
            ConfigureSessionControls();
            ConfigureQuitButton();
            ConfigureBackground();

            if (!m_Subscribed)
            {
                GameManager.SessionCodeReady += OnSessionCodeReady;
                GameManager.SessionStatusChanged += OnSessionStatusChanged;
                m_Subscribed = true;
            }
        }

        void BindMainMenu()
        {
            m_MainMenu.SetBinding("style.display", new DataBinding
            {
                dataSource = GameSettings.Instance,
                dataSourcePath =
                    new PropertyPath(GameSettings.MainMenuStylePropertyName),
                bindingMode = BindingMode.ToTarget
            });
        }

        void BindPlayerName()
        {
            var nameField =
                m_MainMenu.Q<TextField>(UIElementNames.NameInputField);

            if (nameField == null)
                return;

            nameField.SetBinding("value", new DataBinding
            {
                dataSource = GameSettings.Instance,
                dataSourcePath =
                    new PropertyPath(nameof(GameSettings.PlayerName)),
                bindingMode = BindingMode.TwoWay
            });
        }

        void BindCharacterSelection()
        {
            m_ChooseCharacterGroup =
                m_MainMenu.Q<RadioButtonGroup>(
                    UIElementNames.ChooseCharacterOption);

            if (m_ChooseCharacterGroup == null)
                return;

            m_ChooseCharacterGroup.SetBinding("value", new DataBinding
            {
                dataSource = GameSettings.Instance,
                dataSourcePath =
                    new PropertyPath(nameof(GameSettings.PlayerCharacter)),
                bindingMode = BindingMode.TwoWay
            });
        }

        void BindConnectionMode()
        {
            m_ConnectionModeGroup =
                m_MainMenu.Q<RadioButtonGroup>(
                    UIElementNames.ConnectionModeOption);

            if (m_ConnectionModeGroup == null)
                return;

            m_ConnectionModeGroup.SetBinding("value", new DataBinding
            {
                dataSource = GameSettings.Instance,
                dataSourcePath =
                    new PropertyPath(nameof(GameSettings.ConnectionMode)),
                bindingMode = BindingMode.TwoWay
            });

            m_ConnectionModeGroup.UnregisterValueChangedCallback(
                OnConnectionModeChanged);

            m_ConnectionModeGroup.RegisterValueChangedCallback(
                OnConnectionModeChanged);

            m_ConnectionModeGroup.style.display = DisplayStyle.None;
        }

        void ConfigureSessionControls()
        {
            m_SessionNameLabel =
                m_MainMenu.Q<Label>(UIElementNames.SessionNameLabel);

            m_SessionNameField =
                m_MainMenu.Q<TextField>(UIElementNames.SessionInputField);

            if (m_SessionNameLabel != null)
                m_SessionNameLabel.style.display = DisplayStyle.None;

            if (m_SessionNameField != null)
                m_SessionNameField.style.display = DisplayStyle.None;

            m_CreateGameButton =
                m_MainMenu.Q<Button>(UIElementNames.CreateGame);

            m_StartHostButton =
                m_MainMenu.Q<Button>(UIElementNames.StartHost);

            m_ConnectToServerButton =
                m_MainMenu.Q<Button>(UIElementNames.ConnectToServer);

            if (m_CreateGameButton != null)
            {
                m_CreateGameButton.clicked -= OnCreateSessionPressed;
                m_CreateGameButton.text = "Create Session";
                m_CreateGameButton.clicked += OnCreateSessionPressed;
                m_CreateGameButton.style.display = DisplayStyle.Flex;
            }

            if (m_StartHostButton != null)
                m_StartHostButton.style.display = DisplayStyle.None;

            if (m_ConnectToServerButton != null)
            {
                m_ConnectToServerButton.clicked -= OnJoinSessionPressed;
                m_ConnectToServerButton.text = "Join Session";
                m_ConnectToServerButton.clicked += OnJoinSessionPressed;
                m_ConnectToServerButton.style.display = DisplayStyle.Flex;

                CreateJoinCodeInput();
            }
        }

        void CreateJoinCodeInput()
        {
            if (m_ConnectToServerButton == null)
                return;

            var parent = m_ConnectToServerButton.parent;

            if (parent == null)
                return;

            var existingField = m_MainMenu.Q<TextField>("JoinCodeInput");

            if (existingField != null)
            {
                m_JoinCodeInput = existingField;

                if (m_JoinCodeInput.parent == parent)
                {
                    int currentIndex = parent.IndexOf(m_JoinCodeInput);
                    int buttonIndex = parent.IndexOf(m_ConnectToServerButton);

                    if (currentIndex != buttonIndex - 1)
                    {
                        m_JoinCodeInput.RemoveFromHierarchy();
                        buttonIndex = parent.IndexOf(m_ConnectToServerButton);
                        parent.Insert(buttonIndex, m_JoinCodeInput);
                    }
                }

                return;
            }

            m_JoinCodeInput = new TextField("Enter Join Code")
            {
                name = "JoinCodeInput",
                tooltip = "Enter the code supplied by the host."
            };

            m_JoinCodeInput.style.width = 240;
            m_JoinCodeInput.style.maxWidth =
                new Length(100, LengthUnit.Percent);
            m_JoinCodeInput.style.alignSelf = Align.Center;
            m_JoinCodeInput.style.marginTop = 6;
            m_JoinCodeInput.style.marginBottom = 6;

            int index = parent.IndexOf(m_ConnectToServerButton);
            parent.Insert(index, m_JoinCodeInput);
        }

        void ConfigureQuitButton()
        {
            m_QuitButton = m_MainMenu.Q<Button>(UIElementNames.QuitGame);

            if (m_QuitButton == null)
                return;

            m_QuitButton.clicked -= OnQuitPressed;
            m_QuitButton.clicked += OnQuitPressed;
        }

        void ConfigureBackground()
        {
            var hidingBackground =
                m_MainMenu.Q<VisualElement>(
                    UIElementNames.HidingBackground);

            if (hidingBackground == null)
                return;

            hidingBackground.SetBinding("style.display", new DataBinding
            {
                dataSource = GameSettings.Instance,
                dataSourcePath =
                    new PropertyPath(
                        GameSettings.MainMenuSceneLoadedPropertyName),
                bindingMode = BindingMode.ToTarget
            });
        }

        void OnCreateSessionPressed()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("GameManager is unavailable.");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.LogWarning(
                "Create the session from a supported host. " +
                "Use the join code in WebGL.");
#else
            GameManager.Instance.CreateRelaySession();
#endif
        }

        void OnJoinSessionPressed()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("GameManager is unavailable.");
                return;
            }

            string code = m_JoinCodeInput?.value?.Trim();

            if (string.IsNullOrWhiteSpace(code))
            {
                Debug.LogWarning("Enter the host's join code first.");
                return;
            }

            GameManager.Instance.JoinSessionByCode(code);
        }

        void OnSessionCodeReady(string code)
        {
            Debug.Log("[Session] Join code is ready.");
        }

        void OnSessionStatusChanged(string message)
        {
            Debug.Log("[Session] " + message);
        }

        void OnConnectionModeChanged(ChangeEvent<int> evt)
        {
            GameSettings.Instance.ConnectionMode = evt.newValue;
        }

        void OnDisable()
        {
            if (m_Subscribed)
            {
                GameManager.SessionCodeReady -= OnSessionCodeReady;
                GameManager.SessionStatusChanged -= OnSessionStatusChanged;
                m_Subscribed = false;
            }

            if (m_CreateGameButton != null)
                m_CreateGameButton.clicked -= OnCreateSessionPressed;

            if (m_ConnectToServerButton != null)
                m_ConnectToServerButton.clicked -= OnJoinSessionPressed;

            if (m_ConnectionModeGroup != null)
            {
                m_ConnectionModeGroup.UnregisterValueChangedCallback(
                    OnConnectionModeChanged);
            }

            if (m_QuitButton != null)
                m_QuitButton.clicked -= OnQuitPressed;
        }

        static void OnQuitPressed()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.QuitAsync();
        }
#endif
    }
}
