using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Exit / portal trigger. By default opens the room-choice UI when the exit is unlocked.
    /// Set a fixed destination and disable openRoomChoiceUi for direct hops (e.g. debug).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TransitionTrigger : MonoBehaviour
    {
        [Header("Room Choice (BlazBlue)")]
        [Tooltip("When true and exit is unlocked, walking in opens the next-room choice UI.")]
        [SerializeField] private bool openRoomChoiceUi = true;

        [Header("Fixed Destination (Optional)")]
        [Tooltip("Used only when openRoomChoiceUi is false.")]
        [SerializeField] private string targetRoomId;

        [Tooltip("Used only when openRoomChoiceUi is false.")]
        [SerializeField] private string targetSceneName;

        [SerializeField] private string targetEntranceId = "Default";

        [Header("Visuals (Optional)")]
        [SerializeField] private Color debugColor = new Color(0.2f, 0.8f, 1f, 0.4f);

        private bool hasTriggered = false;
        private IRoomExitGate exitGate;
        private Collider2D exitCollider;

        public Collider2D ExitCollider
        {
            get
            {
                if (exitCollider == null)
                {
                    exitCollider = GetComponent<Collider2D>();
                }

                return exitCollider;
            }
        }

        private void Awake()
        {
            exitCollider = GetComponent<Collider2D>();
        }

        public void SetExitGate(IRoomExitGate gate)
        {
            exitGate = gate;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasTriggered) return;

            PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
            if (player == null || player.IsDead)
            {
                return;
            }

            if (exitGate != null && !exitGate.IsExitAvailable)
            {
                Debug.Log("[TransitionTrigger] Exit is still locked; request ignored.", this);
                return;
            }

            if (openRoomChoiceUi)
            {
                hasTriggered = true;
                Debug.Log("[TransitionTrigger] Player reached exit. Opening room choice UI.", this);
                if (!RoomExitPresenter.TryShowChoiceUi())
                {
                    hasTriggered = false;
                    Debug.LogWarning("[TransitionTrigger] Failed to open room choice UI.", this);
                }

                return;
            }

            if (SceneTransitionManager.Instance == null)
            {
                Debug.LogError("[TransitionTrigger] SceneTransitionManager Instance is missing in the scene!");
                return;
            }

            hasTriggered = true;

            if (!string.IsNullOrWhiteSpace(targetRoomId))
            {
                Debug.Log($"[TransitionTrigger] Player entered exit portal. Transitioning to room '{targetRoomId}'");
                SceneTransitionManager.Instance.TransitionToRoom(targetRoomId, targetEntranceId);
                return;
            }

            Debug.Log($"[TransitionTrigger] Player entered exit portal. Transitioning to '{targetSceneName}'");
            SceneTransitionManager.Instance.TransitionToScene(targetSceneName, targetEntranceId);
        }

        private void OnDrawGizmos()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.color = debugColor;
                if (col is BoxCollider2D box)
                {
                    Gizmos.DrawCube(transform.position + (Vector3)box.offset, box.size);
                }
                else
                {
                    Gizmos.DrawSphere(transform.position, 0.5f);
                }
            }
        }
    }
}
