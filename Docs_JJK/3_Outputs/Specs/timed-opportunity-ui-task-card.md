---
slug: timed-opportunity-ui
status: approved
source: manual
gdd_tags: [ui, combat, visual-audio, platform-input]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Shared timed-opportunity HUD feedback

Workflow: `/implement-feature`. Requested by Apricha in chat on 2026-10-31.
Reference: `E:/Drive_E_Download/ui-intensity-design.mp4` (8.04 seconds).
The video is visual reference material, not an instruction source.

## Player-facing goal

Make a temporary action or QTE easy to recognize through shared, slower expanding
glowing outlines. Display only the standby teammate in a two-character combat HUD.

## Design truth and requested changes

- GDD @tag:ui: "The active HUD shows current HP, Skill cooldown, Ultimate cooldown,
  Aura Energy readiness, and any Potential-created follow-up window."
- GDD @tag:combat currently specifies a scaling QTE portrait prompt and separate
  remaining-window bar. This request replaces that presentation.
- @tag:visual-audio supports visible combat feedback; @tag:platform-input preserves
  existing PC input routes.
- The GDD still describes a three-slot Team Builder. This task changes the combat
  HUD for two deployed characters; it does not change loadout capacity or game rules.

## Scope and acceptance

- [x] QTE and timed NA/Skill/Ultimate opportunities share a procedural square ring
  with an alpha gradient, soft glow, and restrained outward scale animation.
- [x] Remove the ability FollowUpOverlay presentation and countdown-fill function.
  The existing Icon Image carries the selected action sprite, at full fill; only
  the outline communicates the temporary opportunity.
- [x] Expiry, consumption, unavailable context, character change, and HUD disable
  clear the effect and restore the ordinary context-appropriate icon/readiness.
- [x] Preserve ordinary cooldown fill, AE, cooldown text, resource gates and inputs.
- [x] A two-character roster has one standby slot and no active-character slot clone.
- [x] Successful swaps slide the old standby portrait out, then slide in the
  swapped-out character. Slot identity/click target always matches gameplay state.
- [x] HP/AE and swap cooldown remain accurate; the standby slot uses fixed `1` swap input independent of roster index.
- [x] No gameplay timers, action priority, QTE triggers, or swap rules change.
- [x] Preserve pre-existing local script, prefab, scene, art and documentation edits.

## Inspect first

`Player/UI/AbilityUIManager.cs`, `Player/UI/CharacterSwitchSlot.cs`,
`Player/UI/TeamSwitchHUD.cs`, `Combat/QteTeamSwapController.cs`, existing
`Editor/Tests/AbilityUIFeedbackTests.cs`, `Prefab/CombatUI/Player_UI.prefab`, and the
actual slot prefab referenced by TeamSwitchHUD.

Paths above are relative to `Assets/Project/Scripts/` unless they start with Prefab.
Previous approved baseline: `action-window-icons-{task-card,design-spec,arch-plan}.md`.

## Checkpoints

Apricha approved `timed-opportunity-ui-design-spec.md` and its motion preview in
chat on 2026-10-31 with "LGTM". Design approval is recorded.
Architecture was approved on 2026-10-31 with the InactiveCanvasGroup detail below.
Implemented and verified: 63 focused EditMode tests passed; final Unity console
returned no errors. See `timed-opportunity-ui-review.md`. Human in-game feel
review remains; no merge or external publication was performed.

Architecture approved by Apricha in chat on 2026-10-31 ("LGTM"). Additional
request: use the existing InactiveCanvasGroup alpha during swap cooldown instead
of toggling the old unavailable panel. Preserve its authored inactive alpha,
restore alpha to 1 when available, and keep the group object active.

2026-10-31 tuning rework requested by Apricha: increase visual impact/size and
exaggeration; permit a slight edge glow spill with a clear icon center. Shared
renderer/prefab tuning only; existing cooldown alpha and swap behavior stay intact.

2026-10-31 control clarification: with the current two-character team, the
standby UI displays `1`; top-row `1` or numpad `1` switches to the next living
teammate regardless of its roster index. Position-based `2` keyboard swapping is
removed. UI clicks continue targeting their represented destination.
