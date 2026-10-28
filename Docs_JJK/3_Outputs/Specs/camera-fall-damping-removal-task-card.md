---
slug: camera-fall-damping-removal
status: approved
source: manual
gdd_tags:
  - identity
owner: implementation-agent
human_checkpoint: not-required
next_agent: human
blocked_by: []
---

# Task Card: Remove Fall and Landing Camera Damping

## Player-Facing Goal

Keep normal camera following while removing the extra downward anticipation and upward recovery when the player falls and lands.

## Source and Approval

- Direct user request, 2026-10-28: remove camera up/down damping on falling and landing because it is over-engineered and looks bad.
- This request approves the removal direction. Subjective final feel still requires an in-editor playtest.

## GDD Reference

- `GDD_Neo-JJK.md @tag:identity`: readable, fast action and simple, reliable systems. The GDD does not require special fall/landing damping.

## Type and Routing

- Bug fix / camera-feel correction; workflow: `/fix-bug`.
- Implementation uses existing camera components; no new architecture, dependencies or settings.
- Keep the current checkout to preserve the other ongoing work.

## Scope

- `Assets/Project/Scripts/Combat/CameraLookAhead.cs`: remove velocity-based vertical offset, smoothing and settings. Track player Y directly.
- `Assets/Project/Scripts/Cameras/CameraManager.cs`: remove legacy fall-triggered damping API, coroutine, fields and state; preserve player-reference resolution and normal Cinemachine damping.
- `Assets/Project/Prefab/Camera/CameraSetUp.prefab`: remove serialized settings belonging to the removed features.
- Preserve horizontal look-ahead, confinement, combat framing, camera shake and authored action/QTE presentation.

## Acceptance Criteria

- [x] Camera proxy follows player Y without a fall offset or landing recovery tween (code inspection).
- [x] Falling/landing cannot change exploration or combat damping through the legacy path.
- [x] Removed settings and API have no remaining asset/code references.
- [x] Existing horizontal, room and action camera logic stays intact in the diff.
- [x] Assembly-CSharp compilation succeeds using Unity's cached compiler response file with temporary output; scoped whitespace/reference checks pass.
- [ ] In-editor playtest and subjective final feel validation; see `../TestPlans/camera-fall-damping-removal-regression.md`.

## Game-Design Check

- Domain: camera. Clarity/response: remove the velocity-dependent dip and recovery so vertical targeting stays predictable.
- Fit/satisfaction: apply the user's preferred simpler follow; add no feedback or tuning values.
- Motivation and player-state transitions: unaffected.
- Playtest: jump/apex/fall/land, long drop, repeated jumps, midair swap, explore/combat transitions, room walls and QTE plunge. Pass when ordinary falling/landing adds no dip/rebound and existing authored camera presentation still works.
