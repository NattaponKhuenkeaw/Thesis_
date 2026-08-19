---
slug: "edge-snap-dash-sequencing"
status: needs-human
source: manual
gdd_tags:
  - "combat-scope"
  - "platform-input"
owner: "qa-agent"
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Edge Snap and Dash Sequencing Regression

## Test Plan Summary

| Test Area | Priority | Type | Platform |
| --- | --- | --- | --- |
| Edge/drop action output | P0 | Manual PlayMode | PC |
| Dash phase ownership | P0 | Manual PlayMode | PC |
| Landing Dash | P0 | Manual PlayMode | PC |
| Explicit output regressions | P1 | Manual PlayMode | PC |
| Compilation | P0 | Automated source compile | Unity 6000.3.4f1 |

## Functional Tests

### Ground attack crosses a ledge

GIVEN a ground `MatchInput` action whose movement carries or allows the player past a platform edge  
WHEN the action completes without reaching another surface  
THEN the FSM enters Air/Falling, the action animation is not canceled by Landing, and position never snaps vertically to distant ground.

Repeat while a thin collider sliver still contacts the edge: the `GroundC.x - 1` support ray must release Ground context before completion.

### Drop-through then immediate attack

GIVEN the player stands on a one-way platform  
WHEN Down+Jump initiates drop-through and Attack is pressed on the same or next input frame  
THEN an Air-context attack runs uninterrupted and completion remains airborne unless real contact has occurred.

### Dash Recovery ownership

GIVEN Dash is in Recovery before its authored Cancel frame  
WHEN Move, Attack, Skill, Ultimate, or Jump is pressed  
THEN Move remains locked, while Attack, Skill, Ultimate, and Jump may interrupt if their normal availability rules allow it.

### Dash Cancel boundary

GIVEN Dash reaches its first authored Cancel frame  
WHEN a legal buffered action or grounded movement input exists  
THEN the full action set and grounded locomotion resolve on that boundary.

### Landing Dash

GIVEN the player has just touched ground and Landing recovery is active  
WHEN Dash is pressed and cooldown permits it  
THEN the FSM enters Dash immediately, Dash animation replaces Landing, and the old landing timer does not resume afterward.

## Edge Cases

- [ ] Positive-Y MotionData on a ground `MatchInput` action stays Ground-owned.
- [ ] Explicit `Ground` output still performs its intentional ground correction.
- [ ] Explicit `Air` output on physical ground still reconciles through Landing after the action.
- [ ] Landing Dash during cooldown stays in Landing and does not clear its timer.
- [ ] Rapid Attack/Skill/Ultimate/Jump input during Dash Recovery follows the authored interrupt policy; walking never opens before Cancel.
- [ ] Air Dash Recovery retains neutral-input gravity hold and A/D fall release.
- [ ] Ledge action into immediate Dash resolves an Air Dash and consumes the normal air-dash resource.
- [ ] Pause/hit-stop during Recovery does not move the Cancel boundary early.
- [ ] Character switch remains unavailable from Dash.

## Input Coverage

- [ ] KB+M: Down+Space drop-through, LMB attack, Shift Dash.
- [ ] Controller: equivalent bound Drop, Attack, and Dash actions.

## Regression Checklist

- [ ] Ground action combos continue on the same resolver tree when support remains valid.
- [ ] Edge loss at a legal chain boundary reroutes to the matching Air root.
- [ ] Jump cancel from an explicit Ground output still uses its authored correction.
- [ ] Dash cooldown begins once when Dash exits.
- [ ] Landing still accepts Attack, Jump, Skill, and Ultimate according to existing behavior.
- [ ] No new Console errors or warnings attributable to the changed files.

## Automated Verification

- Unity runtime `Assembly-CSharp` compiled with the active 6000.3.4f1 Bee/Roslyn response file: **pass, zero errors**.
- Compiler emitted only existing obsolete-API/reference warnings outside the changed files.

## Human Checkpoint

The Editor connector was unavailable, so the P0 PlayMode scenarios and subjective Cancel-frame feel remain required before merge.
