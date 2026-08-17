---
slug: "artifact-passive-event-and-secondary-effect-authority"
status: approved
source: manual
gdd_tags:
  - "combat"
  - "items"
  - "progression"
owner: "architect-agent"
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# ADR-010: Artifact Passive Event and Secondary-Effect Authority

| Field | Value |
| --- | --- |
| Status | **Accepted** |
| Date | 2026-08-18 |
| Author | Codex |
| GDD Section | `@tag:combat`, `@tag:items`, `@tag:progression` |

## Context

ADR-009 gives every Artifact copy stable instance identity, but Artifact definitions currently expose
only display text and shared Additional Damage definitions. Phase-1 content requires per-instance
cooldowns/counters, off-field attribution, damage-type infusion, custom DOT application/ticks,
Projectile-independent Particle areas, and Explosion Splash without turning origin/presentation into
a damage formula.

## Decision

- Add immutable `ArtifactPassiveSetDefinition` content to `ArtifactDefinition` and create one
  transient `ArtifactPassiveRuntime` per deployed Artifact instance.
- Make a run-scoped `ArtifactPassiveRuntimeSystem` own instance cooldowns, counters, armed flags, and
  trigger indexes. `TeamLoader` only reconciles deployed snapshots; `CharacterPassiveController`
  remains Character Potential authority.
- Publish normalized action, damage, switch, and health events through a typed `CombatEventHub` that
  owns no passive state.
- Extend damage requests/results with orthogonal provenance, origin tags, and proc/side-effect policy.
  `DamageDelivery` remains formula selection and `ElementType` remains hit-owned damage type.
- Represent Particle and Explosion as tags/behaviors. Particle gameplay uses pooled
  `ParticleBehaviorRuntime` data with behavior/effect/compatibility layers and no `ProjectileData`
  dependency. Explosion Splash is a spatial fan-out executor that preserves or explicitly overrides
  the parent delivery/type.
- Separate DOT application from tick execution. A target-owned `DotRuntimeReceiver` owns duration,
  cadence, stacking, and source snapshots; DOT ticks submit terminal requests through the shared
  damage processor.
- Use one allocation-free `CombatIntervalScheduler` for active DOT and Particle interval work.
- Reuse ADR-002 Runtime Effect Recipes for target-owned buffs, debuffs, shields, healing, and status
  presentation. Artifact runtime state owns trigger mechanics, not target effect state.
- Keep Phase-1 combat state out of saves. Deployed instance cooldowns/counters may survive scene
  transitions, while target/world state clears with its scene.
- Preserve legacy Additional Damage and elemental DOT paths behind compatibility adapters until
  regression and asset validation complete.

## Alternatives Considered

| Option | Pros | Cons |
| --- | --- | --- |
| Add all nine passives directly to `CharacterPassiveController` | Reuses existing callbacks quickly. | Mixes Character Potential and Artifact identity, grows a god object, and cannot isolate duplicate instances safely. |
| Store counters/cooldowns on `ArtifactDefinition` | Minimal runtime types. | Shared ScriptableObject state leaks across duplicates, characters, and runs. |
| Treat Particle as `ProjectileData` Terrain mode | Reuses area tracking and VFX. | Inherits traversal, hit count, replacement, collider, and projectile ownership edge cases that do not apply to fields. |
| Add Particle and Explosion to `DamageDelivery` | Simple branching. | Couples formula to origin/presentation and breaks ADR-008 composition such as DOT Explosion Splash. |
| Keep custom DOT inside `EnemyDamageTypeState` | Small change for elemental DOT. | Keeps element profile, reductions, application conditions, stacking, and tick routines coupled. |
| Per-effect coroutine/Update | Local implementation. | Multiplies polling/coroutines and complicates pause, scene cleanup, and allocation control. |
| Instance runtime + typed events + focused effect services | Preserves existing authorities and supports all Phase-1 combinations. | Introduces explicit contracts and requires an incremental compatibility period. |

## Consequences

### Positive

- Duplicate Artifact copies remain independent during combat.
- Active and off-field source attribution uses the same deployed authority.
- DOT, Particle, and Explosion combine without multiplying damage formulas.
- Secondary chains are traceable and protected from accidental recursion.
- Runtime Effect Recipes remain reusable rather than being duplicated in Artifact code.
- Particle gameplay can evolve without inheriting projectile edge cases.
- The nine Phase-1 Artifacts serve as an architecture acceptance suite for later content.

### Negative / Trade-offs

- Damage request construction gains provenance and policy fields across existing producers.
- Elemental DOT needs a compatibility adapter before its state can fully move to the generic receiver.
- Unity authoring requires validation/property drawers for polymorphic passive and Particle responses.
- A persistent event/runtime service and a scene-scoped enemy registry require careful subscription
  and scene cleanup tests.
- Phase-1 transient cooldown/counter state is intentionally lost on full save/load.

### Migration

- Restore or explicitly migrate the nine existing Artifact assets/meta files before passive authoring;
  ADR-009 preservation remains mandatory.
- Add event/provenance contracts with compatibility defaults and lock current damage regressions.
- Add passive runtime beside existing Additional Damage behavior; migrate content incrementally.
- Add generic DOT receiver and prove elemental DOT cadence parity before delegation.
- Add pooled Particle and Explosion services without adding fields to `ProjectileData`.
- Author Phase-1 content in three waves: A07/A08/A05, A01/A02/A03/A06, then A09/A04.
- Remove compatibility branches only after Unity asset, EditMode, PlayMode, and profiler validation.

## Related

- Extends ADR-002: Runtime Effect Recipes.
- Extends ADR-004: Target-Owned Runtime State Channels.
- Extends ADR-005: Deployed Character Live-Stat Authority.
- Preserves ADR-006: Hit-Owned Damage Types and Enemy Reductions.
- Extends ADR-008: Orthogonal Damage Delivery and Hit-Owned Type.
- Extends ADR-009: Artifact Definition and Instance Authority.
- Task card: `Docs_JJK/3_Outputs/Specs/artifact-passive-phase-1-task-card.md`.
- Design spec: `Docs_JJK/3_Outputs/Specs/artifact-passive-phase-1-design-spec.md`.
- Architecture plan: `Docs_JJK/3_Outputs/Specs/artifact-passive-phase-1-arch-plan.md`.
