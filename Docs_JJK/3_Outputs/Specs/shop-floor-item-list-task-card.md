---
slug: shop-floor-item-list
status: approved
source: manual
gdd_tags:
  - progression
owner: codex
human_checkpoint: not-required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Shop Floor Item List

## Player-Facing Goal

The Test_Shop scene offers Blazblue Entropy Effect-style floor shop items: the player walks into an item zone, sees item details and price, buys once with Enter, and can reroll all offers for free with R while item prices gain a stacking markup.

## Source

- Origin: manual prompt
- Requested by: Apricha

## GDD Reference

- `@tag:progression` - Soul Shop item zones randomize characters, artifacts, and consumables; player can reroll shop items and increase listed item prices.

## Type

- [x] Feature
- [ ] Bug fix
- [x] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Shop
- Inventory
- UI

### Files To Inspect First

- `Assets/Project/Scripts/Shop/ShopManager.cs`
- `Assets/Project/Scripts/Shop/UI/ShopUI.cs`
- `Assets/Project/Scripts/Shop/UI/ShopGridItemUI.cs`
- `Assets/Project/Scripts/UI/InteractableWorldUI.cs`
- `Assets/Project/Scenes/Test_Shop.unity`

### Out of Scope

- Permanent duplicate upgrade spend UI.
- New shop art or animation clips beyond existing `ItemListPanel` animator triggers.
- Official team publishing or PR merge.

## Acceptance Criteria

- [x] Old overlay shop is sell-only and lists items from `PlayerRunInventory`.
- [x] `ShopManager` is the source of truth for floor offer population, purchases, rerolls, and state reset.
- [x] `ShopItemListUI` supports Consumable, Artifact, and Character rolls.
- [x] Enter buys the current floor offer only while the player is inside its trigger.
- [x] Purchased floor offers enter Sold state until reroll.
- [x] Consumable offers support bundle quantity purchases.
- [x] Reroll uses an `R` world interaction, is free, resets cards to Close state, and increases listed item prices by 15% per reroll.
- [x] Works on the primary platform input model.
- [x] Follows `RULES_AND_POLICY.md`.

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/implement-feature`

Next artifact:

- `Docs_JJK/3_Outputs/TestPlans/shop-floor-item-list-test-plan.md`
