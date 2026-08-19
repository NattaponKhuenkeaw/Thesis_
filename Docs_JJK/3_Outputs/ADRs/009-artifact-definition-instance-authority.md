---
slug: "artifact-definition-instance-authority"
status: approved
source: manual
gdd_tags:
  - "progression"
  - "items"
  - "ui"
owner: "architect-agent"
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# ADR-009: Artifact Definition and Instance Authority

| Field | Value |
| --- | --- |
| Status | **Accepted** |
| Date | 2026-08-17 |
| Author | Codex |
| GDD Section | `@tag:progression`, `@tag:items`, `@tag:ui` |

## Context

The current Artifact ScriptableObject simultaneously owns reusable content, fixed stats, inventory identity, equipment identity, and duplicate conversion. Reference equality and definition-ID saves cannot represent multiple copies with independent randomized rolls or Favourite state.

## Decision

- Introduce `ArtifactDefinition` as the reusable content authority. Keep the existing script/meta GUID on an obsolete `Artifact : ArtifactDefinition` compatibility asset so all nine authored assets and serialized references remain valid during v2 migration; remove the adapter only after Unity asset validation.
- Represent each acquisition as a serializable `ArtifactInstance` with a stable instance ID, definition ID, quantized stat rolls, Favourite/new state, acquisition order, and generation seed.
- Make `PlayerRunInventory` the persistent Artifact-instance authority and use instance IDs for draft, equipped, save, sell, and UI identity.
- Make `TeamLoader` resolve immutable `ArtifactRuntimeSnapshot` records on Deploy, extending ADR-005's deployed-stat authority.
- Centralize roll generation in an injected-RNG generator, roll application in `ArtifactStatAggregator`, and sorting/filtering in a pure query engine.
- Derive numeric roll quality from the roll catalog. Do not store or expose Artifact rarity.
- Migrate run saves from v2 definition IDs to v3 instance records, retaining v2 as rollback input until validation.
- Remove Artifact Transcendence fragments while preserving Character Potential state.
- Preserve all current Artifact data as imported instance rolls without rerolling; imported out-of-range values have unavailable quality.
- Keep damage math decimal internally, then return one final integer `DamageResult` consumed by HP, feedback, notifications, and Training Debug.

## Alternatives Considered

| Option | Pros | Cons |
| --- | --- | --- |
| Clone ScriptableObjects at runtime | Minimal caller changes | Unity object lifecycle, save identity, and asset/runtime separation remain unsafe. |
| Store roll lists on definitions | Simple authoring | Every copy still shares stats; duplicates remain impossible. |
| Store instances only in Team slots | Small inventory change | Unequipped ownership, Favourite state, filtering, and selling have no authority. |
| Recompute deployed Artifacts from inventory on each hit | Always current | Violates Deploy semantics and ADR-005; adds hot-path work. |
| Reset legacy Artifact data | Simplest migration | Destructive and rejected by the approved design. |

## Consequences

### Positive

- Same-definition Artifact copies remain independent across acquisition, save/load, Favourite, equip, deployment, and selling.
- Stat generation and quality are deterministic and unit-testable.
- UI queries cannot mutate persistent state.
- Combat reads immutable deployed snapshots and one shared stat aggregator.
- Existing builds and Character Potential progression survive migration.

### Negative / Trade-offs

- Every Artifact reference path must migrate from Unity object identity to instance identity.
- Save schema, team drafts, Shop selling, passives, UI cards, and tests all change together.
- Large duplicate inventories require pooled grid presentation and cached indices.
- Imported legacy values may display no roll quality.

### Migration

- Add new contracts and dual-read adapters first.
- Dry-run asset and v2-save migration before writing.
- Preserve the existing Artifact script/meta GUID during rename.
- Write and read back v3 before considering migration successful; keep v2 untouched for rollback.
- Remove fixed `bonusStats`, definition-ID slots, Artifact fragment APIs, and adapters only after asset/save/prefab regression validation.

## Related

- ADR-005: Deployed Character Live-Stat Authority
- ADR-008: Orthogonal Damage Delivery and Hit-Owned Type
- `artifact-random-roll-inventory-design-spec.md`
- `artifact-random-roll-inventory-arch-plan.md`
- `artifact-random-roll-inventory-impact-analysis.md`
