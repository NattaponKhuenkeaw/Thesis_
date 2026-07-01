# Shop Floor Item List Test Plan

## Scope

- Scene: `Assets/Project/Scenes/Test_Shop.unity`
- Feature task: `Docs_JJK/3_Outputs/Specs/shop-floor-item-list-task-card.md`
- Runtime source of truth: `ShopManager`

## Edit Mode

- `InventoryAcquisitionResolverTests.ConsumableAcquisitionAddsRequestedQuantity`
  - Confirms quantity purchases add the requested stack amount to `PlayerRunInventory`.

## Manual Play Mode

1. Enter `Test_Shop`.
2. Confirm the old `E` shop panel opens sell-only inventory from `PlayerRunInventory`.
3. Walk into each floor item trigger.
4. Confirm the item card enters open state and `ENTER` purchases the item once.
5. Confirm consumable offers add their configured stack quantity.
6. Confirm purchased floor cards enter sold state and cannot be bought again.
7. Press `R` at the reroll prompt.
8. Confirm floor offers refresh, sold state resets, each card triggers `IsClose`, and no currency is spent.
9. Confirm every listed item price increases by 15% per reroll stack.

## Notes

- The cards use `IsOpen`, `IsClose`, and `IsSold` animator triggers.
- The reroll prompt is intentionally separate from the sell-only overlay.
