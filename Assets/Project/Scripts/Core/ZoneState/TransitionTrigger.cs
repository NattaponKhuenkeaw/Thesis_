using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Place on a GameObject with a 2D Trigger Collider to represent a door or portal.
    /// When the player overlaps, triggers transition to the target scene.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TransitionTrigger : MonoBehaviour
    {
        [Header("Scene Transition Settings")]
        [Tooltip("The name of the scene to load.")]
        [SerializeField] private string targetSceneName;

        [Tooltip("Optional entrance ID to spawn at in the target scene.")]
        [SerializeField] private string targetEntranceId = "Default";

        [Header("Visuals (Optional)")]
        [SerializeField] private Color debugColor = new Color(0.2f, 0.8f, 1f, 0.4f);

        private bool hasTriggered = false;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasTriggered) return;

            // Check if the overlapping object is the player character
            PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
            if (player != null && !player.IsDead)
            {
                hasTriggered = true;
                
                if (SceneTransitionManager.Instance != null)
                {
                    Debug.Log($"[TransitionTrigger] Player entered exit portal. Transitioning to '{targetSceneName}'");
                    SceneTransitionManager.Instance.TransitionToScene(targetSceneName, targetEntranceId);
                }
                else
                {
                    Debug.LogError("[TransitionTrigger] SceneTransitionManager Instance is missing in the scene!");
                }
            }
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
