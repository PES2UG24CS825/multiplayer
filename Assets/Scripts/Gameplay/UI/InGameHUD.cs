
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using Unity.NetCode;

namespace Unity.MP_FPS
{
    [RequireComponent(typeof(UIDocument))]
    public class InGameHUD : MonoBehaviour
    {
        [Header("Reticle Configuration")]
        [SerializeField]
        private float reticleBaseSize = 80f;

        private VisualElement m_RootElement;
        private ProgressBar m_HealthBar;
        private VisualElement m_PlayerHealthBarFill;
        private ProgressBar m_AmmoBar;
        private Label m_AmmoLabel;
        private Label m_ReloadingLabel;
        private VisualElement m_Reticle;

        private float m_shotFeedbackTimer = 0f;

        private const float k_ShotFeedbackDuration = 0.1f;

        private static readonly Color k_HealthBarColor =
            new Color(0.29f, 0.83f, 0.43f, 0.75f);

        private const string k_CrossReticleClass =
            "kits-reticle-cross";

        private const string k_TCrossReticleClass =
            "kits-reticle-tcross";

        private const string k_CircularCrossClass =
            "kits-reticle-circularcross";

        private const string k_OpenCircularReticleClass =
            "kits-reticle-opencircular";

        private World m_ClientWorld;
        private EntityManager m_EntityManager;
        private EntityQuery m_LocalPlayerQuery;

        private bool m_QueryInitialized;

        void OnEnable()
        {
#if UNITY_SERVER
            return;
#else
            UIDocument document = GetComponent<UIDocument>();

            if (document == null)
            {
                Debug.LogError("[InGameHUD] UIDocument is missing.");
                return;
            }

            m_RootElement = document.rootVisualElement;

            if (m_RootElement == null)
            {
                Debug.LogError("[InGameHUD] Root VisualElement is missing.");
                return;
            }

            m_HealthBar =
                m_RootElement.Q<ProgressBar>("player-health-bar");

            if (m_HealthBar != null)
            {
                m_PlayerHealthBarFill =
                    m_HealthBar.Q<VisualElement>(
                        null,
                        "unity-progress-bar__progress");
            }

            m_AmmoLabel =
                m_RootElement.Q<Label>("ammo-label");

            m_AmmoBar =
                m_RootElement.Q<ProgressBar>("player-ammo-bar");

            m_ReloadingLabel =
                m_RootElement.Q<Label>("reloading-label");

            m_Reticle =
                m_RootElement.Q<VisualElement>("player-reticle");

            m_QueryInitialized = false;
            m_ClientWorld = null;
#endif
        }

        void LateUpdate()
        {
#if UNITY_SERVER
            return;
#else
            if (m_RootElement == null)
                return;

            if (GameSettings.Instance == null)
                return;

            bool isInGame =
                GameSettings.Instance.GameState ==
                GlobalGameState.InGame;

            m_RootElement.style.display =
                isInGame ? DisplayStyle.Flex : DisplayStyle.None;

            if (!isInGame)
                return;

            // Check whether the previous client world still exists.
            if (m_ClientWorld == null || !m_ClientWorld.IsCreated)
            {
                ResetEcsReferences();

                if (!InitializeEcs())
                {
                    m_RootElement.style.display = DisplayStyle.None;
                    return;
                }
            }

            if (!m_QueryInitialized)
            {
                m_RootElement.style.display = DisplayStyle.None;
                return;
            }

            try
            {
                if (!m_ClientWorld.IsCreated)
                {
                    ResetEcsReferences();
                    return;
                }

                if (m_LocalPlayerQuery.IsEmptyIgnoreFilter)
                {
                    // No local player entity is currently available.
                    m_RootElement.style.display = DisplayStyle.None;
                    return;
                }

                PredictedPlayerGhost playerData =
                    m_LocalPlayerQuery.GetSingleton<PredictedPlayerGhost>();

                UpdateHealthBar(playerData);
                UpdateAmmo(playerData);
                UpdateReloading(playerData);
                UpdateReticleVisual(playerData);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    "[InGameHUD] ECS update postponed: " +
                    exception.Message);

                ResetEcsReferences();
                m_RootElement.style.display = DisplayStyle.None;
            }
#endif
        }

        private bool InitializeEcs()
        {
#if UNITY_SERVER
            return false;
#else
            ResetEcsReferences();

            try
            {
                foreach (World world in World.All)
                {
                    if (world == null || !world.IsCreated)
                        continue;

                    if (!world.IsClient())
                        continue;

                    m_ClientWorld = world;
                    m_EntityManager = world.EntityManager;

                    m_LocalPlayerQuery =
                        m_EntityManager.CreateEntityQuery(
                            ComponentType.ReadOnly<PredictedPlayerGhost>(),
                            ComponentType.ReadOnly<GhostOwnerIsLocal>()
                        );

                    m_QueryInitialized = true;

                    Debug.Log(
                        "[InGameHUD] Client ECS query initialized.");

                    return true;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    "[InGameHUD] Client ECS initialization failed: " +
                    exception.Message);

                ResetEcsReferences();
            }

            return false;
#endif
        }

