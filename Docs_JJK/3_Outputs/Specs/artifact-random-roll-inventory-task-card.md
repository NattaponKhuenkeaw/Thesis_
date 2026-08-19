---
slug: "artifact-random-roll-inventory"
status: implementation-validation
source: manual
gdd_tags:
  - "core-loop"
  - "ui"
  - "progression"
  - "items"
owner: "orchestrator-agent"
human_checkpoint: required
next_agent: game-design-agent
blocked_by: []
---

# Task Card: Artifact Random Rolls and Inventory Discovery

## Player-Facing Goal

Every Artifact acquisition creates a distinct, duplicate-allowed item with one to four randomized stat lines. Artifacts have no rarity. Players judge them by their rolled values and use sorting, filtering, and a Favourite-selection mode to find and preserve useful instances.

## Source

- Origin: Manual prompts on 2026-08-17
- Requested by: User
- Reference: Genshin-inspired randomized stat hunting using the probabilities and ranges supplied by the user, without an Artifact rarity system.

## GDD Reference

- `@tag:core-loop` - Rewards may include an Artifact or stat-focused upgrade between combat rooms.
- `@tag:ui` - Team Builder and Character Inspector own Artifact lookup, preview, and loadout editing in Safe Zones.
- `@tag:progression` - Soul Shop duplicate conversion and Artifact Transcendence are the current rules to replace.
- `@tag:items` - Artifacts currently provide fixed `bonusStats`; all existing Artifact data must migrate into the instance system.

## Type

- [x] Feature
- [ ] Bug fix
- [x] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Artifact definition versus owned-instance identity
- Artifact acquisition and randomized stat generation
- Roll-quality calculation without rarity labels or tiers
- Run inventory persistence and equipped Artifact slot references
- Artifact stat resolution, decimal display, and integer damage output
- Artifact grid sorting, filtering, and Favourite-selection mode
- Shop acquisition behavior for duplicate Artifact instances
- Removal of Artifact Transcendence fragments from `PlayerMetaProgression`
- Migration of every existing Artifact definition, stat, passive, price, and reference
- GDD Artifact, Soul Shop, Backpack, and progression sections after approval

### Files To Inspect First

- `Assets/Project/Scripts/Inventory/Items/Artifact.cs`
- `Assets/Project/Scripts/Progression/InventoryAcquisitionResolver.cs`
- `Assets/Project/Scripts/Progression/PlayerMetaProgression.cs`
- `Assets/Project/Scripts/Shop/PlayerRunInventory.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/ArtifactGridPanel.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/ArtifactGridItemUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/ArtifactPreviewPanel.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/ArtifactSlotUI.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/TeamLoadoutProfile.cs`
- `Assets/Project/Scripts/Character/CharacterStats.cs`
- `Assets/Project/Scripts/Combat/CombatDamageResolver.cs`

### Out of Scope

- Artifact rarity, stars, rarity colors, or rarity-derived behavior
- Artifact enhancement, leveling, rerolling, or stat-line upgrading
- New compare, recycle, bulk-action, or lock systems
- Changes to Character Potential fragments
- Terrain/Particle implementation or new Artifact passive behaviors
- New Artifact art, icons, or audio assets

## Acceptance Criteria

- [x] Every Artifact acquisition creates a unique persistent instance ID, even when its definition matches an owned Artifact.
- [x] Every generated instance has one to four contiguous stat lines and no rarity field.
- [x] Stat types may repeat within one Artifact and each roll retains its own value.
- [x] Roll amounts support two decimal places and remain within the approved inclusive range.
- [x] Roll quality is numeric and derived only from a value's normalized position within its stat range.
- [x] Flat HP/ATK/DEF add directly; percentage HP/ATK/DEF scale clean character base values; all other rolls add percentage points.
- [x] The Artifact inventory supports sorting, filtering, and persisted Favourite status.
- [x] Favourite mode activates explicitly; while active, clicking a card designates or undesignates that Artifact as Favourite.
- [x] Equipped slots, deployment, save/load, preview, and runtime stat resolution use Artifact instance identity rather than definition identity.
- [x] Artifact duplicates never convert to fragments; Character duplicate conversion remains unchanged.
- [x] Every existing Artifact asset and reference has a lossless importer and dry-run report; Unity asset validation remains pending.
- [x] Legacy values outside the new random ranges remain exact and do not receive a fabricated roll-quality score.
- [x] Artifact Transcendence data is omitted through v3 migration while Character Potential is preserved.
- [x] Damage calculations retain decimal precision internally and apply/display the same integer final damage exactly once.
- [ ] Existing combat, Artifact passives, Additional Damage, Team Builder Deploy, and Character Potential behavior do not regress.
- [x] Follows `RULES_AND_POLICY.md`.

## Human Checkpoints

- [x] Approve the revised roll-only design with no rarity or CURL system.
- [x] Approve the weighted amount distribution starting values.
- [x] Approve the complete legacy Artifact migration strategy.
- [x] Approve architecture and ADR before implementation.
- [ ] Review PR before merge.

## Router Decision

Recommended workflow: `/implement-feature`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/artifact-random-roll-inventory-design-spec.md`
