
using System.Collections.Generic;
using Gameplay.Leaderboard;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Unity.MP_FPS.UI
{
    public class LeaderboardUi : MonoBehaviour
    {
        public VisualTreeAsset ScoreItemTemplate;

        EntityManager _entityManager;
        World _currentClientWorld;
        EntityQuery _inGameQuery;
        VisualElement _rootElement;
        ListView _listView;

        readonly List<ScoreUiPlayerInfo> _items = new();

        private float _timer;

        void Awake()
        {
#if UNITY_SERVER
            // Dedicated server does not initialize client UI.
            return;
#else
            var uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogWarning("LeaderboardUi: UIDocument is unavailable.");
                enabled = false;
                return;
            }

            _rootElement = uiDocument.rootVisualElement;
            _rootElement.style.display = DisplayStyle.None;

            _listView = _rootElement.Q<ListView>("score-items");

            if (_listView == null || ScoreItemTemplate == null)
            {
                Debug.LogWarning(
                    "LeaderboardUi: ListView or ScoreItemTemplate is missing.");
                enabled = false;
                return;
            }

            _listView.itemsSource = _items;
            _listView.makeItem = () => ScoreItemTemplate.Instantiate();

            _listView.bindItem = (element, i) =>
            {
                if (i < 0 || i >= _items.Count)
                    return;

                var player = _items[i];

                var nameLabel = element.Q<Label>("name");
                var killsLabel = element.Q<Label>("kills");
                var deathsLabel = element.Q<Label>("deaths");

                if (nameLabel != null)
                    nameLabel.text = player.PlayerName;

                if (killsLabel != null)
                    killsLabel.text = player.Kills.ToString();

                if (deathsLabel != null)
                    deathsLabel.text = player.Deaths.ToString();
            };

            _listView.fixedItemHeight = 20;
            _listView.selectionType = SelectionType.None;
            _listView.RefreshItems();
#endif
        }

        void OnEnable()
        {
#if !UNITY_SERVER
            if (_rootElement == null || _listView == null)
                return;

            GameInput.Actions.UI.ShowLeaderboard.started += OnShowLeaderboard;
            GameInput.Actions.UI.ShowLeaderboard.canceled += OnHideLeaderboard;
#endif
        }

        void OnDisable()
        {
#if !UNITY_SERVER
            if (GameInput.Actions != null &&
                GameInput.Actions.UI != null &&
                GameInput.Actions.UI.ShowLeaderboard != null)
            {
                GameInput.Actions.UI.ShowLeaderboard.started -= OnShowLeaderboard;
                GameInput.Actions.UI.ShowLeaderboard.canceled -= OnHideLeaderboard;
            }

            if (_inGameQuery != default && _entityManager != default &&
                _entityManager.IsCreated)
            {
                _inGameQuery.Dispose();
            }
#endif
        }

#if !UNITY_SERVER
        private void OnShowLeaderboard(InputAction.CallbackContext context)
        {
            if (_rootElement != null)
                _rootElement.style.display = DisplayStyle.Flex;
        }

        private void OnHideLeaderboard(InputAction.CallbackContext context)
        {
            if (_rootElement != null)
                _rootElement.style.display = DisplayStyle.None;
        }

        void InitializeClientWorld(World clientWorld)
        {
            if (_inGameQuery != default && _entityManager != default &&
                _entityManager.IsCreated)
            {
                _inGameQuery.Dispose();
            }

            if (clientWorld != null && clientWorld.IsCreated)
            {
                _entityManager = clientWorld.EntityManager;

                _inGameQuery = _entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<NetworkStreamInGame>(),
                    ComponentType.ReadOnly<NetworkStreamConnection>()
                );

                if (_rootElement != null)
                    _rootElement.style.display = DisplayStyle.None;
            }
            else
            {
                _entityManager = default;
                _inGameQuery = default;
            }

            _currentClientWorld = clientWorld;
        }

        static World GetClientWorld()
        {
            const string clientWorldName = "ClientWorld";

            for (var i = 0; i < World.All.Count; i++)
            {
                if (World.All[i].Name == clientWorldName &&
                    World.All[i].IsCreated)
                {
                    return World.All[i];
                }
            }

            return null;
        }

        void Update()
        {
            if (_rootElement == null || _listView == null)
                return;

            var clientWorld = GetClientWorld();

            if (clientWorld != _currentClientWorld)
                InitializeClientWorld(clientWorld);

            if (_currentClientWorld == null ||
                !_currentClientWorld.IsCreated ||
                _entityManager == default ||
                !_entityManager.IsCreated)
            {
                _rootElement.style.display = DisplayStyle.None;
                return;
            }

            var isInGame = _inGameQuery.CalculateEntityCount() != 0;

            if (!isInGame)
            {
                _rootElement.style.display = DisplayStyle.None;
                return;
            }

            if (_rootElement.style.display == DisplayStyle.None)
                return;

            _timer += Time.deltaTime;

            if (_timer < 0.5f)
                return;

            _timer = 0.0f;
            UpdateLeaderboardUI();
        }

        private void UpdateLeaderboardUI()
        {
            if (LeaderboardManager.Instance == null ||
                _listView == null)
                return;

            var scores = LeaderboardManager.Instance.GetScores();

            scores.Sort((a, b) =>
            {
                int killComparison = b.Kills.CompareTo(a.Kills);

                if (killComparison != 0)
                    return killComparison;

                return a.Deaths.CompareTo(b.Deaths);
            });

            _items.Clear();

            foreach (var score in scores)
            {
                _items.Add(new ScoreUiPlayerInfo(
                    score.NetworkId,
                    score.PlayerName.ToString(),
                    score.Kills,
                    score.Deaths
                ));
            }

            _listView.RefreshItems();
        }
#endif
    }

    public class ScoreUiPlayerInfo
    {
        public int PlayerId;
        public string PlayerName;
        public int Kills;
        public int Deaths;

        public ScoreUiPlayerInfo(int id, string name, int kills, int deaths)
        {
            PlayerId = id;
            PlayerName = name;
            Kills = kills;
            Deaths = deaths;
        }
    }
}
