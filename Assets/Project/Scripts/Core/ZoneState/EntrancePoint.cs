using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Component placed in the scene to represent an entrance point for the player team.
    /// Marks the spawn position and the target stop position where control is returned to the player.
    /// </summary>
    public class EntrancePoint : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("The ID of this entrance, useful if a scene has multiple entrances (e.g. Left/Right)")]
        [SerializeField] private string entranceId = "Default";

        [Tooltip("The transform representing the destination point the character will walk to.")]
        [SerializeField] private Transform stopPoint;

        public string EntranceId => entranceId;
        public Vector3 SpawnPosition => transform.position;
        public Vector3 StopPosition => stopPoint != null ? stopPoint.position : transform.position;

        private void OnDrawGizmos()
        {
            // Spawn point visualization (green sphere)
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.position, 0.3f);
            
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"Entrance: {entranceId}");
#endif

            // Destination point visualization (red sphere and path line)
            if (stopPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(stopPoint.position, 0.3f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, stopPoint.position);
            }
        }
    }
}
