---
slug: shop-character-death-rest
status: needs-human
gdd_tags: [core-loop, characters, progression]
owner: agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Character purchases, death, and Rest

Implemented the user's requested rules within the existing inventory/team/stat authorities. Individual defeat now retains the CharacterItem and RunCharacterState, sets a serialized death marker, zeros HP/AE, clears transient effects, and releases the defeated team's slot/equipment using existing compaction. Generic additions and heals cannot clear death. Terminal run reset still performs the previous ownership cleanup explicitly.

Rest heals living deployed teammates by its existing authored percentage and revives every owned dead character at 30% current Max HP. Revived characters remain benched until assigned. Death is visible on inventory cards; character preview explains Rest and disables Assign; assignment and CharSlot entry points also reject unavailable characters.

The shop excludes owned character identities regardless of the legacy filter or permanent fragment progression. Stale duplicate offers cannot spend currency or create fragments. Character lists switch their effective runtime type to Artifact when the shop-eligible character pool is owned; authored list types remain intact for later runs. Both floor-offer and direct purchase paths refresh remaining owned-character offers. Fallback uses normal Artifact rolls, prices and reroll increases.

Checkpoint JSON retains the death marker with a backward-compatible zero-HP fallback. The GDD progression section, task card, impact analysis and smoke plan record the new rules. No scenes, prefabs, dependencies, build settings, or camera work were changed by this task.

## Validation

All **68/68 Unity EditMode tests passed** in the isolated project copy, with graphics enabled for UI render/raycast checks. Fixtures: DeathResetOwnershipTests, ShopCharacterAvailabilityTests, ShopCharacterReacquisitionTests, ShopPurchaseAudioTests, and TeamSelectionLifecycleTests. Coverage includes Main/ally revival at exactly 30% (including a full-heal authored setting), living heal/cap, full wipe, current and legacy checkpoint restoration, identity aliases, floor/direct purchases, stale offers, Artifact fallback/reroll prices, and dead-card assignment controls.

The existing UI fixture leaked runtime pause holds between EditMode tests because destruction did not invoke its runtime disable cleanup. Teardown now calls that cleanup explicitly; the complete suite passes. The scoped diff check also passed. Results: `E:/CodexTemp/Thesis-room-camera-20261110-1c46f2de/shop-death-rest-verified.xml`.

Manual gameplay/visual review remains in `../TestPlans/shop-character-death-rest-test-plan.md`, especially death/survivor switching, Rest presentation and re-equipping revived characters.
