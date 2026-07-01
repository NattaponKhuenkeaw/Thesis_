using UnityEngine;
using UnityEngine.InputSystem;
using Project.UI;
using Project.TeamBuilder.UI;

namespace Project.DebugTools
{
    /// <summary>
    /// A quick debug tool to forcefully clear any locks, UI action maps, or canvas raycast blockers
    /// that might be preventing the player from moving or attacking.
    /// Press F12 to trigger it in-game.
    /// </summary>
    public class InputDebugResetter : MonoBehaviour
    {
        [Header("Settings")]
        public Key debugKey = Key.F12;
        
        // Automatically create this debug object in every scene without needing to add it manually.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            var go = new GameObject("InputDebugResetter_Auto");
            go.AddComponent<InputDebugResetter>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current[debugKey].wasPressedThisFrame)
            {
                ForceResetAll();
            }
        }

        public void ForceResetAll()
        {
            Debug.Log("<color=red>--- INPUT DEBUG RESET TRIGGERED (F12) ---</color>");

            // 1. Reset InputLockManager
            bool wasLocked = InputLockManager.IsLocked;
            InputLockManager.ResetLocks();
            Debug.Log($"1. InputLockManager: WasLocked={wasLocked} -> NowLocked={InputLockManager.IsLocked}");

            // 2. Force PlayerInput back to 'Player' Action Map
            var playerInput = FindFirstObjectByType<PlayerInput>();
            if (playerInput != null)
            {
                string oldMap = playerInput.currentActionMap != null ? playerInput.currentActionMap.name : "null";
                playerInput.SwitchCurrentActionMap("Player");
                Debug.Log($"2. PlayerInput ActionMap: {oldMap} -> {playerInput.currentActionMap.name}");
            }
            else
            {
                Debug.LogWarning("2. PlayerInput not found in scene!");
            }

            // 3. Clear TeamUI stuck states and disable its raycast blocking
            var teamUI = FindFirstObjectByType<TeamUI>(FindObjectsInactive.Include);
            if (teamUI != null)
            {
                var canvasGroup = teamUI.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    bool wasBlocking = canvasGroup.blocksRaycasts;
                    canvasGroup.blocksRaycasts = false;
                    Debug.Log($"3. TeamUI blocksRaycasts: {wasBlocking} -> FALSE.");
                }
            }

            // 4. Force InspectScreenUI raycasts off just in case
            var inspectScreen = FindFirstObjectByType<InspectScreenUI>(FindObjectsInactive.Include);
            if (inspectScreen != null)
            {
                var cg = inspectScreen.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    bool wasBlocking = cg.blocksRaycasts;
                    cg.blocksRaycasts = false;
                    cg.interactable = false;
                    cg.alpha = 0f;
                    Debug.Log($"4. InspectScreenUI blocksRaycasts: {wasBlocking} -> FALSE.");
                }
                inspectScreen.gameObject.SetActive(false);
            }

            // 5. Clear player state machine input buffers
            var stateMachine = FindFirstObjectByType<PlayerStateMachine>();
            if (stateMachine != null && stateMachine.InputBuffer != null)
            {
                stateMachine.InputBuffer.ClearAll();
                Debug.Log("5. PlayerStateMachine InputBuffer cleared.");
            }

            Debug.Log("<color=green>--- INPUT DEBUG RESET COMPLETE ---</color>");
        }
    }
}
