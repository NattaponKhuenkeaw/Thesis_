# DevLog: 2026-10-29 — QTE Outgoing Clone Animation

## Goal and Cause

The user clarified that switching away from Lightning Plunge or Execution Breakdown should leave the ordinary background clone to finish its current animation, then disappear. TeamLoader already routes QteIntro through PlayerTeamSwitchState, but that executor only initialized an Animator pose. Both affected authored steps opt into action-frame sampling and use zero-speed states, so autonomous Animator playback cannot finish their animation.

## Change

- Keep one shared outgoing clone implementation. Resolve the sampled clip's state/layer by the same clip-name convention used by PlayerAnimator, and sample from the outgoing action frame at initialization and subsequent logic ticks.
- Use the finite action clock for completion of manually sampled animation. Keep ordinary Animator completion checks and the lifetime safety guard.
- Clamp finite logic advancement at the execution endpoint so waiting for a normal clip does not process extra gameplay frames.
- Preserve character-switch rules, hit history, target motion, damage attribution, ordinary animation playback, cameras and unrelated working-tree edits.

## Verification

Assembly-CSharp compilation passed (exit 0) using Unity 6000.3.4f1's Roslyn compiler and existing response file with temporary assembly outputs. Scoped diff whitespace checks passed. Saved action flags and zero-speed Animator states were inspected. No automated gameplay tests added or run; visual swap checks remain in `../TestPlans/qte-background-clone-animation-regression.md`.
