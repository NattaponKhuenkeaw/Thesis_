---
slug: character-inspector-folder-layout
status: approved
gdd_tags: [ui, progression]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Character Inspector folder layout validation

## Automated Editor validation
Run `Tools/Neo-JJK/UI/Validate Character Inspector Folder Layout`. It uses isolated prefab contents and a temporary run inventory. Coverage: authored decorative asset sources, real identity portrait/name, all ten stats and shared run level, twelve Potential segments, selected rail styling, stable rail objects when changing characters, Info/Skills navigation, selected-page preservation, real skill entries, equipment action binding, Potential entry/return, close and empty state, and drawer translation/opacity without content scaling.

Previews: Info at 16:9 and 4:3, Skills, alternate character Info, Equipment, and empty inventory.

Run `Tools/Neo-JJK/UI/Validate Character Inspector Regression` for RunGlobalLevelTests, CameraGirlPotentialTests and ArtifactInstanceSystemTests. The existing isolated-scene executor preserves the user's open scene. Run `Validate TeamBuilder Inventory Layout` to check the previously completed inventory layout after TeamCanvas changes.

## Human visual/timing review
1. In a Safe Zone, open Inspector through the usual icon and character inspection entry. Confirm the real level, stats and role.
2. Switch Info/Skills quickly; switch characters while a drawer transition is active. Text remains steady in scale; the final selected page reaches full opacity and the original position.
3. Scroll long Skills/Equipment content and the portrait rail with a roster taller than the viewport. Roster objects and scroll position should survive selection changes.
4. Open Equipment from Info and return. Open Upgrade Potential, inspect/upgrade eligible nodes using the existing confirmation flow, and return. Counts/segments and skills should update from MetaProgression.
5. Close through X, Back and Escape; reopen. No stuck input blockers or old selection highlights.
6. Enter Combat Zone; the Inspector must close and remain inaccessible as before.
7. Check 16:9, 4:3 and window resizing for overlapping labels, unreadable stats, stretched portraits or clipped descriptions.

Automated Edit Mode pose checks verify animation properties; Play Mode timing feel remains a human review item.
