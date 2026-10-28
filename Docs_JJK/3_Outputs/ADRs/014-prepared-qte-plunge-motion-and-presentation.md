---
slug: main-qte-lightning-plunge
status: approved
source: manual
gdd_tags: [combat, combat-scope, visual-audio]
owner: architect-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# ADR-014: Prepared QTE Plunge Motion and Presentation

| Field | Value |
| --- | --- |
| Status | Accepted — Apricha's architecture “lgtm,” 2026-10-28 |
| Date | 2026-10-28, Asia/Bangkok |
| Author | architect-agent |
| GDD | `@tag:combat`, `@tag:combat-scope`, `@tag:visual-audio` |

## Context

Main's approved QTE design requires a physical enemy-relative airborne start, lightning-only descent, verified floor impact and an existing authored recovery follow-up. The current QTE returns one grounded position, the motor routes ordinary grounded Y motion to a visual offset, and QTE camera correction currently centers X only. Replacing assets alone cannot provide the requested movement/contact/cleanup semantics.

Design and architecture approvals: Apricha's successive “lgtm” replies to their respective checkpoints, 2026-10-28. [Detailed plan](../Specs/main-qte-lightning-plunge-arch-plan.md).

## Decision

Subsequent user-authorized visual polish shortens Main's startup to two action ticks and replaces the winding bolt with an off-screen near-vertical thunderstrike. Dedicated URP shader/material layers and a local Light2D remain presentation-only and use the existing action clock. No packages, pipeline/scene-volume changes or new gameplay authority are introduced. The latest pacing correction restores Main's existing handoff slow motion and adds independent visual hold/fade settings, superseding its temporary slowdown exclusion. Existing saved height/camera tuning and Heavy/recovery/follow-up ownership are retained.

Post-implementation playtest correction, explicitly requested by Apricha: the latest start height is Y+5, impact feedback tier is Heavy, ground VFX use a separate larger scale/longer tail, and camera framing locks to the body at a fixed handoff lens. This supersedes the initial floor-envelope framing and size-punch proposal. The scoped correction may lift beyond the room's vertical ceiling to show Main in midair, retaining supported horizontal bounds and a floor-safe lower camera limit. Runtime ownership remains unchanged.

Add a default-off QTE entry mode and runtime prepared-entry placement value. Validate start, exact landing X, floor continuity and vertical body clearance before consuming the swap. TeamLoader commits the prepared entry using the existing team transaction.

Append an authored captured-world-destination MotionData mode, sampled by the existing QTE action frame and physically applied only inside PlayerMovement. It bypasses grounded visual offsets, uses terrain sweeps and resolves fresh contact before the existing damage pipeline receives an Active frame. Moving support is captured collider-locally and revalidated. Blocked or invalid support aborts without impact damage and returns control using physical support.

PlayerQteIntroState owns the plunge root's renderer flags, reusable VFX instance and generation-scoped camera token, with cleanup before any next-step transition as well as on state exit. LightningPlungeVFX only renders supplied world-space geometry and manually sampled cosmetic particles. CameraManager owns bounded X/Y handoff reset and contact feedback using existing Cinemachine cameras. No independent ability director or global time authority is introduced.

Main's QTE-specific root changes while its existing Execution Breakdown next node and ordinary manual Dimension Cut remain separate. Existing serialized defaults retain grounded entries for other profiles. Editor authoring must reproduce the new Main entry rather than overwrite it with the old cut.

## Alternatives considered

| Option | Benefit | Reason not selected |
| --- | --- | --- |
| Change Main's QTE asset only | Small authored edit | Current placement remains grounded and ordinary Y motion may be visual-only; camera and follow-up cleanup gaps remain. |
| Keep the body on the floor while a bolt falls above it | Reuses grounded placement directly | Does not implement the approved physical airborne entry and leaves an invisible floor-level body during descent. |
| Independent VFX/movement coroutine or Timeline director | Easy bespoke sequencing | Introduces a competing motion/clock authority and can desynchronize damage, hitstop, visibility and follow-up. |
| Broaden existing action-camera QTE eligibility globally | Reuses one API | Changes presentation behavior beyond this Main-only entry; a scoped QTE extension preserves existing exclusions. |
| Prepared placement + existing motor/state + reusable visual component | Explicit placement/contact/ownership; current action clock stays authoritative | Requires reviewed opt-in integration across the existing handoff, motor and camera surfaces. Selected proposal. |

## Consequences

### Positive

- Actor motion, contact damage, visible effect phases and camera accent share action-phase boundaries.
- Terrain validation happens before QTE consumption, and live support failure cannot emit a floating impact hit.
- Root cleanup cannot overwrite Execution Breakdown's new renderer/camera owner.
- The effect can be reused or pooled without owning gameplay state.
- Existing packages, pipeline, ordinary QTE defaults and gameplay damage delivery remain authoritative.

### Trade-offs

- A physical captured-world motion path requires explicit motor sampling before hit processing; it cannot be approximated by an ordinary grounded relative key.
- Main's authored entry/controller/utility must migrate together. Manual action references must not be mutated through shared data objects.
- Moving-platform and interruption behavior need manual gameplay review. Supported final camera-position correction remains limited to verified confinement geometry; unsupported shapes use safe existing camera behavior and are reported.
- A next-step transition may truncate visual remnants to guarantee ownership cleanup; persistent tails across Execution Breakdown are outside this proposal.

### Migration and checks

Follow the affected-systems list and implementation order in the architecture plan. Append enums without renumbering, default to the current grounded profile, preserve old method overloads where needed, and isolate Main-specific data. Compile and inspect the console, then manually preview physical contact, both facings, edge/roof rejection, pause/hitstop, repeated reuse, early follow-up and cleanup. No automated tests are added or run per the user.

## Related

- [Approved design](../Specs/main-qte-lightning-plunge-design-spec.md).
- [ADR-003: state-owned movement](003-deterministic-player-state-owned-movement.md).
- [ADR-008: unified damage delivery](008-unified-player-damage-delivery.md).
- [ADR-013: action-owned camera presentation](013-action-owned-camera-presentation.md).
- [Approved prior QTE architecture](../Specs/qte-team-swap-arch-plan.md).
