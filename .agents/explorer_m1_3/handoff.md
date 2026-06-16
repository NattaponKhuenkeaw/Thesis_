# Handoff Report — Explorer 3

## 1. Observation

Direct observations from codebase inspection:
1. **`CharacterStats.cs`**:
   * Lines 30–33:
     ```csharp
     [Header("Aura Energy")]
     [Tooltip("Multiplier applied to all gained aura energy.")]
     public float auraEnergyMultiplier = 1f; // Multiplier for gained AE
     ```
   * Lines 53–72:
     ```csharp
     public void AddNumericBonuses(CharacterStats bonus)
     {
         if (bonus == null)
         {
             return;
         }

         maxHP += bonus.maxHP;
         attack += bonus.attack;
         defense += bonus.defense;
         speed += bonus.speed;
         elementalMultiplier += bonus.elementalMultiplier;
         critRate += bonus.critRate;
         critDamage += bonus.critDamage;
         cooldownReduction += bonus.cooldownReduction;
         skillMastery += bonus.skillMastery;
         damageMastery += bonus.damageMastery;
         damageResistance += bonus.damageResistance;
         auraEnergyMultiplier += bonus.auraEnergyMultiplier;
     }
     ```
2. **`Artifact.cs`**:
   * Line 16:
     ```csharp
     public CharacterStats bonusStats;
     ```
3. **`PlayerCombat.cs`**:
   * Lines 772–799 (`NotifyResolvedEnemyHit`):
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
             ...
     ```
4. **`TeamLoader.cs`**:
   * Line 631 (`BuildRuntimeStats`):
     ```csharp
                 stats.AddNumericBonuses(artifact.bonusStats);
     ```
5. **`ArtifactPreviewPanel.cs`**:
   * Lines 102–111 explicitly draw the following properties: `maxHP`, `attack`, `defense`, `speed`, `critRate`, `critDamage`, `cooldownReduction`, `skillMastery`, `damageMastery`, and `damageResistance`. It does not use reflection to query stats.
6. **Asset Serialization Files (YAML)**:
   * Grep search matches like `auraEnergyRate: 1` in `a_1_Shattered Crown.asset` (line 36) confirm that the field was previously serialized under `CharacterStats`.

---

## 2. Logic Chain

1. **Reintroducing `AuraEnergyRate` (R1)**:
   * Re-declaring the `AuraEnergyRate` enum (with `High`, `Med`, `Low` values) and adding `public AuraEnergyRate auraEnergyRate = AuraEnergyRate.Med;` inside `CharacterStats.cs` allows each character to have a base energy rate category.
   * `Clone()` must copy this enum.
   * `PlayerCombat.NotifyResolvedEnemyHit()` must fetch the current character's runtime stats via `teamLoader` and evaluate `stats.auraEnergyRate` (High: 15%, Med: 10%, Low: 5%) to gate AE gain on hit, replacing the static 20% flat gate (`UnityEngine.Random.value > 0.8f`).
2. **Preventing Artifact Inspector Visibility/Editability (R3)**:
   * Because `Artifact.bonusStats` is a `CharacterStats` field, Unity's default inspector exposes all serialized fields of `CharacterStats` inside it.
   * Therefore, we must intercept the inspector drawing. This can be achieved by:
     * *Method A (Custom Property Drawer)*: Check if `property.serializedObject.targetObject is Artifact` in `CharacterStatsDrawer` and skip rendering `auraEnergyRate`.
     * *Method B (Custom Editor)*: Overwrite `ArtifactEditor` to manually draw the fields of `CharacterStats` without `auraEnergyRate`.
   * Both methods successfully hide the property ONLY when the parent object is an `Artifact` asset, ensuring it remains fully visible on `CharacterObject` profiles.
3. **Preventing Stat Merging**:
   * At runtime, artifact bonuses are aggregated into the character stats via `stats.AddNumericBonuses(artifact.bonusStats)` in `TeamLoader.cs`.
   * Since `AddNumericBonuses` in `CharacterStats.cs` explicitly lists target fields instead of iterating via reflection, and since `AuraEnergyRate` is a non-numeric enum (which cannot compile with the `+=` operator anyway), simply omitting `auraEnergyRate` from `AddNumericBonuses()` prevents any merging of this field.

---

## 3. Caveats

* Assumes that the existing serialized `auraEnergyRate: X` entries in `.asset` files are deprecated and will be updated/reserialized when character/artifact configurations are saved, which Unity does automatically when assets are touched in editor.
* Investigation is read-only; no code files were modified except documentation/analysis reports.

---

## 4. Conclusion

1. **For R1**: Re-add the `AuraEnergyRate` enum to the codebase, map it to `CharacterStats.cs` (including `Clone()`), and implement the corresponding chance thresholds (`High` = 0.15f, `Med` = 0.10f, `Low` = 0.05f) in `PlayerCombat.NotifyResolvedEnemyHit()`.
2. **For R3 (Inspector Visibility)**: Implement a custom property drawer for `CharacterStats` or a custom inspector for `Artifact` that filters out `auraEnergyRate` when the target object is an `Artifact`.
3. **For R3 (Merging)**: Ensure that `AddNumericBonuses` does not mention `auraEnergyRate`, which is already the case and enforces complete separation during stat summation.

---

## 5. Verification Method

1. **Code Compilation**: Verify the project compiles without syntax or type errors after code is modified.
2. **Inspector Check**:
   * Select a `CharacterObject` asset (e.g. `CharacterObject_Kyo.asset`) and confirm "Aura Energy Rate" is visible and editable.
   * Select an `Artifact` asset (e.g. `a_1_Shattered Crown.asset`) and verify that "Aura Energy Rate" is hidden from its stats.
3. **Log / Assert Verification**: Place a debug statement in `PlayerCombat.NotifyResolvedEnemyHit` to output the resolved energy chance and verify it rolls against the character's base energy rate.
