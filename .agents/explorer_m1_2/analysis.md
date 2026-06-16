# Analysis Report - Aura Energy Gain Chance Refactor (Milestone 1)

## Executive Summary
This report analyzes the refactoring of the Aura Energy (AE) gain chance system inside `PlayerCombat.cs` (specifically in the `NotifyResolvedEnemyHit` method). It details how to restore the `AuraEnergyRate` check on every hit based on character stats (High=15%, Med=10%, Low=5%), ensure no chance routine runs when no hitbox data is active or `baseGainAmount <= 0f`, and coordinates with Explorer 1 and Explorer 3.

---

## 1. Codebase Investigation: `PlayerCombat.cs`

### 1.1 `NotifyResolvedEnemyHit` Structure
The current implementation of `NotifyResolvedEnemyHit` in `Assets/Project/Scripts/Combat/PlayerCombat.cs` (lines 772-815) handles energy gain on hit and on kill.

```csharp
    public void NotifyResolvedEnemyHit(
        CharacterObject character,
        float onHitAmount,
        float onKillAmount,
        bool isDefeated)
    {
        float baseGainAmount = isDefeated ? onKillAmount : onHitAmount;
        if (character == null || baseGainAmount <= 0f)
            return;

        if (teamLoader == null)
            teamLoader = Object.FindFirstObjectByType<TeamLoader>();

        CharacterStats stats = teamLoader != null
            ? teamLoader.GetCharacterRuntimeStats(character)
            : character.stats;

        float hitDensityMultiplier = 1f;
        if (!isDefeated)
        {
            if (UnityEngine.Random.value > 0.8f)
                return;

            if (!TryConsumeAuraEnergyGrantSlot())
                return;

            hitDensityMultiplier = ConsumeSameFrameHitAuraMultiplier();
        }

        float statMultiplier = stats != null
            ? Mathf.Max(0f, stats.auraEnergyMultiplier)
            : 1f;
        float energyToGain = Mathf.Max(0f, baseGainAmount)
            * statMultiplier
            * hitDensityMultiplier;

        if (energyToGain <= 0f)
            return;

        if (teamLoader == null)
            return;

        teamLoader.AddCharacterAE(character, energyToGain);
    }
```

### 1.2 The Chance Gate for Non-Kill Hits
Currently, the chance gate is hardcoded as:
```csharp
if (UnityEngine.Random.value > 0.8f)
    return;
```
This maps to a flat 20% hit chance. The requirement is to refactor this to use the character's `AuraEnergyRate` categories:
* **High**: 15% chance
* **Med**: 10% chance
* **Low**: 5% chance

---

## 2. Refactor Analysis

### 2.1 Re-integrating the `AuraEnergyRate` Enum & Stats
The `AuraEnergyRate` enum needs to be restored in `CharacterStats.cs` by Explorer 1:
```csharp
public enum AuraEnergyRate
{
    Low,
    Med,
    High
}
```
And `CharacterStats` will have the field:
```csharp
public AuraEnergyRate auraEnergyRate = AuraEnergyRate.Med;
```

### 2.2 Implementing the Chance Check in `NotifyResolvedEnemyHit`
We can implement a private static helper mapping `AuraEnergyRate` to the roll chance:
```csharp
    private static float ResolveAuraEnergyGainChance(AuraEnergyRate auraEnergyRate)
    {
        return auraEnergyRate switch
        {
            AuraEnergyRate.High => 0.15f,
            AuraEnergyRate.Low => 0.05f,
            _ => 0.10f
        };
    }
```

Inside `NotifyResolvedEnemyHit`, the hardcoded `0.8f` check will be replaced as follows:
```csharp
        if (!isDefeated)
        {
            AuraEnergyRate rate = stats != null ? stats.auraEnergyRate : AuraEnergyRate.Med;
            float chance = ResolveAuraEnergyGainChance(rate);

            if (UnityEngine.Random.value > chance)
                return;

            if (!TryConsumeAuraEnergyGrantSlot())
                return;

            hitDensityMultiplier = ConsumeSameFrameHitAuraMultiplier();
        }
```

