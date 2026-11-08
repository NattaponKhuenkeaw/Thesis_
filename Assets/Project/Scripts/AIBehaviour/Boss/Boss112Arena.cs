using UnityEngine;
using Unity.Cinemachine;

namespace Project.AIBehaviour.Boss
{
    /// <summary>Authored room bounds shared by the boss movement and its physical arena.</summary>
    public sealed class Boss112Arena : MonoBehaviour
    {
        [SerializeField] private Collider2D floor;
        [SerializeField] private Collider2D leftWall;
        [SerializeField] private Collider2D rightWall;
        [SerializeField] private Transform departurePoint;
        private CinemachineCamera arenaCamera;
        private bool encounterStarted;

        private void Awake()
        {
            arenaCamera = GetComponentInChildren<CinemachineCamera>(true);
            UseEntranceCamera();
        }

        private void Start() => UseEntranceCamera();

        private void UseEntranceCamera()
        {
            // Follow the player down the corridor; the authored arena view takes over at the fight trigger.
            if (!encounterStarted && arenaCamera != null && CameraManager.instance != null)
                arenaCamera.enabled = false;
        }

        public void ConfigureBoss(Boss112Controller boss)
        {
            if (boss == null || floor == null || leftWall == null || rightWall == null) return;
            encounterStarted = true;
            if (arenaCamera == null) arenaCamera = GetComponentInChildren<CinemachineCamera>(true);
            if (arenaCamera != null) arenaCamera.enabled = true;
            float ground = floor.bounds.max.y;
            boss.ConfigureArena(leftWall.bounds.max.x, rightWall.bounds.min.x, ground,
                departurePoint != null ? departurePoint.position.y - ground : 23f,
                arenaCamera);
        }
    }
}
