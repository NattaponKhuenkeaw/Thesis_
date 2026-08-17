---
slug: "artifact-click-equip-interaction"
status: implementation-validation
source: manual
gdd_tags:
  - "ui"
  - "items"
owner: "qa-agent"
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Artifact Click-to-Equip Test Plan

## Automated

- [x] Inventory Artifact card is clickable and implements no begin/drag/end-drag interfaces.
- [x] Equipped Artifact slot remains clickable, draggable, and a drop target.
- [x] Unity runtime/editor assemblies compile without errors.
- [x] All EditMode tests pass.

Validation evidence: Unity 6000.3.4f1 isolated EditMode run passed **51/51**, with zero failures or skipped tests. Result: `C:/Users/Apricha/AppData/Local/Temp/Thesis_ArtifactV3_UnityValidation/artifact-click-equip-results-final2.xml`.

## Interactive

- [ ] Left-click each of the three Artifact destinations and verify only the chosen slot highlights.
- [ ] Select an Artifact; verify equip, stat refresh, New-state update, and grid closure.
- [ ] Search/sort/filter before selection; verify the destination persists.
- [ ] Enter Favourite Mode; verify card clicks only toggle Favourite, then exit and equip.
- [ ] Open the generic Artifact grid with no destination; verify card click previews.
- [ ] Close the grid, switch to Character inventory, and enter Combat; verify pending destination clears.
- [ ] Begin an equipped-slot drag while a destination is selected; verify the pending highlight clears before the drag resolves.
- [ ] Drag equipped Artifacts between empty/filled slots and characters; verify swaps remain correct.
- [ ] Drop an equipped Artifact outside valid slots and right-click one; verify both remove as before.
