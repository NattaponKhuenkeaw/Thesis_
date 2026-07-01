using UnityEngine;
using UnityEngine.UI;
using Project.Core.ZoneState;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Attach this component to a UI Button. It will automatically bind its onClick 
    /// event to trigger a scene transition using the active SceneTransitionManager instance,
    /// avoiding inspector reference loss due to singleton destruction.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TransitionButtonHelper : MonoBehaviour
    {
        [Header("Destination Settings")]
        [Tooltip("The target scene to transition to.")]
        [SerializeField] private string targetSceneName;

        [Tooltip("Optional entrance ID to spawn at.")]
        [SerializeField] private string targetEntranceId = "Default";

        private void Awake()
        {
            Button btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(OnButtonClicked);
                btn.onClick.AddListener(OnButtonClicked);
            }
        }

        private void OnButtonClicked()
        {
            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning($"[TransitionButtonHelper] Target scene name on button {gameObject.name} is empty!");
                return;
            }

            if (SceneTransitionManager.Instance != null)
            {
                Debug.Log($"[TransitionButtonHelper] Button clicked. Transitioning to scene: {targetSceneName}");
                SceneTransitionManager.Instance.TransitionToScene(targetSceneName, targetEntranceId);
            }
            else
            {
                Debug.LogError("[TransitionButtonHelper] SceneTransitionManager.Instance is null! Cannot transition.");
            }
        }
    }
}
