# ADR-002: Runtime Effect Recipes

| Field       | Value                    |
|-------------|--------------------------|
| Status      | **Accepted**             |
| Date        | 2026-07-07               |
| Author      | Software Architect Agent |
| GDD Section | `@tag:combat`, `@tag:characters`, `@tag:progression` |

## Context

Runtime effects need to be reusable formulas, not character-owned one-off assets. The current model puts formula hooks, duration, target policy, stacking, display, and numeric tuning on the same `RuntimeEffectDefinition`, which encourages duplicated assets such as many separate "increase attack" effects.

## Decision

Introduce `RuntimeEffectRecipe` as the character/projectile/artifact-owned application layer. `RuntimeEffectDefinition` remains the formula type, while recipes own presentation, policy, and parameter overrides. Runtime systems resolve recipe fields first and legacy definition fields second so existing assets continue to work while new kit authoring can reuse fewer formula assets.

## Alternatives Considered

| Option | Pros | Cons |
|--------|------|------|
| Keep one SO per effect use | Simple and already works. | Explodes asset count and keeps ownership muddled. |
| Move all effects into passive hooks | Centralizes kit behavior. | Loses run-owned active effect state, HUD visibility, ticking, and stack handling. |
| Recipe layer with legacy fallback | Incremental, reusable, preserves current assets. | Requires a compatibility period with old fields still present. |

## Consequences

### Positive
- Character kits can reuse formula assets with per-kit values and policy.
- Conditions stay in kit trigger/filter data.
- Runtime containers become clearer as bundles/recipes instead of separate formula ownership.
- Existing assets do not require a risky all-at-once YAML migration.

### Negative / Trade-offs
- Inspectors will temporarily show both legacy definition defaults and recipe overrides.
- Runtime effect matching must include a recipe key to avoid merging different uses of the same formula incorrectly.

### Migration
- Add recipe/policy/presentation/parameter data classes.
- Add recipe-aware application overloads to `PlayerRunInventory` and `RuntimeEffectReceiver`.
- Update formulas to read recipe parameter overrides.
- Gradually migrate character assets to generic formula definitions plus kit recipes.

## Related
- Task card: `Docs_JJK/3_Outputs/Specs/runtime-effect-character-progression-refactor-task-card.md`
- Impact analysis: `Docs_JJK/3_Outputs/Specs/runtime-effect-character-progression-refactor-impact-analysis.md`
- Scripts affected: `RuntimeEffectDefinition.cs`, `CharacterProgressionDefinition.cs`, `PlayerRunInventory.cs`, `RuntimeEffectReceiver.cs`, `CharacterPassiveController.cs`, `ProjectileRuntime.cs`
