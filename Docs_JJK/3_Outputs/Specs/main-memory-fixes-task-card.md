---
slug: main-memory-fixes
status: needs-human
source: manual
gdd_tags: [characters, combat]
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Main Memory fixes

Source: Apricha's 2026-11-07 Main Fix request. Workflow: `/fix-bug`.
Context: GDD `@tag:characters` Memory ranks; `@tag:combat` skill purpose. The direct request corrects the earlier character-memory card's Lv3 stun boundary.

## Scope and acceptance

- Superseding user correction on 2026-11-07: Dark Chain has no stun at any rank. See `projectile-magnetism-reaction-fix-task-card.md` for the correction and the Main Normal Attack blocker.
- Kit Definition → Potential Nodes → Skill Lv4 exposes Dark Chain summon template, duration, interval, strike lifetime, reference point, ATK and Main Max HP ratios. Default reference point is Ground Projection.
- Passive Lv5 exposes Ultimate Finisher Damage, using Main's Max HP at 20%, Darkness element. Retain final-melee-hit and rank restrictions.
- Preserve existing user edits, damage feedback, targeting, pool reset and action entry/cancel/resource behavior. No architecture, dependency, CI or publishing changes.
- Verify compilation and focused Memory, projectile and finisher regressions. Human playtest covers stun readability, elevated targets, swaps and repeated casts.

## Experience checks

Clarity: descriptions and Inspector expose the current combat values. Motivation: Memory unlocks remain authoritative. Response: input/cancel windows remain the existing action's responsibility. Satisfaction/Fit: retain Dark Chain visuals/audio and ground reference. The later user correction removes stun; enemy reaction ownership remains in the existing system.

## Verification

Implementation complete. Unity compiled without errors; `Tools/Project/Combat/Validate Main Memory Fixes` passed 67 tests with zero failures. Results: `Temp/main-memory-fixes-tests.xml`. Existing obsolete Artifact warnings remain. See `../TestPlans/main-memory-fixes-regression.md` for the remaining human combat feel/readability checks. No merge or publishing performed.
