---
slug: camera-girl-field-feedback
status: needs-human
source: manual
gdd_tags: [combat]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Bug: CameraGirl Attack and Crit Buffs Have No Field Visual

## Goal and Source

Owner reported on 2026-10-14 that CameraGirl's attack and crit buffs still lacked visual feedback after the shared field visual feature was implemented.

Workflow: `/fix-bug`. Reuse the previously approved golden power-up style and field-visual architecture; no new design or architecture decision.

GDD `@tag:combat`: Runtime Effect Recipes own effect presentation alongside application values/policy. The current CameraGirl CharacterObject and runtime trigger code are the inspected implementation truth.

## Root Cause and Scope

The previous integration assigned field visuals to four Artifact recipes only. CameraGirl's base skill random buff pool, attack recipe, and upgraded crit recipes retained empty `presentation.fieldVisualPrefab` slots.

Assign the existing `FieldPowerUp` prefab to all valid positive ATK/crit recipes in CameraGirl's kit, including random-pool entries. Preserve buff values, durations, targets, trigger/filter data, icons, and unrelated existing user edits.

## Acceptance

- [x] Both base random crit-buff outcomes have field visuals.
- [x] CameraGirl's attack buff and upgraded CR/CD recipes have field visuals.
- [x] Shared CR/CD buffs retain one loop until the final buff ends.
- [x] Reapplication keeps the attack buff's loop instead of duplicating it.
- [x] Snapshot comparison confirms exactly nine visual assignments and no other character-asset changes.
- [x] Unity compilation and all 65 focused EditMode checks pass, including four CameraGirl regressions.

## Files

- `Assets/Project/Character/Hero/test_1_CameraGirl/CharacterObject_cg.asset`
- `Assets/Project/Scripts/Editor/FieldEffectVisualAssetCreator.cs`
- `Assets/Project/Scripts/Editor/FieldEffectVisualPreviewWindow.cs`
- Focused CameraGirl field-visual regression tests.

Final in-game readability review remains covered by the existing field-effect test plan.

## Result

Fixed through recipe presentation assignments. The asset authoring utility now includes CameraGirl and provides a targeted `Wire CameraGirl Buff Visuals` menu. Existing custom field visual assignments are preserved. No runtime combat code was changed for this fix.
