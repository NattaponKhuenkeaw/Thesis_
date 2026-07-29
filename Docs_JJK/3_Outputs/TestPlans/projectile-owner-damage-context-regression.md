---
slug: projectile-owner-damage-context
status: superseded
source: manual
gdd_tags:
  - combat
owner: qa-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Projectile Owner Damage Context Regression

> Superseded by `deployed-character-live-stats-e2e.md`. The earlier unit-style
> coverage was removed in favor of the user-approved single E2E scenario.

The owner-identity requirement remains valid, but a source that is still
deployed now reads its live off-field effective stats.

## PlayMode

1. Give character A substantially lower ATK than character B.
2. Fire a slow or delayed projectile with A and switch to B before impact.
3. Confirm Training Debug reports A as owner and damage matches A.
4. Repeat with A higher than B and confirm damage still matches A.
5. Repeat without switching; apply/remove an owner buff before impact and
   confirm the live refresh still follows A.
6. Confirm no passive or action modifier belonging only to B affects A's
   existing projectile.
