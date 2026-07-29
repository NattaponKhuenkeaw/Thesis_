# 2026-07-29 — Projectile Owner Damage Context

> Updated by ADR-005 later on 2026-07-29: a switched-out source that remains
> deployed now uses its live off-field effective stats. The captured snapshot
> is only the fallback after that source leaves the deployed team.

Fixed delayed projectiles inheriting damage from a character selected after
the projectile was spawned.

- Live projectile stat refresh now requires the active character to match the
  projectile's recorded source character.
- After a team switch, the projectile keeps its owner identity.
- The rebound active-character passive controller is detached from that
  projectile's hit calculation.
- Superseded the earlier unit-style regression coverage with one integrated
  deployed-source E2E test.
