using System.Collections.Generic;
using UnityEngine;
using Project.UI;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Room clear unlocks the exit. Choice UI opens when the player reaches the exit trigger.
    /// </summary>
    public static class RoomExitPresenter
    {
        /// <summary>
        /// Called on room clear: unlock exit / show portal visuals only. Does not open UI.
        /// </summary>
        public static void RevealExits(GameObject portalsRoot, bool unlockSceneExit)
        {
            if (unlockSceneExit)
            {
                if (SceneTransitionManager.Instance == null
                    || !SceneTransitionManager.Instance.SetCurrentRoomExitAvailable(true))
                {
                    Debug.LogWarning("[RoomExitPresenter] Current scene exit could not be unlocked.");
                }
            }

            if (portalsRoot != null)
            {
                portalsRoot.SetActive(true);
            }

            PrepareOffers();
        }

        /// <summary>
        /// Rolls and caches next-room offers after a clear (UI still stays closed).
        /// </summary>
        public static void PrepareOffers(string excludeRoomId = null)
        {
            EnsureDirector();
            RoomChoiceDirector director = RoomChoiceDirector.Instance
                ?? Object.FindFirstObjectByType<RoomChoiceDirector>();

            if (director == null)
            {
                Debug.LogWarning("[RoomExitPresenter] RoomChoiceDirector missing; cannot prepare room choices.");
                return;
            }

            IReadOnlyList<RoomChoiceOffer> offers = director.GenerateOffersAfterClear(excludeRoomId);
            if (offers == null || offers.Count == 0)
            {
                Debug.LogWarning("[RoomExitPresenter] No room offers generated. Add more RoomData entries to MapSO.");
            }
        }

        /// <summary>
        /// Prepares leave-offers for SafeZone rooms without advancing the shop clear counter.
        /// </summary>
        public static void PrepareOffersForSafeRoom(string excludeRoomId = null)
        {
            EnsureDirector();
            RoomChoiceDirector director = RoomChoiceDirector.Instance
                ?? Object.FindFirstObjectByType<RoomChoiceDirector>();

            if (director == null)
            {
                Debug.LogWarning("[RoomExitPresenter] RoomChoiceDirector missing; cannot prepare safe-room choices.");
                return;
            }

            IReadOnlyList<RoomChoiceOffer> offers = director.GenerateOffers(excludeRoomId, includeShop: false);
            if (offers == null || offers.Count == 0)
            {
                Debug.LogWarning("[RoomExitPresenter] No leave offers generated for safe room.");
            }
        }

        /// <summary>
        /// Called when the player walks into the exit: open the choice UI.
        /// </summary>
        public static bool TryShowChoiceUi()
        {
            EnsureDirector();
            RoomChoiceDirector director = RoomChoiceDirector.Instance
                ?? Object.FindFirstObjectByType<RoomChoiceDirector>();

            if (director == null)
            {
                Debug.LogWarning("[RoomExitPresenter] RoomChoiceDirector missing; cannot show room choice UI.");
                return false;
            }

            IReadOnlyList<RoomChoiceOffer> offers = director.LastOffers;
            if (offers == null || offers.Count == 0)
            {
                // Fallback if exit was reached without a prior clear-prep (e.g. debug).
                offers = director.GenerateOffersAfterClear();
            }

            if (offers == null || offers.Count == 0)
            {
                Debug.LogWarning("[RoomExitPresenter] No room offers to show.");
                return false;
            }

            RoomChoiceUI ui = RoomChoiceUI.EnsureInstance();
            if (ui == null)
            {
                return false;
            }

            if (ui.IsShowing)
            {
                return true;
            }

            ui.Show(offers);
            return true;
        }

        private static void EnsureDirector()
        {
            RunProgressionManager.EnsureInstance();

            if (RoomChoiceDirector.Instance != null)
            {
                return;
            }

            if (Object.FindFirstObjectByType<RoomChoiceDirector>() != null)
            {
                return;
            }

            GameObject go = new GameObject("RoomChoiceDirector");
            go.AddComponent<RoomChoiceDirector>();
            Debug.Log("[RoomExitPresenter] Auto-created RoomChoiceDirector.");
        }
    }
}
