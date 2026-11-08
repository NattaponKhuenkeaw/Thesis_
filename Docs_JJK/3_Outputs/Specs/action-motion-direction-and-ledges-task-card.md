---
slug: action-motion-direction-and-ledges
status: needs-human
source: manual
gdd_tags: [combat-scope, platform-input]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Stable dash direction and grounded action ledges

Workflow: `/fix-bug`. Source: Apricha's 2026-11-07 request. GDD: `@tag:combat-scope`, `@tag:platform-input`; existing state/motor ownership follows ADR 003.

## Authorized behavior and implementation scope

- Dash Through Target preserves the committed facing when player/enemy bodies overlap. An enemy anchor offset must not reverse the dash. A clearly separated target on either side still establishes the approach direction at capture.
- Grounded action displacement stops before losing floor/platform support. Apply the motor's existing wall constraints first, then one shared support constraint for Relative/Absolute/SnapToEnemy/DashThroughTarget motion, gated combat steering, committed ground dash and horizontal action teleports.
- Stop displacement at an edge while the action's animation, hit processing, phase/cancel timeline and resource cost continue normally. No new state, dependency, serialized setting or architecture is needed.
- Preserve authored visual lift, airborne actions/dashes, intentional jumps, drop-throughs and ordinary locomotion. Captured QTE plunge already validates its landing support/path and remains unchanged. If support actually disappears, retain existing cause-aware Air output behavior.

This request supplies the direction and ledge behavior approval. Final subjective feel remains a human checkpoint after a concrete implementation. Ordinary walking off ledges is outside this action-motion fix.

## Experience and state checks

Clarity: overlap does not cause an unexpected reversal; a grounded action cannot unexpectedly send the player off a ledge. Response: valid actions still commit and retain their authored cancels. Satisfaction/Fit: existing animation, audio and VFX remain. Motivation: unchanged combat resources.

Entry/exit, interruption, chains and resource consumption retain existing FSM rules. Edge clipping affects displacement only. Ground support, slopes, one-way floors, gaps, missing/removed support, hitstop, opposite facing and repeated input require regression coverage. No combat timing, speed or damage tuning is proposed.

## Acceptance and validation

- Reproduce overlap with tiny offsets on both sides and Dimension Cut's negative target-anchor offset; verify forward travel in both facings.
- Verify distant left/right target capture, optional face-on-arrival and targetless fallback.
- Verify both ends of solid and one-way platforms for every shared movement entry point, including an instant teleport over a gap to another supported destination.
- Verify a blocked action retains its state/timing and can move inward again; check real airborne and drop-through paths remain free.
- Compile in Unity, run isolated native motor/physics regressions, check the console and scoped diff whitespace. Keep current scenes and unrelated working-tree changes intact.

## Router decision

Current artifact: this task card, containing the minimal bug-fix design and implementation plan. Next output: implementation diff, `3_Outputs/TestPlans/action-motion-direction-and-ledges-regression.md`, and dated DevLog. No architecture change or new ADR is proposed. Human owns final gameplay feel and merge.

## Implementation result

Implemented the captured approach side/overlap-facing rule and shared grounded-action ledge constraint without changing authored combat costs/timing or the FSM architecture. Unity compiled runtime/editor assemblies with no console errors. All 50 focused motion checks and all 12 responsive movement checks passed; reports are in `../Reports/ActionMotionSafety/`. Two stale animation-test fixture assumptions were corrected to respect authored startup and actual remaining clip duration. Final live gameplay feel remains the human checkpoint.
