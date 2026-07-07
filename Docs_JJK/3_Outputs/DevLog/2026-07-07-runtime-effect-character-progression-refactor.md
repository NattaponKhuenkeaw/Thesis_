# DevLog: 2026-07-07 - Runtime Effect / Character Progression Refactor

## Goal
Split runtime-effect formulas from character-authored application ownership so kits can reuse fewer formula definitions with per-kit values, duration, target, stacking, and display policy.

## What I Did
- [x] Added task card, impact analysis, and ADR-002 for runtime-effect recipes.
- [x] Added `RuntimeEffectRecipe`, `RuntimeEffectRecipeData`, policy, presentation, and parameter payloads.
- [x] Made `RuntimeEffectInstance` carry non-serialized recipe context for live execution.
- [x] Added recipe-aware application paths to `PlayerRunInventory` and `RuntimeEffectReceiver`.
- [x] Updated kit-triggered and projectile-area runtime effects to prefer recipes and fall back to legacy direct definitions.
- [x] Updated formula definitions to read recipe parameter overrides before legacy SO fields.
- [x] Updated runtime-effect UI/debug display to use recipe presentation and duration metadata.

## Key Decisions
- Kept legacy definition fields as fallback so existing character assets do not need a risky all-at-once YAML migration.
- Kept conditions in kit trigger/filter data instead of moving them into runtime-effect formulas.
- Made child recipe payloads non-recursive to avoid Unity serialization depth warnings while still supporting one visible container effect with multiple formula payloads.
- Marked live recipe references as non-serialized so run-state assets do not try to save transient recipe graphs.

## Bugs Found
- [x] Unity hot reload reported serialization depth warnings for recursive `RuntimeEffectRecipe.childRecipes`; fixed by splitting top-level recipe from non-recursive child payload data.

## Game Feel Notes
Not playtested in-editor. Human owner will run E2E validation.

## Next Session
- Migrate Rugal/Kyo/Goenitz assets from legacy one-off effect SOs into shared formulas plus kit-owned recipes.
- Add focused editor tests for recipe override values, remove-on-damaged policy, and recipe-key stack merging.
- Consider custom inspectors to keep recipe authoring compact.

---

## Git Commit Summary
```
refactor(combat): split runtime effect recipes from formula definitions

- add recipe-owned policy, presentation, and parameter payloads
- route run inventory, receiver, kit triggers, and area effects through recipes
- preserve legacy definition fallback for existing authored assets
```
