---
slug: qte-background-clone-animation
status: approved
source: manual
gdd_tags: [combat, combat-scope]
owner: implementation-agent
human_checkpoint: not-required
next_agent: human
blocked_by: []
---

# QTE Background Clone Animation

## Goal and Approval

2026-10-29: the user requires Lightning Plunge and Execution Breakdown to share the ordinary outgoing background clone when switching characters: finish the current action/animation, then disappear. Clarification explicitly excludes spawning a clone just for chaining into Execution Breakdown.

## Routing and Context

- Workflow: `/fix-bug`.
- GDD `@tag:combat` / `@tag:combat-scope`: outgoing action remains until completion; repo project stack requires small systems.
- Existing TeamLoader already maps QteIntro to the shared PlayerTeamSwitchState executor. Both affected steps opt into syncAnimationToActionFrames; the executor does not implement that playback mode.
- Current implementation: one shared clone path. Target: preserve it and advance opted-in animations using the clone's existing action clock, matching PlayerAnimator.SampleActionFrame.

## Scope and Acceptance

- Modify PlayerTeamSwitchState only for action-frame animation sampling and finite completion. No new clone type, swap permission, damage, camera, VFX or chain behavior.
- Preserve outgoing frame, state/layer and hit history; do not replay the animation from zero.
- Complete the sampled animation and existing action/motion before removing the clone; retain the lifetime safety limit.
- Preserve normal Animator-driven clone playback.
- Compile and inspect the diff; manually verify both Main steps, CameraGirl/ordinary swaps and repeated swaps. No automated gameplay tests authorized or run in this slice.

## Game Feel

Camera/feedback tuning is unchanged. Clarity and response improve by keeping the outgoing actor's remaining pose sequence visible while control transfers. No numeric tuning or player-state transitions are introduced. Final visual confirmation remains human-owned.

## Verification

- Assembly-CSharp compiles successfully with Unity 6000.3.4f1's Roslyn compiler and existing editor response file, redirecting outputs to a temporary folder (exit 0).
- Scoped diff whitespace check passes; authored flags and zero-speed Animator states verified in saved assets.
- Manual gameplay checks remain in `../TestPlans/qte-background-clone-animation-regression.md`.
