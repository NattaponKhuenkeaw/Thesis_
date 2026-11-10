---
slug: shop-character-death-rest
status: approved
gdd_tags: [core-loop, characters, progression]
owner: agent
human_checkpoint: not-required
next_agent: implementation-agent
blocked_by: []
---

# Impact analysis

Goal: retain ownership through individual death, revive at Rest, and exhaust Character shop offers into Artifact offers using existing authorities.

| Class | Before | After |
| --- | --- | --- |
| CharacterInventoryState | Defeat deletes ownership/runtime record; shop can reacquire | Defeat marks retained state dead; explicit revive clears marker; full run-loss cleanup stays explicit |
| TeamLoader | Compacts surviving team, heals deployed characters | Same defeat/switch sequence; Rest also revives dead owned roster entries |
| ShopManager | Owned characters can become fragments; defeated characters repurchased | All owned character identities excluded and duplicate purchases rejected; exhausted character lists roll Artifacts |
| ShopItemListUI | Authored item type controls population | Authored type retained; effective runtime type can become Artifact |
| CharacterGridItemUI / TeamBuilderUI | Ownership implied availability | Dead roster remains inspectable, labeled Death, with assignment blocked |

Save compatibility: additive death field defaults false for old saves; restore also recognizes zero HP. No schema/key/config changes. Inventory remains state authority; TeamLoader remains live-stat/presentation authority. Existing team compaction releases defeated character equipment. Rest revives the resulting benched character with their current unequipped Max HP; deployment recalculates equipment normally.

Risks: default Main bootstrap, full-wipe guards, duplicate identities, stale offers, and checkpoint restoration. Cover these in existing tests. No new ADR is needed because class responsibilities and architectural authorities remain unchanged. Human approval for the requested rules is the source prompt; visual feel and merging remain review checkpoints.
