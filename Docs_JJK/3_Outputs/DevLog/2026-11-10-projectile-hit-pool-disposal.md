---
slug: projectile-hit-pool-disposal
status: implemented
gdd_tags:
  - combat
owner: implementation-agent
human_checkpoint: not-required
next_agent: qa-agent
blocked_by: []
---

# Projectile Hit Pool Disposal

## Bugs Found

- The shared damage processor disposed a default Unity pool handle when it
  skipped capturing pre-hit target effects. This threw after damage was applied
  and interrupted projectile hit registration, leaving the hit limit and
  cooldown bookkeeping incomplete.
- Use an optional pool handle so a missing snapshot needs no disposal. A rented
  snapshot still returns to the pool when the damage call exits.

## Verification

- Reproduced the exception using the installed Unity engine assembly. Verified
  the fix with absent, empty, and populated effect lists and confirmed the
  rented snapshot was returned on exceptional exit.
- Runtime and editor assemblies compiled with Unity's compiler. The Play Mode
  scenarios are in the combat reward and damage pipeline test plan.
