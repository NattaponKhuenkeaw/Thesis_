---
slug: artifact-ui-polish
status: needs-human
source: manual
gdd_tags: [ui, items, progression]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# DevLog: 2026-11-06 — Artifact UI polish

## Goal
Resolve the artifact inventory, reward-card, Potential detail placement and navigation Settings issues from the user's prompt.

## Changes
- Modal closing explicitly releases raycasts/interactivity and the grid's active-menu reference. Discard mutation runs after closing, so inventory rebuild events cannot interrupt modal cleanup.
- Artifact descriptions explicitly enable TMP rich text. Compact preview rows use parent-controlled width and separate bounded label/value columns.
- Rarity sorting is appended to the enum, preserving existing serialized sort values. The Favorite toolbar button is hidden; per-copy Favorite remains in the context menu.
- Equipment retains the selected destination and open inventory. A noninteractive overlay identifies the selected artifact slot; numbered context labels are removed.
- Passive Potential details mirror to the left and other tracks restore the authored placement.
- HUD navigation binds Settings to the existing pause/settings entry point; the authored button had no persistent click target.
- Reward cards use rarity-colored backgrounds and two-click selection, with a raised preview followed by pickup/exit and one guarded commit. The Claim button is removed from the prefab.
- Reward generation rolls an eligible rarity per card with existing biome weights and removes chosen definitions from that pull's pool. SSR policy is unchanged.

## Validation
Runtime and targeted editor sources compile with the project's Roslyn references. The original editor has stale `ShopEconomyBalanceTests` references to removed shop APIs; that unrelated fixture is excluded only in a temporary verification project. Source files in the user's project remain intact.

Unity verification used that independent copied project, preserving the user's open scene. All 56 targeted editor tests, the Play Mode regression, and the prefab layout assertions passed. The captures were inspected at 16:9 and 4:3. See `../Reports/ArtifactUiPolish/validation.md` for evidence and the remaining main-project compilation blocker.

## Game-feel checkpoint
Selection/pickup motion uses the starting values documented in the task card and regression plan. Final human review should confirm overlay strength, readability and card motion at 16:9 and 4:3. Existing reveal and confirmation audio are retained.

## Boundaries
Branch: `fix/artifact-ui-polish`. Preserved the pre-existing `InteractableWorldUI.cs` edit and shop compile-error task card. No merge, publishing, dependency or build configuration changes.
