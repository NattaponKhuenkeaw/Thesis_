# Artifact rarity and room rewards validation

## Automated checks

Run `Tools/Project/Field Effects/Run Artifact Inventory Tests` in Edit Mode. Results are written to `Temp/ArtifactInventoryTests.xml`. This runs the existing instance suite plus the rarity and transaction regressions.

- Rarity profiles preserve canonical stat units, use deterministic seed results and limit the early roll profile.
- Fixed R stats and rolled stats use the same clean character base.
- Prepared copy commits preserve the selected rolls and reject duplicate IDs without spending Soul twice.
- A clearing callback from an earlier visit cannot unlock the next visit.
- The catalog loads ten R, eight SR and nine SSR definitions, with valid IDs and passive references.
- Hard route cards carry the correct total Soul and zero, one or two picks.
- The saved picker binds real rolled copies and grants only the selected copy once.
- Discard removes exactly the target and rejects a repeated stale target.
- Existing legacy import, equip, save and query cases remain in the suite.

Run `Tools/Project/Field Effects/Run Artifact Regression Tests` for the broader passive and VFX fixtures. The diagnostic fixture now reads the saved content, because its former setup regenerated production assets while running tests. The latest broader run before the final UI tests passed 77 of 87 checks. Nine failures came from the older diagnostic fixture's deployment setup, and one came from a Thunder tail assertion. The current implementation has not established a clean baseline for those ten checks.

## Visual checks

`Tools/Project/Field Effects/Capture Artifact Rarity UI` instantiates the saved prefabs in an isolated scene and renders them through the existing TeamBuilder capture helper. Outputs go to the ignored `Logs/TeamBuilderInventoryPreviews` folder.

Inspect the reward picker at 1920×1080 and 1024×768, the rarity tiles, the item menu and the discard dialog. Check that stars use sprites, the Favourite and New badges occupy separate corners, names wrap within the tile, passive descriptions fit, and the confirmation controls stay within the frame.

## Playtest script

1. Start a fresh run. Inspect early shop Artifact stock, buy an R and compare the preview and deployed stats. Reroll and check Soul spending and the tier mix. At later biome shops, verify SR availability and late SSR purchase availability.
2. Choose Hard routes until each reward pattern appears. Compare the route card with the clear payout. The two-pick pattern should grant zero room-clear Soul. Confirm one of three choices for each pull and check the exact rolls in inventory.
3. Test an authored count of three picks. Repeatedly click Confirm, interrupt the reveal, pause during selection, and attempt exit and TeamBuilder input during the reward. Check controller selection and confirmation.
4. Clear a multi-chamber room. Accessibility should open after the final chamber and final reward. Enter a shop, rest room, boss room and training scene, and verify their existing specialized flows.
5. Open a copy menu at every screen edge. Toggle Favourite while an equip destination is selected, then preview and equip normally. Test discard Cancel, checked confirmation, unchecked confirmation, a stale target, an equipped copy and a draft-assigned copy. Relaunch to check the opt-out preference.
6. Reload an existing boss-lobby checkpoint and compare the nine SSR definitions, instance IDs, rolls and equipped references. Ordinary run inventory still follows the existing cross-session reset policy.

The design spec contains the starting economy values and adjustment criteria. Record human pacing and game-feel results before treating those values as final balance.
