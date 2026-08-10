# 2026-08-10 - Edge Snap and Dash Sequencing

Fixed cause-aware air resolution and aligned Dash/Landing transitions with the latest manual game-feel direction.

## Bugs Found

- Ground-started `MatchInput` actions always projected Ground at exit, so genuine ledge loss could call the unbounded authored Ground snap and move the player to a distant floor.
- Drop-through was recorded separately from lost support and was not treated as a genuine airborne boundary by combat routing.
- Dash exposed action interrupts and grounded locomotion during Recovery instead of waiting for its authored Cancel phase.
- Landing rejected Dash, and ground-dash validation rejected any active landing timer, preserving the Landing animation/lock.

## Changes

- Added one cause-aware ground-departure predicate covering ledge loss and authored drop-through while excluding positive-Y MotionData.
- Made ground-started `MatchInput` resolve Air after genuine departure for completion, cancellation, and projected successor routing; explicit Ground/Air outputs remain authoritative.
- Restricted Dash Recovery to no action interrupts and moved grounded movement/facing/animation handoff to Cancel.
- Allowed Landing to route Dash normally and clear the landing timer when Dash starts.
- Added the user-directed short `GroundC.x - 1` support ray so a lingering edge-contact cannot hold a grounded action context.
- Corrected Dash Recovery policy: discrete Attack, Skill, Ultimate, and Jump interrupts remain legal there; only walking/facing wait for Cancel.
- Updated `@tag:combat-scope`, ADR-003, and the task/design/architecture artifacts to record the new contract.
- Added the focused manual regression plan.

## Verification

- `git diff --check`: pass.
- Unity 6000.3.4f1 runtime assembly compile using the project Bee/Roslyn response file: pass with zero errors.
- Existing unrelated Kyo character asset modification was preserved and excluded from this work.
- In-Editor P0 feel/reproduction scenarios remain a human checkpoint because the Unity connector was unavailable.

## Next Session

- Run the P0 ledge attack, drop-through attack, Dash Recovery/Cancel, and Landing Dash scenarios in `Test_Playground`.
- Verify both KB+M and controller mappings.
- Review the authored Dash Cancel frame if the newly committed Recovery tail feels too long; do not change timing without a playtest note.

## Git Commit Summary

```text
fix(player): correct edge output and dash cancel sequencing

- resolve genuine ledge and drop-through MatchInput outputs as Air
- keep Dash committed through Recovery and open control at Cancel
- allow Landing to transition directly into an available Dash
```
