using System.Collections.Generic;
using UnityEngine;
using Project.Core.RunEnd;

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

            bool hasTrackedEnemy = false;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                hasTrackedEnemy = true;
                if (enemy.IsAlive)
                {
                    return;
                }
            }

            // Empty / all-null lists must not free-clear. OnEnable used to call RevealExits
            // immediately on Manager.prefab and Test_Rest, unlocking exits and advancing offers.
            if (!hasTrackedEnemy)
            {
                return;
            }

            roomCleared = true;
            NotifyRunEndRoomCleared();
            RoomExitPresenter.RevealExits(portalsRoot, unlockSceneExitOnClear);
        }

        private void NotifyRunEndRoomCleared()
        {
            RunEndController runEndController = RunEndController.Instance ?? FindFirstObjectByType<RunEndController>();
            if (runEndController == null)
            {
                Debug.LogWarning("[RoomClearManager] Room cleared but RunEndController is missing; room reward was not granted.", this);
                return;
            }

            runEndController.NotifyRoomCleared();
        }
    }
}
