# Architecture Decision Records Index

> Last updated: 2026-10-27

## Dependency Order

```mermaid
graph TD
    ADR001["ADR-001: Aura Energy Resource-Gated Ultimate System"]
    ADR002["ADR-002: Runtime Effect Recipes"]
    ADR003["ADR-003: Deterministic State-Owned Player Movement"]
    ADR004["ADR-004: Target-Owned Runtime State Channels"]
    ADR005["ADR-005: Deployed Character Live-Stat Authority"]
    ADR006["ADR-006: Hit-Owned Damage Types and Enemy Reductions"]
    ADR007["ADR-007: Run-Global Character Level Authority"]
    ADR008["ADR-008: Unified Player Damage Delivery"]
    ADR009["ADR-009: Artifact Definition and Instance Authority"]
    ADR010["ADR-010: Artifact Passive Event and Secondary-Effect Authority"]
    ADR011["ADR-011: Boss Pattern Controller"]
    ADR012["ADR-012: EncounterSO Layout Spawn Slots"]
    ADR001 --> ADR002
    ADR002 --> ADR004
    ADR004 --> ADR005
    ADR005 --> ADR006
    ADR005 --> ADR007
    ADR006 --> ADR008
    ADR005 --> ADR009
    ADR008 --> ADR009
    ADR004 --> ADR010
    ADR005 --> ADR010
    ADR008 --> ADR010
    ADR009 --> ADR010
    ADR006 --> ADR011
    ADR011 --> ADR012
```

_Update this diagram as ADRs are added. Show which decisions depend on others._

Filenames for ADR-006/007/011/012 still use older numbers from the pre-merge branches. Index numbers above are canonical.

## ADR Registry

| # | Title | Status | Phase | Current Note |
|---|-------|--------|-------|-------------|
| [001](001-aura-energy-resource-gated-ultimate.md) | Aura Energy Resource-Gated Ultimate System | **Accepted** | Phase 1 | Refactored Ultimate cooldowns to Aura Energy |
| [002](002-runtime-effect-recipes.md) | Runtime Effect Recipes | **Accepted** | Phase 2 | Splits reusable formulas from kit-owned application recipes |
| [003](003-deterministic-player-state-owned-movement.md) | Deterministic State-Owned Player Movement | **Accepted** | Phase 1 | Implementation approved; awaiting human E2E feel approval |
| [004](004-target-owned-runtime-state-channels.md) | Target-Owned Runtime State Channels | **Accepted** | Phase 2 | Target/channel ownership for shields and structural Runtime Effect reconciliation |
| [005](005-deployed-character-live-stat-authority.md) | Deployed Character Live-Stat Authority | **Accepted** | Phase 2 | Runtime-only deployed records and explicit formula read policy |
| [006](008-hit-owned-damage-types-and-enemy-reductions.md) | Hit-Owned Damage Types and Enemy Reductions | **Accepted** | Phase 2 | Approved with standalone per-element DOT configuration |
| [007](009-run-global-character-level-authority.md) | Run-Global Character Level Authority | **Accepted** | Phase 2 | One run level feeds ADR-005 deployed stat records; individual EXP path is legacy |
| [008](008-unified-player-damage-delivery.md) | Orthogonal Damage Delivery and Hit-Owned Type | **Accepted** | Phase 2 | One Primary/DOT/Additional request pipeline with DI/DM/ADI scaling |
| [009](009-artifact-definition-instance-authority.md) | Artifact Definition and Instance Authority | **Accepted** | Phase 3 | Duplicate rolled instances, v3 persistence, Favourite/query flow, and deployed snapshots |
| [010](010-artifact-passive-event-and-secondary-effect-authority.md) | Artifact Passive Event and Secondary-Effect Authority | **Accepted** | Phase 3 | Per-instance passive runtime, typed events, generic DOT, Projectile-independent Particle, and Explosion Splash |
| [011](006-boss-pattern-controller.md) | Boss Pattern Controller | **Accepted** | Content / Boss | Bosses use pattern sequencer; Behavior Graph stays for trash enemies |
| [012](007-encounter-so-layout-spawn-slots.md) | EncounterSO + Layout Spawn Slots | **Accepted** | Phase 3 | Waves from EncounterSO; positions stay on layout slots |
| [013](013-action-owned-camera-presentation.md) | Action-Owned Camera Presentation | **Accepted** | Combat presentation | User-approved Dimension Slash action-frame camera envelope |

## Phase Mapping

| GDD Phase | ADRs | Current Status |
|-----------|------|----------------|
| **Phase 1** — Stabilize Prototype | ADR-{NNN} | {status} |
| **Phase 2** — Split Core Runtime | ADR-{NNN} | {status} |
| **Phase 3** — Data-Drive Content | ADR-{NNN} | {status} |

## How to Use

1. **Before making an architecture decision** → Check this index for existing decisions
2. **To create a new ADR** → Copy `_TEMPLATE.md` → Name it `{NNN}-{kebab-case-title}.md`
3. **After creating** → Add a row to the registry table above
4. **When superseding** → Update the old ADR's status to `Superseded by ADR-{NNN}`
