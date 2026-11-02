---
slug: targetless-qte-dimension-cut
status: needs-human
source: manual
gdd_tags: [combat, characters, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Targetless QTE, Dimension Cut and ability HUD feedback

Source: Apricha's 2026-11-02 bug report and clarification that the alternate sprite is the HUD dash button. Workflow: `/fix-bug`; use the current checkout and preserve existing local edits. GDD: `@tag:combat`, `@tag:characters`, `@tag:visual-audio`. This direct request supersedes the older Main spec's target-required cut and lost-target ordinary-swap policy.

## Authorized behavior

- An earned, unexpired QTE can execute after its enemy dies, disappears or leaves range. Prefer a usable live target; otherwise place the intro relative to the outgoing player's current position and facing. Ground, body and path safety still apply. No hit means no newly earned QTE window.
- Main's cached Dimension Cut can execute on Normal Attack without an enemy, spending one cached charge and using its existing forward movement fallback. With an enemy, preserve target capture, facing, anchoring and authored range. No changes to timing, damage, dash cooldown, air quota or costs.
- Show Main's existing `UI_Dash_HaveStack 1.PNG` HUD sprite whenever at least one full Shadow Meter box is available, independent of enemy presence. Revert below that threshold or on character switch. Keep cooldown overlay and availability dimming. Expose the alternate artwork beside Dash Icon in the animation editor.
- Follow-up: successful Skill, Ultimate and Dash commitment plays the same white HUD overlay as Normal Attack. Reuse existing pulse duration/scale. Rejected/buffered input and automatic intro/chain steps do not report successful manual commitment.
- User explicitly requested no script tests: do not add, update or run automated test scripts for this task.

## Implementation and state rules

Use existing QteTeamSwapController placement, CombatAction target gate, PlayerMovement fallback and AbilityUIManager refresh. No new architecture or dependencies. Existing state entry/cancel/exit rules remain. QTE window/transition checks still reject pause, hitstun, invalid roster and unsafe terrain. Cache cost is paid only on committed cuts; dash conversion remains on committed dash.

## Experience check

Response: an enemy disappearing does not veto the requested action. Clarity: the icon indicates a full spendable meter box, not cached cuts. Satisfaction/Fit: retain authored movement, animation, VFX and audio. Motivation: existing meter/cached-charge economy remains. No tuning values proposed.

## Acceptance / review

- Verify both grounded and plunge intros after target loss, including while ordinary swap cooldown is active; unsafe placements remain rejected.
- Verify targetless cached cuts on ground/air, multiple cuts, empty cache, nearby target acquisition, minimum travel and walls.
- Verify HUD at below/at/above one box, after dash spending, cached cuts alone, cooldown, missing alternate sprite and character changes.
- Verify committed Skill/Ultimate/Dash white feedback, including cooldown/resource rejection and repeated inputs.
- Check compilation/console and inspect changes; leave combat and visual acceptance to manual playtesting. Record actual results and limitations in the DevLog. No automated script tests.
- Human owns final combat feel and visual approval; no merge or publishing is authorized.

## Implementation result

Implemented the existing movement fallback by removing Main Dimension Cut's enemy-required asset gate while retaining optional target capture at commitment. QTE placement uses the outgoing position only when no live in-range target exists; an existing live target with unsafe placement still rejects the prepared entry. Both grounded and plunge fallback placement retain terrain checks.

Main's DashData now references the existing charged artwork through `meterReadyIcon`; the Animation Editor exposes it beside Dash Icon. AbilityUIManager reads the current character's meter threshold and keeps its existing cooldown overlay/dimming. Successful manual Skill/Ultimate steps and the existing committed dash event reuse the NA white pulse overlay; QTE intros and automatic chains are excluded.

Validation: Unity refreshed and compiled the final runtime/editor scripts, with a completed assembly reload in Editor.log, output assemblies newer than the modified sources and no C# errors in the log. Scoped `git diff --check` passed. No script tests were retained or run. Live combat and visual approval are pending manual review; the MCP bridge became intermittently unresponsive during refresh, so this is not a Game-view verification.
