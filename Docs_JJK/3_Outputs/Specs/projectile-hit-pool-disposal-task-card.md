---
slug: projectile-hit-pool-disposal
status: approved
source: manual
workflow: /fix-bug
gdd_tags:
  - combat
owner: implementation-agent
human_checkpoint: not-required
next_agent: qa-agent
blocked_by: []
---

# Projectile Hit Pool Disposal

## Report

Additional damage triggered by projectile hit effects or runtime on-hit recipes
throws in `PooledObject.Dispose`. The projectile can repeatedly damage an enemy
because hit processing exits before recording the hit and starting its cooldown.

## Scope and Acceptance

- Keep pre-hit target-effect snapshots available to ordinary on-hit effects.
- Resolve damage safely when a hit does not need a snapshot or the target has no active effects.
- Preserve authored projectile hit limits, intervals, and damage values.
- Check both reported routes and confirm that an effect added by a hit cannot
  satisfy a recipe that requires the effect to have been present beforehand.
