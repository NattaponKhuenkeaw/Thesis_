using UnityEngine;
using UnityEngine.UI;

namespace Project.Core.ZoneState
{
    [RequireComponent(typeof(Button))]
    public class TransitionButtonHelper : MonoBehaviour
    {
        [Header("Destination Settings")]
        [SerializeField] private string targetRoomId;
        [SerializeField] private string targetSceneName;
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
            if (SceneTransitionManager.Instance == null)
            {
                Debug.LogError("[TransitionButtonHelper] SceneTransitionManager.Instance is null! Cannot transition.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(targetRoomId))
            {
                SceneTransitionManager.Instance.TransitionToRoom(targetRoomId, targetEntranceId);
                return;
            }

            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning($"[TransitionButtonHelper] Both targetRoomId and targetSceneName on button {gameObject.name} are empty!");
                return;
            }

            SceneTransitionManager.Instance.TransitionToScene(targetSceneName, targetEntranceId);
        }
    }
}
