using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ExitGameButton : MonoBehaviour
{
    public void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
