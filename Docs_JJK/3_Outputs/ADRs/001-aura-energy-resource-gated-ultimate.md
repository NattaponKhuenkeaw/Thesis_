# ADR-001: Aura Energy Resource-Gated Ultimate System

| Field       | Value                    |
|-------------|--------------------------|
| Status      | **Accepted**             |
| Date        | 2026-06-05               |
| Author      | Software Architect Agent |
| GDD Section | `@tag:combat`            |

## Context

The current player Ultimate ability system relies on standard time-based cooldown timers. This design fails to align with the core game experience pillar of "Fast-paced Hack & Slash" which prioritizes active aggression, flashy visual feedback, and tactile reward loops. We need to transition the Ultimate to an **Aura Energy (AE)** resource-gated system where energy is earned directly through landing hits and defeating enemies. This system must also handle team swap continuity, progression-based stat scaling, and robust HUD/VFX feedback.

## Decision

We decide to:
1. **Remove time-based cooldown gating** from the Ultimate ability and replace it with an Aura Energy (AE) resource gate.
2. **Store AE per character** in the `TeamLoader` runtime state manager to support individual meters for benched characters.
3. **Keep only `auraEnergyMultiplier` as a progression stat** on `CharacterStats`. Maximum AE is derived from authored Ultimate costs instead of stored independently.
4. **Calculate Ultimate cost dynamically** using the formula: `Base Cost (100 AE) * (1.0 - Math.Min(0.50f, CDR * 0.50f))` where CDR is the character's `cooldownReduction` stat divided by 100.
5. **Resolve hit events deterministically** for AE gain inside `HitBoxDamage` to prevent framerate-dependent energy generation.
6. **Support background execution of Ultimate active frames** during a team switch via a temporary `BackgroundUltimateExecutor` clone prefab.
7. **Apply Super Armor + Invulnerability (i-frames)** to the player character during the Active phase of the Ultimate, consuming AE on transition to Active rather than Startup.
8. **Update HUD meters** (via `AbilityUIManager` and `CharacterSwitchSlot`) to show circular fill percentages and pulse/glow/chime feedback when ready.
9. **Separate the two hit-confirmed AE sources.** Every damaging hit independently rolls the character's `AuraEnergyRate` (Low `5%`, Med `10%`, High `15%`) for a fixed `10 AE` base reward owned by `PlayerCombat`. Each `CombatAction` may also author one guaranteed `auraEnergyReward` (default `0`) that is granted once per successful hitbox delivery. Both rewards use the acting character's `auraEnergyMultiplier`; defeating an enemy has no separate AE branch.
10. **Set each character's maximum AE to their runtime Ultimate cost.** A 50 AE Ultimate therefore has a 50 AE capacity.
11. **Require the AE meter to reach its full derived capacity before an Ultimate becomes ready.** Capacity, readiness threshold, and consumed Ultimate cost use the same runtime value.

## Alternatives Considered

| Option | Pros | Cons |
|--------|------|------|
| **A: Shared Team AE Pool** | Simplifies UI and memory tracking. | Eliminates tactical character-specific management and limits design space for upgrades. |
| **B: Dynamic Background Executor (Chosen)** | Ensures seamless active frame execution of Ultimate damage/VFX while allowing immediate player swap control. | Requires instantiating a temporary visual/hitbox clone in the background. |
| **C: Cancel Ultimate on Swap** | Extremely easy to implement. | Ruins player momentum and feels unresponsive. |

## Consequences

### Positive
- Encourages aggressive, combat-focused gameplay rather than passive waiting.
- Robust handling of team-swapping ensures high-quality game feel.
- Dynamic cost reduction integrates CDR mastery into the resource system logically.
- Action designers can attach a guaranteed hit-confirmed AE reward directly to attacks, skills, or Ultimates without changing the global per-hit chance reward.
- AE capacity automatically matches authored Ultimate cost without a duplicate max-stat field.

### Negative / Trade-offs
- Slight overhead from instantiating background executors.
- Complex HUD syncing between active character and benched character slots.

### Migration
- Add `auraEnergyMultiplier` to `CharacterStats.cs`.
- Author an optional `auraEnergyReward` value on ActionResolver actions.
- Derive maximum AE in `TeamLoader` from the character's Ultimate actions.
- Update `HitBoxDamage.cs` to trigger AE generation on successful hits.
- Refactor `PlayerUltimateState.cs` to check and consume AE instead of trigger cooldowns.
- Refactor `AbilityUIManager.cs` and `CharacterSwitchSlot.cs` to read AE and cost values.

## Related
- GDD Section: `@tag:combat`, `@tag:platform-input`
- Scripts affected: `PlayerCombat.cs`, `HitBoxDamage.cs`, `PlayerUltimateState.cs`, `AbilityUIManager.cs`, `CharacterStats.cs`, `TeamLoader.cs`, `PlayerCharacter.cs`
