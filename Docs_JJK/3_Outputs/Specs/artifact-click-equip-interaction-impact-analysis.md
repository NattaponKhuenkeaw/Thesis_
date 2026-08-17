---
slug: "artifact-click-equip-interaction"
status: approved
source: manual
gdd_tags:
  - "ui"
  - "items"
owner: "architect-agent"
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# Artifact Click-to-Equip Impact Analysis

## Responsibility Change

| Class | Before | After |
| --- | --- | --- |
| `ArtifactGridItemUI` | Click preview/Favourite and inventory drag source | Click-only selection source |
| `ArtifactSlotUI` | Equipped drag/drop target and right-click removal | Same, plus left-click destination selection and selected-state visual |
| `TeamBuilderUI` | Character destination state; Artifact preview routing | Also owns one transient Artifact destination and routes card clicks to equip or preview |
| `TeamSlotUI` | Artifact equip/swap/removal mutation | Unchanged mutation authority; left-click delegates destination selection |

## Blast Radius

- No serialized fields are removed or renamed.
- No prefab rewiring, save migration, inventory authority, draft format, or `TeamLoader` deployment change.
- Primary regression risk is stale destination state after panel/category/zone transitions.
- Secondary regression risk is accidentally removing drag interfaces from equipped `ArtifactSlotUI` instead of inventory `ArtifactGridItemUI`.

## ADR Assessment

ADR-009 remains authoritative. The change only reroutes UI input into the existing instance-aware draft mutation and does not require a new ADR.

