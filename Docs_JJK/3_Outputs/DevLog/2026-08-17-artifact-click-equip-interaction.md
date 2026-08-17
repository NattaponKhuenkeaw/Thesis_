# 2026-08-17 - Artifact Click-to-Equip Interaction

## Goal

Make Artifact inventory assignment match the Character destination-first flow without removing equipped-slot drag, swap, or removal behavior.

## Changes

- Removed drag-source interfaces and drag-ghost ownership from `ArtifactGridItemUI`.
- Added left-click destination selection and persistent highlight to `ArtifactSlotUI`.
- Added transient Artifact destination routing to `TeamBuilderUI`; normal card clicks equip when a destination exists and preview otherwise.
- Preserved Favourite Mode precedence, equipped-slot drag/drop swaps, outside-drop removal, and right-click removal.
- Clear pending destinations on close, category change, panel disable, combat entry, successful equip, and equipped-slot drag start.
- Updated the GDD, design spec, impact analysis, task card, and regression plan.

## Verification

- Unity 6000.3.4f1 compilation: pass, zero errors.
- Isolated EditMode tests: **51/51 passed**.
- Interface regression test confirms inventory cards are click-only and equipped slots retain click/drag/drop contracts.
- Interactive Team Builder pointer-flow and visual-highlight checks remain for the human E2E checkpoint.

## Architecture

ADR-009 remains unchanged. Artifact instance identity, `PlayerRunInventory`, draft mutation, save data, and `TeamLoader` deployment authority were not modified.

## Git Commit Summary

```text
feat(ui): select Artifact destination before equip

- make Artifact inventory cards click-only
- preserve equipped-slot drag, swap, and removal
- cancel pending destinations across panel and drag transitions
```
