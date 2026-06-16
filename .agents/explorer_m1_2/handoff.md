# Handoff Report - Explorer 2

## 1. Observation
- **File investigated**: `Assets/Project/Scripts/Combat/PlayerCombat.cs`
  - In `NotifyResolvedEnemyHit` (lines 772-815):
    - Base gain amount selection:
      ```csharp
      float baseGainAmount = isDefeated ? onKillAmount : onHitAmount;
      if (character == null || baseGainAmount <= 0f)
          return;
      ```
    - Check on non-kill hit gate:
      ```csharp
      float hitDensityMultiplier = 1f;
      if (!isDefeated)
      {
          if (UnityEngine.Random.value > 0.8f)
              return;

          if (!TryConsumeAuraEnergyGrantSlot())
              return;

          hitDensityMultiplier = ConsumeSameFrameHitAuraMultiplier();
      }
      ```
- **Historical code from `git show c7d83d2`**:
  - `ResolveAuraEnergyGainChance` was previously defined in `PlayerCombat.cs` as:
    ```csharp
    private static float ResolveAuraEnergyGainChance(AuraEnergyRate auraEnergyRate)
    {
        return auraEnergyRate switch
        {
            AuraEnergyRate.Low => 0.05f,
            AuraEnergyRate.High => 0.15f,
            _ => 0.10f
        };
    }
    ```
- **Asset values from `Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset`**:
  - The `auraEnergyRate: 1` field is still serialized in character asset files under the `stats` key.

---

## 2. Logic Chain
1. `HitBoxDamage.ProcessHit` resolves damage on target hit. If damage is dealt, it delegates to `PlayerCombat.NotifyResolvedEnemyHit` passing the `onHitAmount`, `onKillAmount`, and `isDefeated` flag (Observation 1).
2. Currently, the hit chance gate on the non-kill hit (`!isDefeated`) branch is a hardcoded `UnityEngine.Random.value > 0.8f` (20% flat chance) (Observation 1).
3. The requirements specify returning the `AuraEnergyRate` categories: High (15%), Med (10%), Low (5%).
4. These categories can be resolved by re-implementing `ResolveAuraEnergyGainChance(AuraEnergyRate)` and updating the random check in `NotifyResolvedEnemyHit` to look up the rate from character stats (Observation 2 & 3).
5. The requirements also state that if no `HitboxData` is being sent out or `baseGainAmount <= 0f`, the chance routine should not execute. Since the first check in `NotifyResolvedEnemyHit` is `if (character == null || baseGainAmount <= 0f) return;` (Observation 1), this early return runs before the random chance routine, satisfying the constraint.

---

## 3. Caveats
- This investigation assumes that Explorer 1 successfully restores the `AuraEnergyRate` enum and the `auraEnergyRate` field in `CharacterStats.cs`. If the field is not available or named differently, it will cause compilation errors.
- Since we are read-only, no code files were modified.

---

## 4. Conclusion
The refactor of the Aura Energy gain chance system in `PlayerCombat.cs` is straightforward and can be achieved by:
1. Re-adding the `ResolveAuraEnergyGainChance` helper method mapping the rates (High to 0.15f, Med to 0.10f, Low to 0.05f).
2. Retrieving the character's `stats.auraEnergyRate` (with a default fallback to `AuraEnergyRate.Med` if `stats` is null) inside `NotifyResolvedEnemyHit`.
3. Substituting the hardcoded `0.8f` comparison with the retrieved chance value inside the `!isDefeated` block.

---

## 5. Verification Method
- **Compilation Check**: After implementing, use the Unity MCP tools `refresh_unity` and `read_console` to ensure the project compiles with no compiler errors.
- **Log Inspection**: Set `showCombatLogs` to true in `PlayerCharacter` and print logs inside the `!isDefeated` block when the roll succeeds or fails. Verify the frequency matches the rate configured:
  - High should yield energy ~15% of hits.
  - Med should yield energy ~10% of hits.
  - Low should yield energy ~5% of hits.
- **Zero Value Check**: Verify that if a move is configured with 0 `auraEnergyGainAmount`, the method exits early and does not log/trigger any random rolls.
