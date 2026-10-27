#if UNITY_INCLUDE_TESTS
using System.Reflection;
using UnityEditor;

public static class DimensionSlashValidation
{
    [MenuItem("Tools/Project/Combat/Validate Dimension Slash")]
    public static void Run()
    {
        typeof(ShadowMeterValidation).GetMethod("RunFixtures", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { new[] { "MainDimensionSlashTests", "ShadowMeterTests", "CombatDamageTypeTests", "PlayerActionInterruptTests", "AbilityUIFeedbackTests", "QteTeamSwapTests" },
                "Temp/main-dimension-slash-tests.xml", false });
    }
}
#endif
