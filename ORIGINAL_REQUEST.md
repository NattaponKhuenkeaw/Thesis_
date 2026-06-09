# Original User Request

## Initial Request — 2026-06-06T21:00:04+07:00

Fix 5 post-refactor logic bugs in the Neo-JJK Unity combat system. All bugs stem from the previous two devlog sessions (Aura Energy refactor and Artifact build system). The working directory is `C:\Users\Apricha\Documents\GitHub\Thesis_`.

Working directory: `C:\Users\Apricha\Documents\GitHub\Thesis_`
Integrity mode: development

---

## Context

This is a Unity 2D action game project (Neo-JJK). Recent refactors introduced:
1. **Aura Energy (AE) system** – AE replaces cooldown timers for Ultimate abilities.
2. **Artifact build system** – Artifacts equip onto characters and modify `CharacterStats` at runtime.

Five concrete bugs were identified:

---

## Requirements

### R1 — Fix AE Gain Chance on Non-Kill Hits

**File:** `Assets/Project/Scripts/Combat/PlayerCombat.cs`, method `NotifyResolvedEnemyHit`

**Bug:** The `auraEnergyRate` field (`Low/Med/High`) and the random-chance gate (`ResolveAuraEnergyGainChance`) are applied for the `!isDefeated` (non-kill) branch. This was supposed to be a flat 20% hit chance per the devlog spec, not three tier values (5–15%). The `AuraEnergyRate` enum and `auraEnergyRate` stat are vestigial and incorrect.

**Fix Required:**
- Remove `AuraEnergyRate` enum definition from `CharacterStats.cs`
- Remove `auraEnergyRate` field from `CharacterStats` class body
- Remove `auraEnergyRate` from `CharacterStats.Clone()` return initializer
- Remove `auraEnergyRate` from `AddNumericBonuses()` (it is a non-numeric enum, not merged)
- In `PlayerCombat.NotifyResolvedEnemyHit`, replace the `ResolveAuraEnergyGainChance(auraEnergyRate)` call with a plain `0.8f` threshold (i.e. `if (UnityEngine.Random.value > 0.8f) return;` for 20% pass-through)
- Remove the `auraEnergyRate` local variable and the `AuraEnergyRate` lookup from `NotifyResolvedEnemyHit`
- Delete the `ResolveAuraEnergyGainChance` private method entirely
- Keep `auraEnergyMultiplier` field intact — it is still used
- Keep on-kill AE as 100% (no random gate when `isDefeated == true`)

---

### R2 — Fix Projectile Hit Count (Infinite hits with no Artifact)

**Files:** `Assets/Project/Scripts/Combat/HitBoxDamage.cs`, `Assets/Project/Scripts/Combat/ProjectileHitboxModule.cs`

**Bug:** `ConfigurePerEnemyDamageCalculation` is always called on projectile activation (via `ProjectileHitboxModule.ApplyNormalHitData` / `ApplyGrabHitData`), setting `usePerEnemyDamageCalculation = true` regardless of whether `attackerStats` is null. When `attackerStats == null` (character has no Artifact and stats are null), `CombatFormula.CalculateResolvedDamage` returns 0, so `primaryDamageDealt == 0`, so `dealtDamage == false`, so `projectileOwner?.RegisterSuccessfulHit(enemy)` is NEVER called. This means `remainingHitsByEnemy` (or `spawnContext.remainingHits`) is never decremented → projectile hits infinitely until lifetime expires.

**Fix:**
In `HitBoxDamage.ConfigurePerEnemyDamageCalculation`, set `usePerEnemyDamageCalculation = true` **only** when `attackerStats != null`:
```csharp
public void ConfigurePerEnemyDamageCalculation(
    CharacterStats attackerStats,
    float sourceAttack,
    bool isSkillOrUltimate,
    AdditionalDamageEffectDefinition[] additionalDamageEffects,
    PlayerCombat sourceCombat,
    CharacterObject sourceCharacter,
    float onHitAuraEnergy,
    float onKillAuraEnergy)
{
    // Only enable per-enemy formula if we have stats to calculate with
    usePerEnemyDamageCalculation = attackerStats != null;
    currentAttackerStats = attackerStats;
    // ... rest unchanged
}
```

Also fix the **HitData debug display** bug: In `HitBoxDamage.ProcessHit`, `TrainingDebugStats.Instance.RecordHit` is currently inside the `if (dealtDamage)` block. Move the `RecordHit` call to always fire when `primaryDamageDealt >= 0f` and the hit was processed (not just when `> 0`), so the debug panel shows data even on zero-damage hits.

---

### R3 — Remove `auraEnergyRate` from Artifact `bonusStats`

This is resolved by R1 — once `auraEnergyRate` is removed from `CharacterStats`, it is automatically removed from `Artifact.bonusStats` since `bonusStats` is of type `CharacterStats`. No separate file change needed beyond what R1 requires, but verify that `AddNumericBonuses` does not attempt to merge any removed field.

---

### R4 — Artifact Data Not Syncing to TeamUI on Reopen

**File:** `Assets/Project/Scripts/TeamBuilder/UI/TeamBuilderUI.cs`, method `LoadRuntimeDraft` (approximately line 648–684)

**Bug:** `LoadRuntimeDraft` calls `teamSlots[i].SetCharacter(matchingItem)` but never restores artifact data from `teamLoader.equippedArtifacts`. So when the TeamUI is reopened, all artifact slots appear empty.

