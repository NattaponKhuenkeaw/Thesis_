---
slug: reward-reveal-delay-character-icon
status: approved
source: manual
gdd_tags:
  - ui
  - items
  - progression
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: Reward Reveal Delay and Memory Character Icon

## Player-Facing Goal

Wait one real-time second from room clear to the start of the reward sequence. The sequence does not add another wait for each Artifact or Memory panel. Gameplay and player input continue during that interval; the existing reward UI pause begins when the first panel appears. The Memory character chooser shows each character's portrait icon at a larger size.

## Source

- Origin: Manual prompt
- Link: Current Codex request
- Requested by: User

## GDD Reference

- `@tag:ui` - Character presentation emphasizes the character icon.
- `@tag:items` - Eligible Hard routes offer a three-card Artifact draw.
- `@tag:progression` - Hard combat rooms offer a permanent Memory choice for a living owned character.

## Type

- [x] Feature

## Scope

### Systems Affected

- Shared Artifact and Memory reward picker presentation.

### Files To Inspect First

- `Assets/Project/Scripts/UI/ArtifactRewardPickerUI.cs`
- `Assets/Project/Scripts/UI/ArtifactRewardPickerUI.Memory.cs`
- `Docs_JJK/3_Outputs/ADRs/021-visit-scoped-room-rewards.md`

### Out of Scope

- Reward ownership, payout order, claim behavior, or Memory progression rules.

## Acceptance Criteria

- [x] The reward sequence waits one real-time second from room clear, once per room, without locking player input or acquiring the reward pause hold during the wait.
- [x] Memory character choices bind to `CharacterItem.icon` and use tall portrait cards based on the `Char-Slot` size and aspect ratio, with three cards per row.
- [x] Memory node cards retain their existing size and layout.
- [x] Existing reward choice and room-visit ownership behavior is preserved.

## Human Checkpoints

- [x] Design/game-feel direction supplied in the user request.
- [x] Architecture remains unchanged; existing picker and room-owned reward flow are retained.
- [ ] Final visual/game-feel review.

## Router Decision

Recommended workflow: `/implement-feature`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/reward-reveal-delay-character-icon-design-spec.md`
