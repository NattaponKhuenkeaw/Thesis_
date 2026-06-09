# Project: Neo-JJK Combat System Follow-up Fixes

## Architecture
This is a Unity 2D action game project. The relevant modules are:
1. **Combat System** (`PlayerCombat`, `HitBoxDamage`, `ProjectileHitboxModule`): Handles hit detection, damage calculation, and Aura Energy (AE) gains.
2. **Character Stats** (`CharacterStats`, `CharacterObject`, `CharacterMetaData`): Holds stat variables, equipped items, and character definition data (including audio/visual links).
3. **UI System** (`TeamBuilderUI`): Restores runtime drafts and manages UI displays for characters and their equipped artifacts.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | M1: Aura Energy and Artifact Refactor | R1: AE Gain Chance Refactor, R3: Remove AE Rate from Artifact Field | None | PLANNED |
| 2 | M2: Projectile and Debug Fixes | R2: Projectile Hit Count and HitData Fix | None | PLANNED |
| 3 | M3: TeamUI Artifact Sync | R4: Sync Artifact Data with TeamUI | None | PLANNED |
| 4 | M4: CharacterObject and Audio Cleanup | R5: Clean Up CharacterObject and Audio | None | PLANNED |

## Interface Contracts
No new public interfaces are introduced. We must maintain:
- `CharacterStats` compatibility with Artifact inspectors and calculations.
- `PlayerCombat.NotifyResolvedEnemyHit` behavior on every hit.
- `HitBoxDamage.ConfigurePerEnemyDamageCalculation` compatibility with or without equipped artifacts.
- `TeamBuilderUI.LoadRuntimeDraft` syncing.
- `CharacterObject.audioData` restoration and cleanup of `CharacterMetaData`.
