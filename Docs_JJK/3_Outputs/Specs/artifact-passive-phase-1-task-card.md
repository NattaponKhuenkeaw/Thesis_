---
slug: "artifact-passive-phase-1"
status: approved
source: manual
gdd_tags:
  - "combat"
  - "items"
  - "progression"
owner: "implementation-agent"
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Artifact Passive Phase 1

## Player-Facing Goal

Nine equipped Artifact instances create distinct build-defining combat behaviors across character
rotation, DOT, Particle, Explosion Splash, Crit, sustain, and cursed-condition play. Their effects
remain attributable to the wearer while off-field, communicate activation clearly, and cannot
produce accidental recursive damage chains.

## Source

- Origin: Manual prompt plus `JJK-Artifact-Build 3b5c743aca6c80eb880bfa1cbeff6e44.csv`.
- Reference: `C:/Users/Apricha/Downloads/JJK-Artifact-Build 3b5c743aca6c80eb880bfa1cbeff6e44.csv`.
- Requested by: Project owner.
- Design approval: Approved in chat on 2026-08-17.

The CSV is intake data only. This task card and its linked design spec are the canonical Phase-1
repo artifacts.

## GDD Reference

- `@tag:combat` — delivery and damage type are orthogonal; Primary, DOT, and Additional damage use
  one request pipeline.
- `@tag:items` — Artifact definitions own reusable content while equipped instances contribute to
  deployed builds.
- `@tag:progression` — Artifacts are duplicate-able run equipment and loadout decisions apply on
  Deploy.

## Type

- [x] Feature
- [ ] Bug fix
- [ ] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Artifact definitions, deployed snapshots, and per-instance transient passive state.
- Combat damage events, source attribution, and secondary-event eligibility.
- Runtime Effect Recipes and target-owned DOT state.
- Particle gameplay volumes independent of `ProjectileData`.
- Explosion Splash target fan-out.
- Team switch, action, Crit, health-threshold, and room-enemy events.
- Passive VFX/audio/UI feedback hooks.

### Files To Inspect First

- `Assets/Project/Scripts/Inventory/Items/ArtifactDefinition.cs`
- `Assets/Project/Scripts/Combat/ArtifactRuntimeSnapshot.cs`
- `Assets/Project/Scripts/Combat/DamageContracts.cs`
- `Assets/Project/Scripts/Combat/CombatDamageProcessor.cs`
- `Assets/Project/Scripts/Combat/CharacterPassiveController.cs`
- `Assets/Project/Scripts/Combat/Elements/EnemyDamageTypeState.cs`
- `Assets/Project/Scripts/Systems/TeamLoader.cs`
- `Docs_JJK/3_Outputs/ADRs/002-runtime-effect-recipes.md`
- `Docs_JJK/3_Outputs/ADRs/004-target-owned-runtime-state-channels.md`
- `Docs_JJK/3_Outputs/ADRs/005-deployed-character-live-stat-authority.md`
- `Docs_JJK/3_Outputs/ADRs/008-unified-player-damage-delivery.md`
- `Docs_JJK/3_Outputs/ADRs/009-artifact-definition-instance-authority.md`

### Out of Scope

- The remaining twenty CSV passive rows.
- Final balance tuning of the CSV values.
- New icon production, rarity, and final economy pricing. Phase-1 names, descriptions, and lore use
  the first nine planned GDD Artifact concepts.
- Economy, room-clear rewards, mobility changes, stealth, clone attacks, or backstab services.
- Migrating all Character Potential behavior into the Artifact passive evaluator.
- Using `ProjectileData` for Particle behaviors.
- Save persistence for combat-transient counters, active DOTs, Particles, or cooldown timers.

## Phase-1 Artifact IDs

The CSV Name column is blank. The project owner approved retiring the prior nine Artifact assets and
creating replacements from the first nine planned GDD concepts. GDD identity order maps directly to
the approved Phase-1 behavior order:

