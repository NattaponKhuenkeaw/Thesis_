using CombatCore;
using UnityEngine;

/// <summary>
/// Forces runtime timing to match the combat system's fixed-step assumptions.
/// This runs automatically in both Editor Play Mode and standalone builds,
/// so we do not depend on scene setup or inspector wiring.
/// </summary>
public static class RuntimeTimingBootstrap
{
    private const int MaxFixedStepsPerFrame = 4;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureRuntimeTiming()
    {
        float logicFps = Mathf.Max(1f, FrameDataConstants.LOGIC_FPS);
        float fixedStep = 1f / logicFps;

        // Keep the render loop and the fixed-step combat loop aligned.
        Application.targetFrameRate = Mathf.RoundToInt(logicFps);
        QualitySettings.vSyncCount = 0;

        // Match physics/update cadence to combat logic expectations.
        Time.timeScale = 1f;
        Time.fixedDeltaTime = fixedStep;
        Time.maximumDeltaTime = fixedStep * MaxFixedStepsPerFrame;
    }
}
