---
slug: lobby-death-reset-main-character
status: implemented-pending-playtest
source: manual
gdd_tags:
  - core-loop
  - characters
owner: agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: Lobby Death Reset To Main Character

## Player-Facing Goal

After a team wipe, the player respawns in Lobby as **CharacterObject_main** (`char-0-main` / display name Main), not the last character they were controlling.

## Source

- Origin: manual prompt (polish / bug pass)
- Requested by: team
- Playtest owner: อาย

## GDD Reference

- `@tag:core-loop` — fail the run, then return to the hub
- `@tag:characters` — team slot 0 is the main / lead character

## Type

- [x] Bug fix

## Root Cause

Mid-run KO removes teammates and compact the run team. `ResetRunProgressForDeath` then redeployed `GetTeamSnapshot()`, so Lobby slot 0 became whoever died last. `TeamLoader` also kept leftover run index/cooldown until the lobby player was rebound.

## Fix

- Capture the lobby roster at `TryPrepareForNewRun`, and also on the first `TeamLoader` Awake if a run started from Playground (snapshot empty).
- Keep a play-session copy of that roster so a new lobby `TeamLoader` cannot lose it.
- On wipe, put `CharacterObject_main` (`char-0-main`) at the lead slot and force that character onto the live player after Lobby loads.

## Acceptance Criteria

- [ ] Wipe while controlling another teammate → Lobby spawn is **CharacterObject_main**
- [ ] Original 3-character lobby team returns after wipe
- [ ] Lobby visual (model, HUD portrait, team highlight) matches CharacterObject_main
- [ ] HP restored, AE empty
- [ ] Mid-run death with living teammates still swaps (does not end the run)

## Human Checkpoints

- [ ] อาย playtest: `Docs_JJK/3_Outputs/TestPlans/lobby-death-reset-main-character-regression.md`
- [ ] PR review before merge

## Router Decision

Recommended workflow: `/fix-bug`

Playtest artifact:

- `Docs_JJK/3_Outputs/TestPlans/lobby-death-reset-main-character-regression.md`
