---
slug: shop-reroll-prompt-compile-error
status: approved
source: manual
gdd_tags:
  - progression
  - ui
owner: implementation-agent
human_checkpoint: not-required
next_agent: human
blocked_by: []
---

# Task Card: Shop Reroll Prompt Compile Error

## Reported Behavior

Unity compilation fails because `InteractableWorldUI` reads `ShopManager.CurrentRerollCost`, which does not exist.

## Intended Behavior

The world interaction prompt describes the reroll action without displaying a reroll cost. Rerolls are free, while each reroll increases floor offer prices by the configured percentage.

## GDD Reference

- `@tag:progression` — shop rerolls refresh offers and increase listed item prices.

## Scope

- `Assets/Project/Scripts/UI/InteractableWorldUI.cs`
- `Assets/Project/Scripts/Editor/Tests/ShopEconomyBalanceTests.cs`
- Shop reroll prompt and economy regression coverage.

## Acceptance Criteria

- The UI no longer references the missing `CurrentRerollCost` member.
- The reroll prompt displays its authored description without an incorrect currency cost.
- Economy tests cover free rerolls and the offer price markup.
- Shop reroll behavior and offer price markup remain unchanged.
