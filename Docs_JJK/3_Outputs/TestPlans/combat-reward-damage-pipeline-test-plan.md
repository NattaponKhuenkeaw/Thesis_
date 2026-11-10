---
slug: "combat-reward-damage-pipeline"
status: implemented
source: manual
gdd_tags:
  - "combat"
owner: "qa-agent"
human_checkpoint: required
next_agent: "human"
blocked_by: []
---

# Test Plan: Combat Reward and Damage Pipeline

## Automated / Code-Level

- [ ] Normal damage uses Damage Mastery and can crit.
- [ ] Elemental damage uses Elemental Multiplier, ignores Damage Mastery, and
      can crit.
- [ ] Additional Damage cannot crit and resolves as a separate damage event.
- [ ] Explicit action budgets clamp hit ratios to `0..1`.
- [ ] Legacy actions preserve direct per-hit multiplier behavior.
- [ ] Effective runtime stats equal base stats plus artifacts from the same
      TeamLoader slot.

## Play Mode

- [ ] Missing an attack grants no AE.
- [ ] A surviving target can grant only on-hit AE.
- [ ] A defeated target grants only on-kill AE.
- [ ] A primary hit plus Additional Damage produces two damage popups.
- [ ] Additional Damage killing the target selects the on-kill AE branch.
- [ ] Genocide Cutter's three equal hits total 300% ATK before modifiers.
- [ ] Projectiles use the action reward and damage context that spawned them.
- [ ] Keep an enemy inside a projectile with an Additional Damage hit effect.
      Confirm the damage follows the authored hit interval and hit limit and
      Unity reports no pool disposal exception.
- [ ] Repeat with Additional Damage from a source character's on-hit recipe.
      For a recipe that requires a pre-existing target mark, confirm the hit
      that applies the mark does not trigger the mark-gated bonus. Confirm a
      subsequent eligible hit does.
- [ ] A swapped-out background Ultimate preserves the casting character's
      damage stats and reward ownership.
- [ ] Equipping an ATK artifact changes the matching slot's runtime damage.
- [ ] Equipping HP, DEF, SPD, CRIT, CDR, mastery, resistance, elemental, or AE
      multiplier bonuses affects the matching runtime character.
- [ ] A 50 AE Ultimate has 50 maximum AE, not 100.

## Execution Record

- Unity asset refresh and script compilation completed with no compiler errors.
- Standard Unity script validation passed for the combat formula, hit
  confirmation, player combat, runtime team stats, and Additional Damage
  definition.
- EditMode test execution completed successfully, but the project currently
  discovers zero EditMode tests.
- A short `Test_Playground` Play Mode smoke run entered and exited normally.
  It reported pre-existing `DontDestroyOnLoad` hierarchy errors in
  `GameAudioManager` and `GameFreezeManager`; neither stack trace enters the
  changed combat or artifact pipeline.
- The scenario checklist remains the human gameplay verification checkpoint.
