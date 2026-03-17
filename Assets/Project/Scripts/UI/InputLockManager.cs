using UnityEngine;

namespace Project.UI
{
    /// <summary>
    /// Centralized manager to track if player input should be locked (e.g., when a UI panel is open).
    /// Uses a lock count to allow multiple UI elements to request locks without conflicting.
    /// </summary>
    public static class InputLockManager
    {
        private static int lockCount = 0;

        /// <summary>
        /// Is player input currently locked?
        /// </summary>
        public static bool IsLocked => lockCount > 0;

        /// <summary>
        /// Request to lock player input. Should be paired with a call to Unlock().
        /// </summary>
        public static void Lock()
        {
            lockCount++;
            Debug.Log($"[InputLockManager] Input Locked. Active locks: {lockCount}");
            
            // Optional: Handle cursor visibility here if desired globally
            // Cursor.visible = true;
            // Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>
        /// Request to unlock player input. Should be called when the requesting UI closes.
        /// </summary>
        public static void Unlock()
        {
            if (lockCount > 0)
            {
                lockCount--;
                Debug.Log($"[InputLockManager] Input Unlocked. Active locks: {lockCount}");
                
                // Optional: Handle cursor visibility here if desired globally
                // if (!IsLocked)
                // {
                //     Cursor.visible = false;
                //     Cursor.lockState = CursorLockMode.Locked;
                // }
            }
            else
            {
                Debug.LogWarning("[InputLockManager] Attempted to unlock, but lock count was already 0.");
            }
        }

        /// <summary>
        /// Forcibly clear all locks (useful for scene changes or soft resets).
        /// </summary>
        public static void ResetLocks()
        {
            lockCount = 0;
            Debug.Log("[InputLockManager] All locks reset.");
        }
    }
}