### 2.3 Early Exit When No HitboxData/Energy Configured
The requirement is:
* "If no HitboxData is being sent out (or baseGainAmount <= 0f), there should be no AE chance routine."

In the proposed flow, the check:
```csharp
        float baseGainAmount = isDefeated ? onKillAmount : onHitAmount;
        if (character == null || baseGainAmount <= 0f)
            return;
```
runs at the very start of `NotifyResolvedEnemyHit`, which is before the `if (!isDefeated)` block containing the random roll.
Therefore, if `baseGainAmount <= 0f` (due to no hit amounts being configured, or missing hitbox data yielding 0 base gain), the routine exits immediately without executing the AE chance roll.

---

## 3. Proposed Code Patch for `PlayerCombat.cs`

Below is the proposed patch to apply to `Assets/Project/Scripts/Combat/PlayerCombat.cs`:

```patch
diff --git a/Assets/Project/Scripts/Combat/PlayerCombat.cs b/Assets/Project/Scripts/Combat/PlayerCombat.cs
index 515c0e1..77db8a9 100644
--- a/Assets/Project/Scripts/Combat/PlayerCombat.cs
+++ b/Assets/Project/Scripts/Combat/PlayerCombat.cs
@@ -790,7 +790,9 @@ public class PlayerCombat : MonoBehaviour, ICharacterCombat
         float hitDensityMultiplier = 1f;
         if (!isDefeated)
         {
-            if (UnityEngine.Random.value > 0.8f)
+            AuraEnergyRate rate = stats != null ? stats.auraEnergyRate : AuraEnergyRate.Med;
+            float chance = ResolveAuraEnergyGainChance(rate);
+            if (UnityEngine.Random.value > chance)
                 return;
 
             if (!TryConsumeAuraEnergyGrantSlot())
@@ -815,6 +817,16 @@ public class PlayerCombat : MonoBehaviour, ICharacterCombat
         teamLoader.AddCharacterAE(character, energyToGain);
     }
 
+    private static float ResolveAuraEnergyGainChance(AuraEnergyRate auraEnergyRate)
+    {
+        return auraEnergyRate switch
+        {
+            AuraEnergyRate.High => 0.15f,
+            AuraEnergyRate.Low => 0.05f,
+            _ => 0.10f
+        };
+    }
+
 
     private float ConsumeSameFrameHitAuraMultiplier()
     {
```

---

## 4. Coordination & Synthesis with Other Roles

### 4.1 Explorer 1 (CharacterStats.cs)
* **Task**: Re-integrate `AuraEnergyRate` enum and field into `CharacterStats.cs`.
* **Alignment**: The enum `AuraEnergyRate` must be publicly accessible so `PlayerCombat` can read it.
* **Fields**: `stats.auraEnergyRate` must be available on character stats.

### 4.2 Explorer 3 (Artifact.cs / Inspector)
* **Task**: Ensure `auraEnergyRate` is not visible or editable in the Artifact inspector and not merged in `AddNumericBonuses`.
* **Alignment**: Since `auraEnergyRate` is a non-numeric enum, it must be omitted from arithmetic merging in `AddNumericBonuses(CharacterStats bonus)` (which it naturally is if it's not a numeric field, but double check that `AddNumericBonuses` does not attempt to add it). The Artifact inspector should hide this field to satisfy R3.

---

## 5. Verification Plan
To verify the changes:
1. Ensure the project compiles clean (via Unity MCP `refresh_unity` and `read_console` or manual compiler check).
2. Set the player character's `auraEnergyRate` to `High` (15%), `Med` (10%), or `Low` (5%).
3. Verify that the on-hit event rolls against the correct chance (15%, 10%, or 5% respectively) by tracking AE gain logs.
4. Verify that when no hitbox data is active or `baseGainAmount <= 0f), the chance routine does not run.
