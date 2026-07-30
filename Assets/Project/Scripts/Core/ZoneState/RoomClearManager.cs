using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Watches the enemies assigned to this room and reveals exits after all are defeated.
    /// </summary>
    public class RoomClearManager : MonoBehaviour
    {
        [Header("Room Enemies")]
        [SerializeField] private List<EnemyController> enemies = new List<EnemyController>();

        [Header("Room Exits")]
        [SerializeField] private GameObject portalsRoot;
        [SerializeField] private bool hidePortalsOnStart = true;
        [SerializeField] private bool unlockSceneExitOnClear = true;

        private bool roomCleared;

        private void Awake()
        {
            if (hidePortalsOnStart && portalsRoot != null)
            {
                portalsRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            SubscribeEnemies();
            CheckRoomClear();
        }

        private void OnDisable()
        {
            UnsubscribeEnemies();
        }

        private void SubscribeEnemies()
        {
            if (enemies == null) return;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == null) continue;
                enemies[i].OnDied -= HandleEnemyDied;
                enemies[i].OnDied += HandleEnemyDied;
            }
        }

        private void UnsubscribeEnemies()
        {
            if (enemies == null) return;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == null) continue;
                enemies[i].OnDied -= HandleEnemyDied;
            }
        }

        private void HandleEnemyDied()
        {
            CheckRoomClear();
        }

        private void CheckRoomClear()
        {
            if (roomCleared || enemies == null)
            {
                return;
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy != null && enemy.IsAlive)
                {
                    return;
                }
            }

            roomCleared = true;

            if (unlockSceneExitOnClear)
            {
                if (SceneTransitionManager.Instance == null
                    || !SceneTransitionManager.Instance.SetCurrentRoomExitAvailable(true))
                {
                    Debug.LogWarning("[RoomClearManager] Current scene exit could not be unlocked.", this);
                }
            }

            if (portalsRoot != null)
            {
                portalsRoot.SetActive(true);
            }
        }
    }
}
