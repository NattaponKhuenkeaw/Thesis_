#if UNITY_INCLUDE_TESTS
using System.Reflection;
using UnityEditor;

public static class DimensionSlashValidation
{
    [MenuItem("Tools/Project/Combat/Validate Main Meter QTE Max Cancel")]
    public static void RunFocused()
    {
        typeof(ShadowMeterValidation).GetMethod("RunFixtures", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { new[] { "MainMeterMaxCancelTests", "ActionVfxLifetimeTests", "ShadowMeterTests", "QteTeamSwapTests",
                "MainDimensionSlashTests.FifthPaidManualRootArmsOneWindowAndResetsCount",
                "MainDimensionSlashTests.RealFsmSpendsFiveCachedCutsThenSkillConsumesOneOpportunity",
                "MainDimensionSlashTests.ExistingCooldownDoesNotBlockOrChangeReadyFollowUp",
                "MainDimensionSlashTests.ExtraCutsCannotBankOrRefreshAndStartConsumesExactlyOnce",
                "MainDimensionSlashTests.JudgmentCutUsesUltimatePolicyWhileRemainingOnSkillInput" },
                "Temp/main-meter-qte-max-cancel-tests.xml", false });
    }

    [MenuItem("Tools/Project/Combat/Validate Dimension Slash")]
    public static void Run()
    {
        typeof(ShadowMeterValidation).GetMethod("RunFixtures", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { new[] { "MainDimensionSlashTests", "MainMeterMaxCancelTests", "ShadowMeterTests", "CombatDamageTypeTests", "PlayerActionInterruptTests", "AbilityUIFeedbackTests", "QteTeamSwapTests" },
                "Temp/main-dimension-slash-tests.xml", false });
    }
}
#endif
