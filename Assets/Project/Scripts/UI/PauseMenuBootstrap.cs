using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.UI
{
    /// <summary>
    /// Spawns the authored pause Canvas in play scenes and removes it on the title screen.
    /// </summary>
    public static class PauseMenuBootstrap
    {
        private const string ResourcePath = "UI/PauseMenu";
        private const string MainMenuSceneName = "MainMenu";
        private static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            subscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!subscribed)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                subscribed = true;
            }

            EnsureForScene(SceneManager.GetActiveScene().name);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene.name);
        }

        private static void EnsureForScene(string sceneName)
        {
            if (string.Equals(sceneName, MainMenuSceneName, StringComparison.Ordinal))
            {
                if (PauseMenuController.Instance != null)
                {
                    UnityEngine.Object.Destroy(PauseMenuController.Instance.gameObject);
                }

                return;
            }

            if (PauseMenuController.Instance != null)
            {
                return;
            }

            PauseMenuController prefab = Resources.Load<PauseMenuController>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[PauseMenu] Missing Resources/UI/PauseMenu prefab.");
                return;
            }

            UnityEngine.Object.Instantiate(prefab);
        }
    }
}
