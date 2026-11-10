---
slug: shop-character-death-rest
status: needs-human
gdd_tags: [core-loop, characters, progression]
owner: agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Character ownership / death / Rest regression

## Automated coverage
Result: **68/68 passed** in an isolated Unity batch editor with graphics enabled. Scoped diff check passed. Manual steps below remain pending.

Run `DeathResetOwnershipTests`, `ShopCharacterAvailabilityTests`, `ShopCharacterReacquisitionTests`, `ShopPurchaseAudioTests`, and `TeamSelectionLifecycleTests` in Unity EditMode. These cover retained state/ownership, blocked ordinary healing and repurchasing, Main/ally revival, living healing/capping, full-wipe guards, checkpoint/legacy restoration, catalog exhaustion and reroll pricing, duplicate identity rejection, and dead-card assignment controls.

## Human smoke test
1. Start a run with Main and CameraGirl. Kill either one while keeping the other alive. The death animation and survivor switch still play. The defeated character remains owned at zero HP/AE, their CharSlot is cleared, and their artifacts are available in inventory.
2. In the next SafeZone, inspect the dead character card. It says Death, is dimmed, and offers a Rest revival explanation. Assign is disabled. Normal switch inputs and direct assignment cannot select them.
3. Inspect Character shop offers with the dead character owned. They never reappear. With all shop-eligible characters owned, a Character list offers a normally priced Artifact with the Artifact tag, icon and quantity. Rerolls preserve Artifact behavior and existing price increases.
4. Enter Rest with a damaged living teammate and an owned dead teammate. Claim once. The living deployed teammate gains the authored 30% Max HP, capped at maximum. The dead character revives at exactly 30% of current unequipped Max HP, remains benched, and can now be assigned in a CharSlot. The readout reports revival and existing Rest audio plays.
5. Interact repeatedly with the same Rest. It remains claimed. Repeat death and Rest in a later room; ensure no ownership duplication or free fragment conversion.
6. Wipe the full deployed team. Existing run-loss/Lobby behavior still occurs; a Rest or stale shop purchase cannot bypass teardown.

Observer/readability check: without explaining the rules, ask a player to identify why the dead character cannot deploy and where to revive them. Fail if the dead card looks assignable or revival is confused with purchasing. Review visual fit of the new Rest result and Death label in the actual game view.
