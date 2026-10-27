#if UNITY_INCLUDE_TESTS
using System.Reflection;
using UnityEditor;

public static class QteSwapValidation
{
    [MenuItem("Tools/Project/Combat/Validate QTE Team Swap")]
    public static void Run()
    {
        typeof(ShadowMeterValidation).GetMethod("RunFixtures", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { new[] { "QteTeamSwapTests", "ShadowMeterTests", "PlayerActionInterruptTests", "CameraGirlPotentialTests", "AbilityUIFeedbackTests" },
                "Temp/qte-team-swap-tests.xml", false });
    }
}
#endif
