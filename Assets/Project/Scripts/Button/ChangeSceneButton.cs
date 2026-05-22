using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class ChangeSceneButton : MonoBehaviour
{
    [SerializeField] private string sceneName;

    public void ChangeScene()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string scenePath = null;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (System.IO.Path.GetFileNameWithoutExtension(scene.path) == sceneName)
                {
                    scenePath = scene.path;
                    break;
                }
            }

            if (scenePath != null)
                EditorSceneManager.OpenScene(scenePath);
            else
                Debug.LogWarning($"ChangeSceneButton: Scene '{sceneName}' not found in Build Settings.");
            return;
        }
#endif
        SceneManager.LoadScene(sceneName);
    }
}
