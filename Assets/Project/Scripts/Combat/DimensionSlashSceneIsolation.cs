#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("")]
[DefaultExecutionOrder(-32000)]
public sealed class DimensionSlashSceneIsolation : MonoBehaviour
{
    private void Awake()
    {
        // Play's scene reload can restore saved active flags. Quarantine retained
        // user scenes before their persistent managers run Awake.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene == gameObject.scene) continue;
            foreach (GameObject root in scene.GetRootGameObjects()) root.SetActive(false);
        }
    }
}
#endif
