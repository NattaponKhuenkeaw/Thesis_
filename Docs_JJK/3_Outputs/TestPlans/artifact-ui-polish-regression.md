---
slug: artifact-ui-polish
status: draft
source: manual
gdd_tags: [ui, items, progression]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Artifact UI regression

## Automated coverage
- `ArtifactUiPolishTests`: independent rarity per card, unique definitions within a pull, seeded definition/stat repeatability, early-biome rarity restrictions, both rarity sort directions, selection switching, failed claim retry, repeated modal dismissal, Passive window mirroring/restoration, rich-text parsing and stat bounds.
- `ArtifactInstanceSystemTests`: existing artifact generation, inventory, reward and discard coverage; confirmation now exercises two clicks on the same card.
- `PotentialScreenTests`: existing upgrade cost, unlock and preview behavior.
- `ArtifactUiPolishPlayTests`: three actual context-menu/discard cycles, navigation Settings, and card raise/commit/pickup/exit at gameplay timeScale zero.
- `TeamBuilderInventoryValidation`: real prefab equipment flow, inventory remains open, category/reset behavior and 16:9/4:3 captures.

## Human visual/game-feel checkpoint
Reward details scrolling: use a very long passive and four rolled lines. Wheel or drag within the details and use its scrollbar to reach all stats; header/icon stay fixed and clipped content never escapes the card. Scrolling must not select or claim the card. Confirm by clicking twice outside a drag. Next reward draw starts at the top. On controller, move left/right among cards, Down to details scrollbar, and Up back to the card.
0. Slot/ownership follow-up (2026-11-07): click the same slot repeatedly and alternate filled/empty slots while the inventory is scrolled and filtered. The grid and scroll stay steady; the brighter pulsing border moves to the destination. Filled slots preview their own copy. Equip, transfer to another character, and unequip: the top-right portrait follows the owner and disappears on unequip, rarity stays visible, and Favourite remains readable. Repeat while gameplay time is paused; the border must still pulse. Starting pulse: 1.2 seconds / 6% expansion; reduce expansion if neighboring slots overlap.
1. New player/readability: open artifact inventory from a slot. The destination should have a visible overlay, and no numbered slot header. Click another destination and verify the overlay moves. Double-click a card or press Equip; inventory and the selected destination remain visible. The same artifact equipped again toggles it off.
2. Stats/rich text: author `<b>Attack</b> <color=#FFE066>bonus</color>` in an artifact passive description and `<i>Lore</i>` in its description. Compare preview and draw cards in Play Mode. Check four long stat names and large values at 1920x1080 and 1024x768; no value should escape its row/panel.
3. Modal stress: repeatedly discard unequipped copies, cancel with button/outside click/Escape, then reopen. Try rapid clicks and an empty inventory. No invisible window should block subsequent input. Check the Skip confirmation preference, then restore its original value. Equipped and draft-assigned copies must remain protected.
4. Sorting/favorites: compare R/SR/SSR order under Rarity. The toolbar Favorite button is absent. Right-click Favorite/Unfavorite still changes only that copy and its badge.
5. Potential: choose several Passive nodes, then Skill and Ultimate. Details belong on the left for Passive and return to the original right side for the other tracks. Character switching and repeated upgrades preserve placement.
6. Settings: open using the navigation button in a safe room and Training. Close/back/resume and open again; volume/fullscreen controls retain their existing behavior, and pause/input locks are released correctly.
7. Selection/mastery: in a reward draw, compare actual stats and rarity-colored backgrounds. First click raises a card and shows the second-selection hint. Select a different card freely. Press the selected card again to claim and see the pickup and panel exit. Use controller focus/Submit as well as mouse.
8. Abuse/stress: spam confirm, pause during choosing, finish multiple pulls and reopen another reward. Award exactly one displayed copy per pull. No skipped draw, duplicate award or blocked panel should result.

Starting motion values are 28 canvas units / 0.12 seconds for selection and 100 units / 0.2 seconds for pickup. Pass: selection is obvious, no overlap with header, pickup is readable at timeScale zero. Reduce lift if it overlaps; increase pickup duration if observers miss it. Final feel approval remains with the human reviewer.
