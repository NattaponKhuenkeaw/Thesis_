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

# Action direction and platform-edge regression

GDD: `@tag:combat-scope`, `@tag:platform-input`. Scope and state contract: `../Specs/action-motion-direction-and-ledges-task-card.md`.

## Native checks

Run **Tools > Project > Validation > Run Action Motion Safety Checks**. Uses temporary additive-scene physics bodies and inactive gameplay facades; closes each temporary scene and writes `output/action-motion-safety/validation.json`. Restores the user's active scene and never saves it or enters Play Mode. Preview scenes are unsuitable because the production motor's global Physics2D queries do not see their colliders in this Editor.

Cover both facings with exact/tiny/wider body overlaps, target anchor behind the start, separated targets, all four ordinary MotionData types, solid and one-way platform edges, repeated edge pressure/inward release, committed ground/air Dash, horizontal teleport across a gap, removed/ignored support and ordinary walking. Existing responsive movement checks cover dash recovery/cooldown, jump and landing ownership.

Support sampling starts at **0.1 world units**, matching the scale of existing placement checks, with boundary refinement to a quarter of the existing wall skin. This is a starting engineering tolerance, not a combat balance value. Pass: edge clipping leaves the leading foot supported with no outward creep; a traversable gap larger than the sampling interval cannot be skipped. If a real level has smaller gaps that produce support loss, reduce the sample interval and repeat the path checks, watching physics-query cost. Ray buffers are reused without per-tick allocations.

## Manual gameplay acceptance

- [ ] Shin/Main: dash into a live enemy until the bodies overlap. Trigger cached Dimension Cut repeatedly while facing right, then left. Test enemies slightly ahead, behind and exactly centered. Cut starts along committed facing and preserves target hold/hits.
- [ ] Cut against clearly separated enemies on either side; verify captured approach, normal minimum travel, targetless forward fallback, target disappearance and optional face-on-arrival behavior.
- [ ] Repeat Dash Through Target, Relative/Absolute action motion and Snap To Enemy at both edges of a solid platform and a one-way platform. The actor stops on supported ground; the action's animation, hit events, costs and cancel frames finish normally.
- [ ] Hold gated movement during an action at an edge, then move inward. Outward movement stops without jitter/creep; inward control works as soon as the gate allows it.
- [ ] Use an instant horizontal action teleport toward a target beyond a gap with valid floor on the far side. Stop on the starting platform. Verify ordinary wall blocking remains.
- [ ] Use ordinary ground Dash at an edge, then allow Recovery: committed burst stops, while returned locomotion follows existing walking/falling rules. Cooldown remains tied to actual Dash exit.
- [ ] Jump off the edge, air Dash and use air actions. Confirm normal airborne travel, gravity/output routing and jump resources. Down+Jump still drops through one-way support.
- [ ] Verify authored visual lift, QTE plunge and target anchors near edges. Removing/moving the supporting platform must allow genuine loss-of-support handling rather than freezing the body in air.
- [ ] Exercise connected platform seams, shallow slopes, lower nearby platforms, hitstop, slow motion, pause/unpause, rapid input and an action interrupted by death. No persistent velocity, stuck facing or new console errors.
- [ ] Confirm prediction/readability on keyboard and controller. New players and observers should see the action continue while movement stops at the edge; experienced players can still cancel at authored windows. Check that airborne actions remain the intentional means of crossing gaps.

## Actual results

Unity compiled runtime/editor assemblies with no console errors. All **50 focused native motion cases** passed, followed by all **12 responsive movement checks**. See `../Reports/ActionMotionSafety/native-checks.json` and `../Reports/ActionMotionSafety/responsive-checks.json`. The existing responsive animation fixture now advances authored startup and tests the actual remaining clip duration instead of fixed waits. Its previous generated report was restored after copying this run's evidence.

Manual gameplay acceptance remains pending. Performance/game-feel approval requires a representative live encounter; native checks validate motor/state/presentation behavior only.