        private void ResetEcsReferences()
        {
            m_QueryInitialized = false;
            m_ClientWorld = null;
            m_EntityManager = default;
            m_LocalPlayerQuery = default;
        }

        private void UpdateHealthBar(PredictedPlayerGhost playerData)
        {
            if (m_HealthBar == null)
                return;

            float maxHealth =
                playerData.MaxHealth > 0
                    ? playerData.MaxHealth
                    : 100f;

            m_HealthBar.highValue = maxHealth;
            m_HealthBar.value = playerData.CurrentHealth;

            float healthPercent =
                Mathf.Clamp01(playerData.CurrentHealth / maxHealth);

            Color healthColor =
                Color.Lerp(Color.red, k_HealthBarColor, healthPercent);

            if (m_PlayerHealthBarFill != null)
            {
                m_PlayerHealthBarFill.style.backgroundColor =
                    healthColor;
            }
        }

        private void UpdateAmmo(PredictedPlayerGhost playerData)
        {
            if (m_AmmoLabel == null && m_AmmoBar == null)
                return;

            int magazineSize = 0;

            if (WeaponManager.Instance != null &&
                WeaponManager.Instance.WeaponRegistry != null)
            {
                var weaponData =
                    WeaponManager.Instance.WeaponRegistry.GetWeaponData(
                        playerData.EquippedWeaponID);

                if (weaponData != null)
                    magazineSize = weaponData.MagazineSize;
            }

            if (m_AmmoLabel != null)
            {
                m_AmmoLabel.text =
                    $"{playerData.CurrentAmmo} / {magazineSize}";

                if (playerData.CurrentAmmo == 0)
                    m_AmmoLabel.style.color = Color.red;
                else if (playerData.CurrentAmmo <= magazineSize * 0.3f)
                    m_AmmoLabel.style.color = Color.yellow;
                else
                    m_AmmoLabel.style.color = Color.white;
            }

            if (m_AmmoBar != null)
            {
                m_AmmoBar.highValue =
                    Mathf.Max(1, magazineSize);

                m_AmmoBar.value =
                    playerData.CurrentAmmo;
            }
        }

        private void UpdateReloading(PredictedPlayerGhost playerData)
        {
            if (m_ReloadingLabel == null)
                return;

            m_ReloadingLabel.style.display =
                playerData.ControllerState.IsReloadingState
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        private void UpdateReticleVisual(PredictedPlayerGhost playerData)
        {
            if (m_Reticle == null)
                return;

            if (m_shotFeedbackTimer > 0)
                m_shotFeedbackTimer -= Time.deltaTime;

            if (WeaponManager.Instance == null ||
                WeaponManager.Instance.WeaponRegistry == null)
            {
                m_Reticle.style.display = DisplayStyle.None;
                return;
            }

            var weaponData =
                WeaponManager.Instance.WeaponRegistry.GetWeaponData(
                    playerData.EquippedWeaponID);

            if (weaponData == null)
            {
                m_Reticle.style.display = DisplayStyle.None;
                return;
            }

            string desiredClass =
                GetReticleClassName(weaponData.ReticleType);

            m_Reticle.RemoveFromClassList(k_CrossReticleClass);
            m_Reticle.RemoveFromClassList(k_TCrossReticleClass);
            m_Reticle.RemoveFromClassList(k_OpenCircularReticleClass);
            m_Reticle.RemoveFromClassList(k_CircularCrossClass);

            if (!string.IsNullOrEmpty(desiredClass))
                m_Reticle.AddToClassList(desiredClass);

            m_Reticle.style.display = DisplayStyle.Flex;
            m_Reticle.style.width = reticleBaseSize;
            m_Reticle.style.height = reticleBaseSize;

            bool isReloading =
                playerData.ControllerState.IsReloadingState;

            bool justFired =
                playerData.ControllerState.Shoot;

            bool isWeaponOnCooldown =
                playerData.WeaponCooldown < weaponData.CooldownInMs;

            if (justFired)
                m_shotFeedbackTimer = k_ShotFeedbackDuration;

            bool greyReticleVisual = false;

            if (isReloading)
            {
                greyReticleVisual = true;
                m_shotFeedbackTimer = 0f;
            }
            else if (isWeaponOnCooldown &&
                     m_shotFeedbackTimer <= 0)
            {
                greyReticleVisual = true;
            }

            m_Reticle.style.unityBackgroundImageTintColor =
                greyReticleVisual
                    ? new StyleColor(Color.grey)
                    : new StyleColor(Color.white);
        }

        private string GetReticleClassName(ReticleType reticleType)
        {
            switch (reticleType)
            {
                case ReticleType.Cross:
                    return k_CrossReticleClass;

                case ReticleType.TCross:
                    return k_TCrossReticleClass;

                case ReticleType.OpenCircular:
                    return k_OpenCircularReticleClass;

                case ReticleType.CircularCross:
                    return k_CircularCrossClass;

                default:
                    return "";
            }
        }
    }
}
