---
slug: main-meter-max-cancel
status: draft
source: manual
gdd_tags: [combat, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Implementation architecture for review

- CharacterObject owns `CharacterCombatRules`: ordered hit gain rules, no resolver passive payload.
- CharacterPassiveController applies the selected amount in the existing accepted ordinary-hit
  notification, using the existing counter multiplier and meter cap. Six legacy Main hit-gain
  effects are removed; Dash-to-cache conversion and Potential effects remain.
- QteSwapDefinition owns the committed-intro tag/counter reward and the marker's consume action id.
  PlayerQteIntroState grants it once per Enter after starting the intro. CharacterPassiveController
  removes the marker on a manual on-field start of the authored consume action.
- QteTeamSwapController selects an enemy together with a valid placement. It checks alternative
  enemies when the original target has no safe placement. TeamLoader retains ordinary fallback.
- Dimension Slash's existing action descriptor moves to skillActions, with matching availability
  and consume filters. Stable ids and every managed-reference payload remain unchanged. The authoring
  utility migrates the action descriptor without regenerating its combo.
- Action Type is a small CombatAction policy/white-flash override; Category/input/costs stay unchanged.
- PlayerUltimateState previews a distinct destination with scoped source-cooldown exclusion,
  validates AE/counter costs before outgoing cleanup, then adopts that exact resolver result.
  ActionResolver.AdoptResolved preserves the chosen route, matched conditions and follow-up nodes.
  ExecuteCombatStep is told when the action's counter cost was already funded, preventing double spend.
- Shared ExecuteCombatStep detects committed Ultimate-type Recovery replacements across input routes,
  using existing TransitionContext for source classification. Flash type is snapshotted independently
  of damage/passive category, including projectile and off-field clone presentation.
- MaxCancelFeedback is owned by the player, creates a separate screen overlay so facing cannot
  mirror the label, blocks no raycasts, and releases it when the owning component is destroyed.

This uses the current per-character runtime-state ownership and action-owned cleanup paths; it adds
no dependencies, scene/prefab migration, hard-coded Main identity checks in runtime code, or new
player state. Review alongside ADR-003 (action movement ownership), ADR-005 (deployed state authority)
and ADR-013/014 (action presentation/QTE ownership). This document does not claim ADR acceptance.
