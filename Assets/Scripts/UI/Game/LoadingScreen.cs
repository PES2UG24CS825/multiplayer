
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
    public class LoadingScreen : MonoBehaviour
    {
#if !UNITY_SERVER
        static class UIElementNames
        {
            public const string LoadingStatus = "LoadingStatus";
        }

        void OnEnable()
        {
            var document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                Debug.LogWarning(
                    "LoadingScreen: UIDocument is unavailable.");
                return;
            }

            var root = document.rootVisualElement;

            var progressBar = root.Q<ProgressBar>();
            var label = root.Q<Label>(UIElementNames.LoadingStatus);

            if (progressBar == null || label == null)
            {
                Debug.LogWarning(
                    "LoadingScreen: ProgressBar or LoadingStatus label is missing.");
                return;
            }

            root.SetBinding("style.display", new DataBinding
            {
                dataSource = GameSettings.Instance,
                dataSourcePath = new PropertyPath(
                    GameSettings.LoadingScreenStylePropertyName),
                bindingMode = BindingMode.ToTarget,
            });

            progressBar.SetBinding("value", new DataBinding
            {
                dataSource = LoadingData.Instance,
                dataSourcePath = new PropertyPath(
                    LoadingData.LoadingProgressPropertyName),
                bindingMode = BindingMode.ToTarget,
            });

            label.SetBinding("text", new DataBinding
            {
                dataSource = LoadingData.Instance,
                dataSourcePath = new PropertyPath(
                    LoadingData.LoadingStatusTextPropertyName),
                bindingMode = BindingMode.ToTarget,
            });
        }
#endif
    }
}
