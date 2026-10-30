
using UnityEngine;

#if !UNITY_SERVER
using UnityEngine.UIElements;
#endif

namespace Unity.MP_FPS.UI
{
#if !UNITY_SERVER
    [RequireComponent(typeof(UIDocument))]
#endif
    public class ActionFeed : MonoBehaviour
    {
        public static ActionFeed Instance { get; private set; }

#if !UNITY_SERVER
        private const string KillFeedContainerName = "ActionFeedContainer";
        private const string KillFeedEntryClassName = "action-feed-entry";
        private const long MessageDurationMs = 4000;

        private VisualElement _rootElement;
        private VisualElement _actionFeedContainer;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

#if !UNITY_SERVER
        private void OnEnable()
        {
            var document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                Debug.LogWarning(
                    "ActionFeed: UIDocument is unavailable.");
                return;
            }

            _rootElement = document.rootVisualElement;

            _actionFeedContainer =
                _rootElement.Q<VisualElement>(KillFeedContainerName);

            if (_actionFeedContainer == null)
            {
                Debug.LogWarning(
                    $"ActionFeed: Container '{KillFeedContainerName}' not found.");
            }
        }
#endif

        public void AnnouncePlayerJoined(string playerName)
        {
#if !UNITY_SERVER
            if (_actionFeedContainer == null)
                return;

            var killLabel = new Label($"{playerName} joined");
            killLabel.AddToClassList(KillFeedEntryClassName);

            _actionFeedContainer.Add(killLabel);

            killLabel.schedule.Execute(() =>
            {
                if (killLabel.parent == _actionFeedContainer)
                    killLabel.RemoveFromHierarchy();

            }).StartingIn(MessageDurationMs);
#endif
        }

        public void AnnounceKill(string killer, string victim)
        {
#if !UNITY_SERVER
            if (_actionFeedContainer == null)
                return;

            var killLabel = new Label($"{killer} killed {victim}");
            killLabel.AddToClassList(KillFeedEntryClassName);

            _actionFeedContainer.Add(killLabel);

            killLabel.schedule.Execute(() =>
            {
                if (killLabel.parent == _actionFeedContainer)
                    killLabel.RemoveFromHierarchy();

            }).StartingIn(MessageDurationMs);
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