| Build ID | Stable definition ID | GDD identity | Approved behavior |
| --- | --- | --- | --- |
| `P1-A01` | `ART-P1-001` | Blind's Boundless Ribbon | Ultimate grants Toxic to allies. |
| `P1-A02` | `ART-P1-002` | Fiend-Flesh Mask | Particle pulses build a stacking ally ATK buff. |
| `P1-A03` | `ART-P1-003` | Soul-Merchant's Scales | Skill summons the self-following Throne Barrier. |
| `P1-A04` | `ART-P1-004` | Broken Chrono-Relic | Skill Mark Injury chains into Explosion Splash, Bleed, and Claw. |
| `P1-A05` | `ART-P1-005` | Aura-Resonating Shard | Switch-in applies stacking room-wide Affliction. |
| `P1-A06` | `ART-P1-006` | Rival's Broken Crest | Skill hit creates enemy-following Wrath of Demon. |
| `P1-A07` | `ART-P1-007` | Shroud of Deprivation | Switch-out grants a timed ATK/Crit buff. |
| `P1-A08` | `ART-P1-008` | Memory-Draining Urn | HP thresholds control a Crit benefit and cursed penalty. |
| `P1-A09` | `ART-P1-009` | Shin's Rusty Dagger | Crits build Focus and arm Thunder Strike. |

For Phase 1, the CSV/design spec is functional truth. The original speculative passive text in the
planned-concepts table is retained as historical concept material and does not override this mapping.

## Acceptance Criteria

- [ ] All nine passives are authored through shared data contracts with no Artifact-ID branches.
- [ ] Every counter, stack, cooldown, and armed flag belongs to one Artifact instance.
- [ ] Primary, DOT, and Additional formulas continue to follow ADR-008.
- [ ] Particle and Explosion are tags/behaviors, not new damage deliveries.
- [ ] DOT application and DOT tick execution are separate stages.
- [ ] DOT ticks and default secondary effects cannot recursively proc themselves.
- [ ] Particle gameplay uses a pooled area runtime independent of `ProjectileData`.
- [ ] Explosion Splash hits each eligible target at most once per event.
- [ ] Active and off-field source attribution follows ADR-005.
- [ ] Duplicate Artifact definitions do not share runtime state.
- [ ] Switching, target death, scene transition, pause, Deploy, and source removal are defined.
- [ ] Significant activations provide visual and audio feedback.
- [ ] New-player, stress, skill, abuse, and readability playtests are documented.
- [ ] Existing character passives, projectiles, elemental DOT, Runtime Effects, and damage reporting regressions pass.
- [ ] Gameplay hot paths allocate no managed memory per damage event or Particle pulse.
- [ ] Works on the PC-first keyboard/mouse and controller input model without adding new inputs.
- [ ] Follows `RULES_AND_POLICY.md`.

## Pre-Implementation Gates

- [x] Project owner confirmed the prior nine Artifact asset deletions were intentional.
- [x] Replacement identities and stable definition IDs assigned from the GDD.
- [x] Architecture plan and ADR-010 accepted.
- [ ] Create a Phase-1 test plan before implementation completion.

## Human Checkpoints

- [x] Design/game-feel approval — approved 2026-08-17.
- [x] Architecture approval — approved 2026-08-18.
- [x] Intentional retirement and replacement approval — approved 2026-08-18.
- [ ] PR review before merge.
- [ ] Team status publish approval.

## Router Decision

Workflow: `/implement-feature`

Current artifact: `Docs_JJK/3_Outputs/Specs/artifact-passive-phase-1-task-card.md`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/artifact-passive-phase-1-design-spec.md`
- `Docs_JJK/3_Outputs/Specs/artifact-passive-phase-1-arch-plan.md`

## Implementation Status — 2026-08-18

- Nine replacement Artifact definitions and passive sets created from the approved GDD mapping.
- Typed action, switch, health, and resolved-damage events are integrated with compatibility defaults.
- Per-instance passive state, Runtime Effect responses, Toxic infusion, room registry, and
  projectile-independent Particle pulse runtime are implemented.
- Automated Phase-1 contract tests pass: 4 passed, 0 failed, 0 skipped.
- A04 remains blocked on the generic DOT receiver and Explosion Splash executor; icons, VFX/audio,
  full PlayMode validation, and profiling are also outstanding. The feature is not PR-ready yet.
