---
slug: responsive-movement-flow
status: needs-human
source: manual
gdd_tags:
  - platform-input
  - combat-scope
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# DevLog: 2026-11-03 — Responsive Movement Flow

## Goal

Implement the user-approved fast move/jump/dash tuning, excluding tap/hold jump-height and gliding mechanics.

## What I Did

- Removed authored startup/landing pauses in the three existing movement profiles and startup freezes in nine character DashData definitions.
- Returned steering, facing, gravity, and locomotion animation at Dash Recovery without shortening its resource/cooldown bookkeeping tail.
- Supported zero-startup ordinary/double jumps and guarded the existing immediate-launch override against duplicate commitment.
- Removed the generic facing cooldown and the Landing state's residual one-tick movement lock.
- Consumed valid queued landing movement only after contact/resource reset and landing feedback.
- Added an isolated native validation menu; nine checks pass. Twelve tuning-field checks pass, preserving a concurrent Goenitz dash-speed edit to 35.
- Updated GDD and ADR-003 behavioral clauses to match the approved tuning while preserving motor/FSM/MotionData ownership.

## Key Decisions

Keep direct deterministic velocity, existing speed/arc identities, coyote time, input buffer, dash Active curve, resource limits, and authored cooldown durations. Recovery may return continuous control without ending the Dash state or granting another dash.

## Bugs Found

- Follow-up user report: unconditional Recovery/Cancel animation restoration cut Goenitz's long Dash clip, and gameplay completion could also truncate its remaining frames. Removed the phase-driven restoration and guarded neutral unfinished Dash presentation in PlayerAnimator. No gameplay timing/resource change; three real-controller regression groups added. Two neutral cases failed before the fix and all 12 native checks now pass.

- Zero jump startup needed a runtime clamp change and a duplicate-launch guard.
- Zero landing recovery alone still left a state-owned movement lock and did not execute queued Jump/Dash at contact.
- Landing feedback had to precede buffered movement so it could not overwrite the new launch animation.
- Inactive native Rigidbody2D fixtures discarded velocity assignments; validation now keeps only bare temporary physics bodies active, while gameplay facades remain inactive and isolated.

## Game Feel Notes

Native state/motor checks and compilation passed. No subjective traversal or rendered-animation playtest was claimed. Human review should focus on connected move/jump/dash sequences and neutral air-dash descent.

## Next Session

Run the focused human playtest plan and record the exact character/action pair for any remaining sluggishness. No commit or publication was made.
