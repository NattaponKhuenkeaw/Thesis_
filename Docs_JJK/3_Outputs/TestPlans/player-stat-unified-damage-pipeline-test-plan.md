---
slug: "player-stat-unified-damage-pipeline"
status: approved
source: manual
gdd_tags:
  - "combat"
  - "characters"
  - "items"
owner: "qa-agent"
human_checkpoint: required
next_agent: code-review-agent
blocked_by: []
---

# Test Plan: Player Stat and Unified Damage Pipeline

- Unit-test all five delivery/type matrix rows and critical overrides.
- Verify DI, DM, and ADI multiply independently and never leak to ineligible events.
- Verify DEF/general/type mitigation applies exactly once and in the approved order.
- Verify DOT snapshot, replace/refresh/ignore, tick cadence, and terminal behavior.
- Compare melee, projectile, background, passive, runtime-effect, and dash producers.
- Validate stat/AER asset migration and all serialized prefab references.
- Inspect the ten player-facing labels; SPD, EM, Impact, and character DR must be absent.
- Run EditMode tests, combat smoke tests, console checks, and per-hit allocation profiling.

