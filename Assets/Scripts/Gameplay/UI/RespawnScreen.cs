
using UnityEngine;

#if !UNITY_SERVER
using Unity.Entities;
using Unity.NetCode;
using UnityEngine.UIElements;
#endif

namespace Unity.MP_FPS.UI
{
#if !UNITY_SERVER
    [RequireComponent(typeof(UIDocument))]
#endif
    public class RespawnScreen : MonoBehaviour
    {
#if !UNITY_SERVER
        public Camera RespawnCamera;

        private VisualElement m_RespawnScreen;
        private Label m_RespawnTimerLabel;

        private World m_ClientWorld;
        private EntityManager m_EntityManager;
        private EntityQuery m_LocalPlayerQuery;

        private float m_RespawnCountdown;
        private const float RESPAWN_DURATION = 5.0f;

        private void Awake()
        {
            if (RespawnCamera != null)
            {
                RespawnCamera.gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            var document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                Debug.LogWarning("RespawnScreen: UIDocument unavailable.");
                return;
            }

            m_RespawnScreen = document.rootVisualElement;
            m_RespawnTimerLabel =
                m_RespawnScreen.Q<Label>("RespawnMessage");

            if (m_RespawnTimerLabel == null)
            {
                Debug.LogWarning("RespawnScreen: RespawnMessage label missing.");
                enabled = false;
            }
        }

        private void InitializeEcs()
        {
            foreach (var world in World.All)
            {
                if (world.IsCreated && world.IsClient())
                {
                    m_ClientWorld = world;
                    m_EntityManager = world.EntityManager;
                    break;
                }
            }

            if (m_ClientWorld != null && m_EntityManager.IsCreated)
            {
                m_LocalPlayerQuery =
                    m_EntityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<PredictedPlayerGhost>(),
                        ComponentType.ReadOnly<GhostOwnerIsLocal>()
                    );
            }
        }

        void LateUpdate()
        {
            if (m_RespawnScreen == null ||
                m_RespawnTimerLabel == null)
                return;

            if (GameSettings.Instance == null)
                return;

            if (GameSettings.Instance.GameState != GlobalGameState.InGame)
            {
                m_RespawnScreen.style.display = DisplayStyle.None;
                return;
            }

            if (m_ClientWorld == null || !m_ClientWorld.IsCreated)
            {
                InitializeEcs();

                if (m_ClientWorld == null ||
                    !m_ClientWorld.IsCreated ||
                    !_QueryIsCreated())
                    return;
            }

            bool isPlayerAlive =
                m_LocalPlayerQuery.HasSingleton<PredictedPlayerGhost>();

            if (isPlayerAlive)
            {
                if (RespawnCamera != null)
                    RespawnCamera.gameObject.SetActive(false);

                m_RespawnScreen.style.display = DisplayStyle.None;
            }
            else
            {
                if (RespawnCamera != null)
                    RespawnCamera.gameObject.SetActive(true);

                if (m_RespawnScreen.style.display != DisplayStyle.Flex)
                {
                    m_RespawnCountdown = RESPAWN_DURATION;
                    m_RespawnScreen.style.display = DisplayStyle.Flex;
                }

                m_RespawnCountdown -= Time.deltaTime;

                if (m_RespawnCountdown < 0)
                    m_RespawnCountdown = 0;

                m_RespawnTimerLabel.text =
                    $"RESPAWNING IN {Mathf.CeilToInt(m_RespawnCountdown)}";
            }
        }

        private bool _QueryIsCreated()
        {
            return m_EntityManager.IsCreated;
        }

        private void OnDisable()
        {
            if (m_EntityManager.IsCreated)
            {
                m_LocalPlayerQuery.Dispose();
            }
        }
#endif
    }
}
