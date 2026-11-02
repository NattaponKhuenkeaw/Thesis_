---
slug: main-skill-potential
status: needs-human
source: manual
gdd_tags: [combat, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main Skill Potential revision

Source: Apricha's 2026-11-02 prompt and clarification: Lv4 grants one percentage point of both DOT Mastery (DM) and Additional Damage Increase (ADI) per 8 of Main's ATK. Exact kit values come from this request; GDD `@tag:combat` and `@tag:characters` define Skill, AE and stat semantics.

Workflow: `/fix-bug` for the reported Magnetism and source-stat blockers, with the user's requested kit replacement as acceptance criteria. Existing unrelated local edits must be preserved. No CI, dependencies, build settings, publishing or merge changes.

## Approved behavior

- Base: remove the 15% incoming-damage debuff.
- Lv1: Skill hits apply +20% enemy damage taken for 17 seconds; guaranteed Skill AE Reward increases by 5.
- Lv2: replace the old extra-hit upgrade with gentle continuous Magnetism toward the Dark Chain field center.
- Lv3: replace the old action damage multiplier upgrade with +40% enemy damage taken for 17 seconds, replacing Lv1's 20% rather than stacking.
- Lv4: replace the old crit buff with +DM and +ADI equal to Main's ATK / 8 percentage points for all allies including Main, for 17 seconds. Fractional values are preserved. Refresh without stacking; snapshot Main's effective ATK on application per ADR-005.
- Potential descriptions and expanded Skill text must match the new progression.

## Implementation scope

Reuse ADR-002 recipes and ADR-005 snapshots for source-stat scaling; no stat architecture redesign. Reuse the existing terrain sweep in EnemyHitReactionHandler for horizontal field pull. Queue pull separately from damage so damage cooldown, hit limit and stop-on-impact cannot stall it. Respect death, armor, reaction immunity, grab ownership, hit freeze, terrain and pool reset.

Files: Main character and upgraded Skill projectile assets; ProjectileData, ProjectileRuntime, EnemyHitReactionHandler; projectile authoring controls; focused Skill and movement regression tests.

## Tuning and review

Starting pull speed: 1.5 world units/second within the existing Skill hitbox (8 by 8). Grounded enemies retain Y; flight/reaction vertical motion retains ownership. Test left/right/center targets for monotonic inward movement between damage ticks, no overshoot or wall penetration. Reduce speed if it overpowers movement; increase if the pull cannot be perceived. This is a reviewable starting value, pending human game-feel approval.

Clarity: reuse Dark Chain's visible field and damage-taken marker; panel text exposes progression. Response: no new player lock or input path. Satisfaction/Fit: retain existing Skill animation, VFX and audio. Motivation: upgrades improve enemy grouping and team damage. Existing action entry, cancellation, resource cost, lifetime and output context remain authoritative.

## Acceptance checks

- [x] Base/Lv1/Lv2/Lv3/Lv4 resolve the requested effects and descriptions.
- [x] Vulnerability refreshes to exactly 20% or 40%, never 60%, and expires at 17 seconds.
- [x] Main and off-field allies receive Main's ATK / 8 DM/ADI, refresh without stacking, and expire at 17 seconds.
- [x] Pull continues between hit ticks and after damage hit quota, stops after expiration/reset, preserves Y and respects terrain/immunity.
- [x] Unity compiles and 68 focused regression tests pass.
- [ ] Human combat feel/readability review (required after implementation).

## Implementation and verification

The live Editor retained stale kit values after external YAML import. `MainSkillPotentialAuthoring.Apply` applies the exact approved revision to the loaded objects, with Undo, and saves only Main and the Lv2 Skill projectile. It was executed successfully; no automatic startup migration is installed. The Lv4 projectile is no longer selected by this track and retains the user's previous edits.

Source-stat regression reproduced: after applying with 80 ATK and reapplying with 164 ATK, the old implementation retained +10 DM/ADI rather than +20.5. Same-recipe stat-buff refresh now rebinds the incoming source snapshot in both inventory and receiver paths, then refreshes duration. Other formula types retain their prior reapply behavior.

Focused menu: `Tools/Project/Combat/Validate Main Skill Revision`; result `Temp/main-skill-revision-tests.xml`: **68 passed, 0 failed** (18 damage, 32 enemy position, 10 Magnetism, 8 Skill progression/source-stat checks). Unity console: no compilation errors. `git diff --check` passes for changed tracked scripts.

Broader menu: `Tools/Project/Combat/Validate Main Potential`; result `Temp/main-potential-tests.xml`: **124 passed, 11 failed**. Remaining failures are Shadow Meter authored gain expectations (four cases), Void occupancy gain expectations (three), Dimension Cut target-radius expectations (two), and two cache/target-availability cases. These were also present in the initial validation run during this change; the current local meter/dash edits are outside this Skill revision. No unrelated values or expectations were changed to make the suite pass.
