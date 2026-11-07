#if UNITY_EDITOR
using UnityEditor;
[InitializeOnLoad]
public static class JudgementCutProfilingBootstrap
{
    static JudgementCutProfilingBootstrap()
    {
        EditorApplication.delayCall += () => MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost.Start();
    }
}
#endif
