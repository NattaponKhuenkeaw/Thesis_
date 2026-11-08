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

## Goal and changes

Apricha reported repeated backward Dimension Cuts when player/enemy positions overlap and requested predictable platform-edge blocking for action motion. Workflow: `/fix-bug`; task: `../Specs/action-motion-direction-and-ledges-task-card.md`. GDD: `@tag:combat-scope`, `@tag:platform-input`; preserve ADR 003 state/motor ownership.

Target capture previously turned on a tiny position difference, then the target-dash resolver derived its direction from an anchor that may be offset behind the actor. Capturing an overlapping target now preserves facing, latches that approach side, and prevents the anchor from reversing the committed path. Clearly separated targets still establish either approach direction before movement resolves. Initial automatic facing uses the same overlap rule.

`PlayerMovement.ActionSafety.cs` supplies one allocation-free support constraint after wall clipping. Grounded Relative, Absolute, SnapToEnemy and DashThroughTarget motion, gated combat steering, committed Dash and horizontal action teleports stop at the last supported leading-foot position. The path is sampled so a supported destination beyond a gap cannot bypass the guard. An already-overhanging body with rear support blocks outward travel and can return inward. Action phases, animation, hit processing, costs and cancels continue.

Ground context alone does not create support: airborne motion, intentional drop-through, genuine removed support and ordinary locomotion retain their existing paths. Ground visual lift and captured QTE plunge ownership remain unchanged. GDD now records the user-authorized movement rules alongside the existing cause-aware support-loss rules.

## Validation and discoveries

- Added native isolated motor/physics validation; all 50 focused cases pass, covering both directions, exact/tiny/wider overlaps, both floor types, all shared movement paths, repeated clipping, overhang, inward release, connected seams, lower floors, visual lift and air/drop/support-loss exceptions.
- Preview-scene colliders are invisible to the production global Physics2D queries in this Editor. Validation therefore uses temporary additive scenes, restores the prior active scene and closes every fixture without saving the user's scene or entering Play Mode.
- Broader movement validation exposed two stale fixture assumptions: Goenitz's existing authored dash has startup, and its current clip is shorter than the helper's fixed wait. The helper now advances startup and samples the actual remaining clip duration before/after completion. No character timing or animation asset was changed.
- Final responsive movement run: all 12 checks passed. Runtime/editor assemblies compiled, console contained no errors, and scoped diff whitespace checks passed. Evidence: `../Reports/ActionMotionSafety/native-checks.json` and `../Reports/ActionMotionSafety/responsive-checks.json`. Preserved the previous responsive report after copying this run into the task's report directory.

## Review boundary

Manual encounter testing and subjective combat feel remain pending using `../TestPlans/action-motion-direction-and-ledges-regression.md`. Support sampling is a starting geometric tolerance verified by the native cases; representative moving-platform, slope and performance checks remain manual. No new architecture, dependency, build setting, commit, merge or team publication was introduced. Existing unrelated working-tree edits were preserved.