**Fix:** After `teamSlots[i].SetCharacter(matchingItem);`, add:
```csharp
if (teamLoader != null)
{
    Artifact[] runtimeArtifacts = teamLoader.GetEquippedArtifactsFor(charObj);
    if (runtimeArtifacts != null)
        teamSlots[i].SetEquippedArtifacts((Artifact[])runtimeArtifacts.Clone());
}
```

---

### R5 — Remove Audio Section from `CharacterObject`

**File:** `Assets/Project/Scripts/Character/CharacterObject.cs`

**Task:** Remove the `[Header("Audio")]` and `public PlayerAudioData audioData;` lines. Before removing, search the entire codebase for any references to `characterObject.audioData` or `.audioData` and verify they are absent or update them.

---

## Acceptance Criteria

### R1 — AE Gain Chance
- [ ] `AuraEnergyRate` enum no longer defined in `CharacterStats.cs`
- [ ] `auraEnergyRate` field removed from `CharacterStats` class, `Clone()`, `AddNumericBonuses()`
- [ ] `NotifyResolvedEnemyHit` uses `Random.value > 0.8f` (20% flat chance) for non-kill hit gate
- [ ] `ResolveAuraEnergyGainChance` method deleted from `PlayerCombat.cs`
- [ ] On-kill AE path has no random gate
- [ ] `auraEnergyMultiplier` still present and functional
- [ ] Project compiles (verify via Unity MCP console refresh)

### R2 — Projectile Hit Count
- [ ] `usePerEnemyDamageCalculation = attackerStats != null` in `ConfigurePerEnemyDamageCalculation`
- [ ] `RecordHit` is called outside the `if (dealtDamage)` block so debug shows data
- [ ] Project compiles

### R3 — Artifact bonusStats auraEnergyRate
- [ ] `CharacterStats` (used as `Artifact.bonusStats`) no longer has `auraEnergyRate` field
- [ ] `AddNumericBonuses` compiles without referencing the removed field

### R4 — TeamUI Artifact Sync
- [ ] `LoadRuntimeDraft` calls `SetEquippedArtifacts` with data from `teamLoader.GetEquippedArtifactsFor(charObj)` for each slot
- [ ] Project compiles

### R5 — CharacterObject Cleanup
- [ ] `CharacterObject.cs` no longer has `[Header("Audio")]` or `public PlayerAudioData audioData`
- [ ] No compile errors from missing `audioData` references
- [ ] Project compiles

## Final Verification

After all changes:
1. Use the Unity MCP tools (`refresh_unity`, `read_console`) to trigger a domain reload and verify zero compiler errors
2. If there are compile errors, fix them before reporting done
3. Report a summary of every file changed and what was changed

## Follow-up — 2026-06-06T21:32:16+07:00

Refactor Aura Energy rates/chances, fix projectile infinite hits, remove Aura Energy rate bonus in Artifact, sync equipped Artifacts with TeamUI, and clean up audio sections in CharacterObject in the Unity project.

Working directory: c:\Users\Apricha\Documents\GitHub\Thesis_
Integrity mode: development

## Requirements

### R1. Aura Energy Gain of Chance Refactor
Refactor the Aura Energy gain chance system:
- Return the AuraEnergyRate categories: High (15%), Med (10%), Low (5%).
- Ensure the chance is checked on every single hit rather than only on enemy kill.
- If no HitboxData is being sent out (or baseGainAmount <= 0f), there should be no AE chance routine.

### R2. Projectile Hit Count and HitData Fix
Fix projectile hit count logic:
- Projectiles currently hit infinitely until expiration when a character has no Artifact equipped; restore respect for Hit Count by registering hits unconditionally.
- Fix HitData in Debug mode which is currently not showing the hit details or data even though in-game enemies receive damage.

### R3. Remove Aura Energy Rate from Artifact Field
Remove the Aura Energy Rate of CharacterStats bonus in the Artifact field so it is not visible or editable in the Artifact inspector.

### R4. Sync Artifact Data with TeamUI
Ensure that Artifacts equipped on `CharacterRuntime` are correctly synced with the TeamUI, so that reopening the TeamUI does not lose or reset the character's Artifact configuration.

### R5. Clean Up CharacterObject and Audio
Restore the `audioData` field on `CharacterObject` and clean up `CharacterMetaData` by removing old unused fields `attackSfx`, `skillSfx`, and `hurtSfx`.

## Acceptance Criteria

### AE Chance and hit box
- [ ] AE chance routine rolls on every hit based on AuraEnergyRate: High (15%), Med (10%), Low (5%).
- [ ] AE chance routine does not run when no hitbox data is active (e.g. no energy amounts configured).

### Projectile and Debug Data
- [ ] Projectiles respect hit counts and do not hit infinitely when no Artifact is equipped.
- [ ] HitData is populated and displayed correctly in Debug mode during combat.

### Artifact Field and UI Sync
- [ ] Aura Energy Rate is removed from Artifact stats.
- [ ] Equipped Artifacts persist and sync correctly when reopening the TeamUI.

### CharacterObject Cleanup
- [ ] Audio section is restored on CharacterObject (`audioData` field) and references are updated to use it.
- [ ] Old SFX fields are removed from CharacterMetaData.
