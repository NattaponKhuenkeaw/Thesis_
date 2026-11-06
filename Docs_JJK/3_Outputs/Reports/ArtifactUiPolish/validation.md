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

# Artifact UI validation

Implemented on `fix/artifact-ui-polish`. Final approval of the overlay and reward-card motion remains a human visual/game-feel checkpoint.

## Results
- [Editor tests](editor-tests.xml): 56 passed, zero failed. Covers artifact generation/inventory, mixed-rarity pulls, seeded stat/definition repeatability, rarity sorting, two-click/retry/duplicate-claim protection, modal release, rich text, compact row bounds and Potential placement/upgrades.
- [Play Mode regression](play-tests.xml): passed. Three context-menu/discard cycles fully remove their dialogs; navigation opens Settings; a first reward-card click raises without awarding; a second click awards the exact displayed copy once and completes pickup/fade at gameplay timeScale zero.
- [Layout validation](layout-validation.txt): passes on the actual TeamCanvas prefab, including inventory remaining open after Equip, the noninteractive destination border, sorting-menu availability/bounds and existing character/category/drag-feedback checks.
- Source compilation succeeded for runtime and targeted editor sources. `git diff --check` passes.

## Verification environment and limits
Used Unity 6000.3.4f1 in an independent temporary project copied from the workspace, preserving the open user scene and original project settings/packages. The pre-existing `ShopEconomyBalanceTests.cs` fixture refers to removed `ShopManager.CurrentRerollCost` and `OnRerollCostChanged` APIs. Only that fixture was excluded from the temporary copy; it remains unchanged in the user's project and still blocks full editor compilation there.

An existing Potential icon expectation was updated to honor authored Potential artwork before falling back to kit icons. The inventory validator now checks previews are hidden by the closed inventory, rather than requiring their authored `activeSelf` values to be false.

The Favorite toolbar note was interpreted as a request to remove that button. Copy-level Favorite/Unfavorite remains available through the context menu. Reward rarity is rolled independently per card using existing biome R/SR weights; SSR eligibility and balance were not changed.

## Visual evidence
Reviewed the artifact inventory and draw at 16:9 and 4:3. Compact stat values stay in the preview and the selected slot border leaves the artifact/plus visible. The wider team-card layout still has its existing fixed-width clipping at 4:3; this work does not redesign that layout.

- [Artifact inventory, 16:9](artifact-selected.png)
- [Artifact inventory, 4:3](artifact-selected-4x3.png)
- [Reward palette, 16:9](artifact-rarity-picker.png)
- [Reward palette, 4:3](artifact-rarity-picker-4x3.png)

Reward palette captures deliberately display R, SR and SSR examples to verify styling; they are not evidence that SSR is eligible for Hard-route draws.

See the [regression plan](../../TestPlans/artifact-ui-polish-regression.md) for controller input, Skip confirmation and final feel review. No merge, team publishing, dependency or build configuration changes were made.
