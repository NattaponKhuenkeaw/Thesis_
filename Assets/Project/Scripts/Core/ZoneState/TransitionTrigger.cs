using UnityEngine;
using Project.UI;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Exit / portal trigger. By default opens the room-choice UI while the player stands in the zone
    /// (proximity show/hide, like Shop/Level/Rest floating prompts). Walking away dismisses the UI.
    /// Set a fixed destination and disable openRoomChoiceUi for direct hops (e.g. debug).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TransitionTrigger : MonoBehaviour
    {
        [Header("Room Choice (BlazBlue)")]
        [Tooltip("When true and exit is unlocked, standing in the exit shows next-room choices. Leaving hides them.")]
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

        /// <summary>
        /// Allows the player to re-enter the exit after a failed leave attempt.
        /// </summary>
        public void ResetTrigger()
        {
            hasTriggered = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHandlePlayerExit(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // Keep offers visible while overlapping; also covers ResetTrigger while still inside.
            TryHandlePlayerExit(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!openRoomChoiceUi)
            {
                return;
            }

            PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
            if (player == null)
            {
                return;
            }

            RoomChoiceUI ui = RoomChoiceUI.Instance;
            if (ui != null)
            {
                ui.HideFromProximityExit();
            }
        }

        private void TryHandlePlayerExit(Collider2D other)
        {
            PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
            if (player == null || player.IsDead)
            {
                return;
            }

            if (exitGate != null && !exitGate.IsExitAvailable)
            {
                return;
            }

            if (openRoomChoiceUi)
            {
                // Proximity UI: do not latch hasTriggered — leaving the zone dismisses the panel.
                RoomExitPresenter.TryShowChoiceUi();
                return;
            }

            if (hasTriggered)
            {
                return;
            }

            if (SceneTransitionManager.Instance == null)
            {
                Debug.LogError("[TransitionTrigger] SceneTransitionManager Instance is missing in the scene!");
                return;
            }

            bool started;
            if (!string.IsNullOrWhiteSpace(targetRoomId))
            {
                Debug.Log($"[TransitionTrigger] Player entered exit portal. Transitioning to room '{targetRoomId}'");
                started = SceneTransitionManager.Instance.TryTransitionToRoom(targetRoomId, targetEntranceId);
            }
            else
            {
                Debug.Log($"[TransitionTrigger] Player entered exit portal. Transitioning to '{targetSceneName}'");
                started = SceneTransitionManager.Instance.TryTransitionToScene(targetSceneName, targetEntranceId);
            }

            if (!started)
            {
                Debug.LogWarning("[TransitionTrigger] Transition did not start; exit trigger left armed.", this);
                return;
            }

            hasTriggered = true;
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
