# Architecture Decision Records Index

> Last updated: 2026-07-07

## Dependency Order

```mermaid
graph TD
    ADR001["ADR-001: Aura Energy Resource-Gated Ultimate System"]
    ADR002["ADR-002: Runtime Effect Recipes"]
    ADR001 --> ADR002
```

_Update this diagram as ADRs are added. Show which decisions depend on others._

## ADR Registry

| # | Title | Status | Phase | Current Note |
|---|-------|--------|-------|-------------|
| [001](001-aura-energy-resource-gated-ultimate.md) | Aura Energy Resource-Gated Ultimate System | **Accepted** | Phase 1 | Refactored Ultimate cooldowns to Aura Energy |
| [002](002-runtime-effect-recipes.md) | Runtime Effect Recipes | **Accepted** | Phase 2 | Splits reusable formulas from kit-owned application recipes |

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
