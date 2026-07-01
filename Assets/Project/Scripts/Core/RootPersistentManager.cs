using UnityEngine;

namespace Project.Core
{
    /// <summary>
    /// Put this on the root "Manager" GameObject in your scenes.
    /// It ensures the entire manager group persists across scene loads,
    /// while destroying duplicate manager hierarchies that load in target scenes.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class RootPersistentManager : MonoBehaviour
    {
        public static RootPersistentManager Instance { get; private set; }
        public bool IsDuplicate { get; private set; }

        private void Awake()
        {
            string sourceSceneName = gameObject.scene.name;

            if (Instance != null && Instance != this)
            {
                IsDuplicate = true;
                Debug.Log($"[RootPersistentManager] Duplicate manager root '{gameObject.name}' loaded from scene '{sourceSceneName}'. Destroying this hierarchy; persistent root is '{Instance.gameObject.name}'.");
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            // Ensure this is a root object so DontDestroyOnLoad works.
            transform.SetParent(null);
            IsDuplicate = false;
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[RootPersistentManager] Persistent manager root initialized from scene '{sourceSceneName}'.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
