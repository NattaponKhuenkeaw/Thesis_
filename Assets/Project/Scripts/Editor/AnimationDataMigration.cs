using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// One-time migration utility: converts legacy AttackHitFrameData[]
/// on every AttackStepData into the new HitFrameData + NormalHitbox system.
/// Run via  Window > Animation Editor > Migrate Data.
/// </summary>
public static class AnimationDataMigration
{
    [MenuItem("Window/Animation Editor/Migrate Legacy Hit Data")]
    public static void MigrateAllCharacters()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterObject");
        int totalMigrated = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CharacterObject character = AssetDatabase.LoadAssetAtPath<CharacterObject>(path);
            if (character == null) continue;

            bool dirty = false;

            // Ground combo
            if (character.basicAttackComboData?.steps != null)
                dirty |= MigrateSteps(character.basicAttackComboData.steps);

            // Air combo
            if (character.neutralAirComboData?.steps != null)
                dirty |= MigrateSteps(character.neutralAirComboData.steps);

            // Plunge
            if (character.plungeAttackData?.impactStep != null)
                dirty |= MigrateStep(character.plungeAttackData.impactStep);

            if (dirty)
            {
                EditorUtility.SetDirty(character);
                totalMigrated++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AnimationDataMigration] Migrated {totalMigrated} CharacterObject asset(s).");
    }

    private static bool MigrateSteps(AttackStepData[] steps)
    {
        bool any = false;
        foreach (var step in steps)
            any |= MigrateStep(step);
        return any;
    }

    private static bool MigrateStep(AttackStepData step)
    {
        if (step == null) return false;

        // Skip if already migrated
        if (step.hitData != null) return false;

        // Skip if no legacy data
        if (step.hitFrames == null || step.hitFrames.Length == 0) return false;

        // Create new HitFrameData
        var hitData = new HitFrameData();
        hitData.hitboxes = new List<HitboxBase>();

        foreach (var legacy in step.hitFrames)
        {
            var normal = new NormalHitbox
            {
                startFrameOffset    = legacy.startFrameOffset,
                activeFrames        = legacy.activeFrames,
                debugColor          = legacy.debugColor,
                offset              = legacy.offset,
                size                = legacy.size,
                damageMultiplier    = legacy.damageMultiplier,
                impactTier          = legacy.impactTier,
                useDeterministicReaction = true,
                reactionKind        = HitReactionData.InferLegacyReactionKind(legacy.reactionVelocity, legacy.isKnockdown),
                impactFreezeFrames  = legacy.hitstunFrames,
                reactionFrames      = legacy.knockbackFrames,
                recoveryFrames      = 9,
                horizontalSpeed     = Mathf.Abs(legacy.reactionVelocity.x),
                launchSpeed         = Mathf.Max(0f, legacy.reactionVelocity.y),
                landingBehavior     = HitReactionData.InferLandingBehavior(legacy.reactionVelocity, legacy.isKnockdown),
                canAirJuggle        = legacy.isKnockdown || legacy.reactionVelocity.y > 0.01f,
                maxJuggleCount      = legacy.isKnockdown || legacy.reactionVelocity.y > 0.01f ? 2 : 0,
                juggleLaunchScale   = 0.85f,
                juggleStunScale     = 0.9f,
                hitstunFrames       = legacy.hitstunFrames,
                reactionVelocity    = legacy.reactionVelocity,
                knockbackFrames     = legacy.knockbackFrames,
                isKnockdown         = legacy.isKnockdown,
                snapToAttacker      = legacy.snapToAttacker,
                snapOffset          = legacy.snapOffset,
                flashColor          = legacy.flashColor,
                flashFrames         = legacy.flashFrames
            };

            hitData.hitboxes.Add(normal);
        }

        step.hitData = hitData;
        Debug.Log($"  Migrated step '{step.stepName}': {step.hitFrames.Length} hitframe(s) → NormalHitbox.");
        return true;
    }
}
