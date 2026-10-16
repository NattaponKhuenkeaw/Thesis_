---
slug: field-effect-visual-feedback
status: needs-human
source: manual
gdd_tags: [combat, items]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Field Effect Visual Feedback

## Player-Facing Goal

Recognize active power buffs, power debuffs, and Toxic on players and enemies without obscuring combat. Every visual has enter, sustained loop, and ending animation. The four requested Artifacts drive those visuals through their existing effect sequences.

## Source

- Requested by: project owner, chat on 2026-10-14.
- Reference: attached Genshin screenshot showing small upward amber chevrons around a character. Owner subsequently authorized closer Genshin-style glow/trails and dark flames using Unity particles on 2026-10-14. Keep combat readable and visual density modest.
- Workflow: `/implement-feature`, using the relocated docs system in `Docs_JJK/`.

## GDD Reference

- `@tag:combat`: “A character kit, Potential node, projectile, or Artifact authors the trigger, target, duration, values, stacking rule, removal policy, and presentation through a **Runtime Effect Recipe**.”
- `@tag:items`: Phase-1 identities map to the approved Phase-1 design; speculative concept passives do not override the implemented assets.

## Type

- [x] Feature

## Scope

- Three reusable procedural 2D field visual styles usable on either player or enemy targets: golden upward chevrons, blue-violet downward chevrons, and dark flame particles.
- Connect existing `P1-A01-TOXIC`, `P1-A02-ATK`, `P1-A05-AFFLICTION`, and `P1-A07-SUPPORT` recipes to their corresponding styles.
- Preserve gameplay timing, targets, stacking, damage, and swap semantics.
- Validate refresh, removal, pause, swap, death, and teardown behavior.

### Inspect First

- `Assets/Project/Scripts/Combat/RuntimeEffectRecipe.cs`
- `Assets/Project/Scripts/Combat/RuntimeEffectReceiver.cs`
- `Assets/Project/Scripts/Combat/ArtifactPassiveFeedbackEvents.cs`
- `Assets/Project/Scripts/Combat/ArtifactPassiveRuntimeSystem.cs`
- `Assets/Project/Scripts/Editor/ArtifactPhase1AssetCreator.cs`
- Four corresponding Phase1 PassiveSet assets.
- ADR-002, ADR-004, ADR-010 and approved artifact-passive-phase-1 specs as needed.

### Out of Scope

Gameplay rebalance, new artifact mechanics, character animation edits, build/dependency configuration, and team publishing.

## Acceptance Criteria

- [x] All three styles implement enter, loop, and ending phases; Unity-rendered captures inspected.
- [x] Shared styles support player/enemy targets, with body bounds preferred for sprites containing transparent margins.
- [x] Four Artifact sequences tested against actual saved content and recipient rules.
- [x] Refresh and stack increases retain a bounded shared loop.
- [x] Visual duration follows authoritative effect state, including indefinite Affliction.
- [x] Inventory-driven visuals and character swaps tested without a HUD; off-field effects retained.
- [x] Normal expiry/removal starts ending; disable cleanup and reuse tested; death/teardown handlers implemented.
- [x] Existing icons retained for the three stat recipes; Toxic icon authored. Damage regressions pass.
- [x] Unity compilation succeeds; all 61 focused EditMode checks pass.
- [ ] Human reviews visual readability in combat.

## Human Checkpoints

- [x] Visual direction authorized by owner's 2026-10-14 revision; final in-game feel review remains pending.
- [x] Architecture plan approved after design approval — owner “LGTM”, 2026-10-14.
- [ ] Final in-game visual review.

## Router Decision

Workflow: `/implement-feature`.
Current artifact: `field-effect-visual-feedback-arch-plan.md`.
Next: final in-game visual review. Implementation and automated validation are complete.

## Results

- Report: `Docs_JJK/3_Outputs/Reports/FieldEffectVisuals/README.md`.
- Test plan: `Docs_JJK/3_Outputs/TestPlans/field-effect-visual-feedback-test-plan.md`.
- Unity preview: `Tools > Project > Field Effects > Preview Animations`.
- Work branch: `codex/field-effect-visual-feedback`; changes remain uncommitted for review.
- Saved content preserved: Ribbon targets all allies including wearer for 10s; Mask's recipe duration is 4s. These differ from the older authoring-template descriptions.
