---
slug: training-shop-characters-statless-artifacts
status: approved
source: manual
gdd_tags: [scene-roles, characters, items]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Training shop roster and artifact stats

Source: Apricha's 2026-11-02 request and clarification that artifact stats are disabled while artifacts remain available. This request updates the older Training loadout rule that exposed all characters. Workflow: `/implement-feature`.

## Player-facing behavior

Training's available character roster contains only valid catalog CharacterItems whose `availableInShop` flag is enabled, matching shop eligibility. Artifacts remain available and equippable in Training, but training's generated instances have no stat rolls, so their stats are absent in both the artifact UI and combat. Artifact passives and normal gameplay artifact instances retain existing behavior.

## Implementation

Filter character catalog seeding at the existing `TrainingInventoryState.PopulateCatalog` boundary. Clear stat rolls from each newly generated Training artifact instance before the final catalog-change notification, keeping its definition, instance and passive intact. The Main catalog item remains available and continues to seed the default practice team. No changes to saved inventory or permanent unlocks.

## Verification

Inspect catalog flags and implementation. Compile scripts; do not add or run script tests per Apricha's earlier explicit instruction. Manual follow-up: Training list offers only shop-enabled characters; artifact UI displays no stat rolls for Training instances; equip one and confirm its passive still works; normal gameplay artifact stats apply.

Human review of Training/gameplay feel remains required. No merge or publish.
