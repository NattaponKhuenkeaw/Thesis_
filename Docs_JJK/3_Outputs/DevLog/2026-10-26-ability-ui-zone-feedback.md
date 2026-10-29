---
slug: ability-ui-zone-feedback
status: E2E-pending
gdd_tags: [ui, combat]
owner: Codex
human_checkpoint: requester E2E
next_agent: human
blocked_by: []
---

# Ability UI and zone feedback — 2026-10-26

Implemented the behavior in `../Specs/ability-ui-zone-feedback-task-card.md`.

- Dash overlay/countdown reads the actual movement cooldown frames. Expiry starts the existing ready rush/pulse. Unavailable dash input uses the existing rejected-feedback effect without changing input buffering.
- Committed manual normal-attack steps pulse NA_UI. Automatic follow-ups do not pulse it.
- Skill/Ult binding and character refresh seed readiness silently. Previously `HandleCharacterChanged` reset the ready flags to false, causing an already-ready skill to bounce immediately; redundant UI cooldown ticking could also schedule ready feedback separately from gameplay readiness.
- PlayerHUD accepts zone commands through `OnZoneChanged(ZoneMode)` and listens to the existing provider: safe hides HP/Cool and shows Navigation; combat does the inverse. The controller stays active.
- Wired existing Dash/NA and previously empty Ult references in Player_UI. Preserved the user's prior prefab/scene changes; disabled copied NA cooldown visuals.

## Verification

- Unity's Roslyn compiler: runtime scripts and editor/test scripts compile successfully. Existing obsolete-API warnings remain.
- Unity EditMode run: **22 passed, 1 failed, 0 skipped** across AbilityUIFeedbackTests, SafeRoomPresentationTests, PlayerActionInterruptTests and RunCurrencyHudTests.
- **All 7 new AbilityUIFeedbackTests passed**, including dash timer synchronization/one rush, silent rebinds, both zone transitions with/without Navigation, and prefab references/controller placement.
- The broader failure is `PlayerActionInterruptTests.TakeDamage_DuringStartup_InterruptsIntoHitstun`, line 295: expected `fsm.HitstunState` is null while CurrentState remains TestActionState. This test concerns startup damage interruption; no damage/interruption implementation was changed in this task. Recorded without expanding the UI task to alter combat interruption.
- Detailed local results: `Temp/ability-ui-editmode-results.xml`. Temporary automatic launcher removed after the run.
- E2E feel and scene transitions remain with the requester as requested; checklist is in the task card.
