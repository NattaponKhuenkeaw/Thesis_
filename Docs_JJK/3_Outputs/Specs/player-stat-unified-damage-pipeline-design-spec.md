---
slug: "player-stat-unified-damage-pipeline"
status: approved
source: manual
gdd_tags:
  - "combat"
  - "characters"
  - "items"
owner: "game-design-agent"
human_checkpoint: required
next_agent: architect-agent
blocked_by: []
---

# Design Spec: Player Stat and Unified Damage Pipeline

## Player Goal

The player should recognize three offensive build paths: DI strengthens direct
Primary damage, DM strengthens every non-Physical event, and ADI strengthens
separate Artifact/Particle/Terrain-style damage.

## Rules

| Event | Scaling | Default crit | Mitigation |
| --- | --- | --- | --- |
| Physical Primary | DI | Yes | DEF, general reduction, Physical reduction |
| Non-Physical Primary | DI x DM | Yes | DEF, general reduction, matching reduction |
| DOT | DM | No | General and matching reduction |
| Physical Additional | ADI | No | Physical reduction |
| Non-Physical Additional | ADI x DM | No | Matching reduction |

- Eligible mastery multipliers combine multiplicatively.
- Non-Physical Primary and Additional damage may apply DOT after confirmed damage.
- Additional damage cannot proc Additional damage or grant AE. DOT is terminal.
- DOT snapshots source stats. Stronger replaces, equal refreshes, weaker is ignored.
- CM, CR, CD, DM, DI, ADI, and AER are authored as percentage points.

## Five-Component Check

- **Clarity:** one visible stat maps to one delivery/type rule.
- **Motivation:** three artifact paths support direct, elemental/DOT, and proc builds.
- **Response:** action timing and player control are unchanged.
- **Satisfaction:** existing hit feedback remains delivery-aware.
- **Fit:** the rules favor readable, data-driven hack-and-slash builds.

## Risks

- Impact-to-DI broadens old Ultimate-only bonuses to every Primary hit.
- Removing character Damage Reduction changes affected defensive content.
- Secondary recursion must be prevented by delivery policy, not caller convention.

