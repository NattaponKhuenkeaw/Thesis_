---
slug: runtime-effect-character-progression-refactor
status: approved
source: manual
gdd_tags:
  - combat
  - characters
  - progression
owner: architect-agent
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# Impact Analysis: Runtime Effect / Character Progression Refactor

## Refactor Goal

Separate reusable runtime-effect formula definitions from character-authored application recipes so kits own values, trigger conditions, target scope, duration, stacking, display, and removal policy.

## Current Problem

`RuntimeEffectDefinition` currently mixes formula hooks with ownership policy. A stat formula asset can also own its duration, target, stackability, display metadata, and character-specific numeric values. This forces many near-duplicate assets for the same formula shape.

## Class Responsibility Table

| Class | Before | After |
| --- | --- | --- |
| `RuntimeEffectDefinition` | Formula hooks plus id, display, duration, target, stack, remove policy, and default numeric values. | Formula type and legacy defaults only; execution reads recipe parameters when present. |
| `RuntimeEffectRecipe` | Missing. | Per-use authoring unit that references a formula and owns policy, presentation, and formula parameters. |
| `PassiveRuntimeEffectDefinition` | Trigger/filter plus direct effect reference and partial application policy. | Character-kit trigger/filter plus recipe wrapper. |
| `RuntimeAreaEffectApplication` | Projectile area application plus direct effect reference and partial policy. | Projectile application plus recipe wrapper. |
| `RuntimeEffectInstance` | Live reference to a definition plus timer/source/stack state. | Live reference to resolved definition and recipe, with timer/source/stack state. |
| `PlayerRunInventory` | Run-owned effect storage, but reads duration/stack/remove policy from definitions. | Run-owned effect storage that resolves policy from recipe first, definition fallback second. |
| `RuntimeEffectReceiver` | Local enemy/player receiver with duplicated definition-policy logic. | Local receiver that mirrors recipe-aware run inventory behavior. |
| `RuntimeEffectContainerDefinition` | A definition that forwards to child definitions. | Compatibility wrapper; recipes become the preferred bundle/container layer. |

## Data Flow

```text
Kit trigger / projectile area tick
  -> RuntimeEffectRecipe
  -> PlayerRunInventory or RuntimeEffectReceiver
  -> RuntimeEffectInstance
  -> RuntimeEffectDefinition formula hooks
  -> Stats / damage / heal / status / HUD reaction
```

## Authority Decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Formula ownership | `RuntimeEffectDefinition` | One reusable code-backed ingredient per behavior type. |
| Application ownership | `RuntimeEffectRecipe` authored in kits/projectiles/artifacts | Characters should decide values, duration, target, and stack policy. |
| Live state ownership | `PlayerRunInventory` for team/run effects; `RuntimeEffectReceiver` for enemy/local effects | Preserves existing runtime authority split. |
| Conditions | Character kit trigger/filter classes | Conditions are kit behavior, not formula behavior. |
| Migration | Incremental compatibility | Existing assets can keep using legacy definition fields until manually migrated. |

## Blast Radius

- High: `RuntimeEffectDefinition`, `RuntimeEffectInstance`, formula subclasses, `PlayerRunInventory`, `RuntimeEffectReceiver`.
- Medium: `CharacterPassiveController`, `ProjectileRuntime`, debug/HUD display paths.
- Low: Existing character assets, because this pass keeps legacy field fallback.

## Incremental Strategy

1. Introduce recipe/policy/presentation/parameter data classes.
2. Make runtime instances and application APIs recipe-aware.
3. Update formula subclasses to prefer recipe parameters and fall back to definition defaults.
4. Keep serialized legacy fields for current assets.
5. Migrate individual character assets later, starting with Rugal/Kyo/Goenitz runtime effects.

## Regression Focus

- Stat effects still update `TeamLoader` runtime stats and HUD.
- Stack merging remains conservative across definition, source character, source stat snapshot, duration, and recipe key.
- Instant effects are applied and removed in the same frame.
- Remove-on-owner-damaged reads recipe policy when present.
- Enemy runtime effects still tick, expire, and show visuals.
