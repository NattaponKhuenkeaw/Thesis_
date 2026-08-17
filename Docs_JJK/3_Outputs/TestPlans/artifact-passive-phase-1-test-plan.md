---
slug: "artifact-passive-phase-1"
status: draft
source: "artifact-passive-phase-1-design-spec"
owner: "qa-agent"
human_checkpoint: required
blocked_by:
  - "generic-dot-runtime"
  - "explosion-splash-executor"
  - "phase-1-playmode-feedback-assets"
---

# Test Plan: Artifact Passive Phase 1

## Automated EditMode Baseline

Run on 2026-08-18 in Unity 6000.3.4f1:

- Result: **4 passed, 0 failed, 0 skipped**.
- Stable definition IDs `ART-P1-001` through `ART-P1-009` load from Resources.
- Every Phase-1 definition references a non-empty passive set.
- Focus and Thunder share state inside one instance but not across duplicate instances.
- Compatibility proc defaults preserve Primary on-hit/Aura behavior and keep DOT terminal.
- `ParticleBehaviorDefinition` has no `ProjectileData` field.
- `DamageDelivery` remains exactly Primary, Dot, and Additional.

## Functional PlayMode Matrix

| ID | Scenario | Pass condition | Current readiness |
| --- | --- | --- | --- |
| A01 | Ultimate then rotate to each ally | Qualifying ally damage becomes Darkness for 10s; delivery is unchanged | Ready for PlayMode |
| A02 | One Particle pulse hits three enemies | Exactly one +3% ATK stack; refreshes 3s duration; caps at ten | Ready for PlayMode |
| A03 | Skill, remain in barrier, then switch out | 50% ATK pulses every 2s; heal uses confirmed total; field ends when wearer anchor is unavailable | Ready for PlayMode |
| A04 | Skill mark then eligible hit | One deterministic mark; one deduplicated Splash; terminal 3s Bleed; Claw behavior | Blocked by DOT/Splash slice |
| A05 | Switch into wearer with current and late-spawn enemies | Current living room enemies gain one stack; late spawn is unchanged | Ready for PlayMode |
| A06 | Skill hit then kill anchor | 200% ATK pulses and +10% damage taken; field ends with anchor | Ready for PlayMode |
| A07 | Successful, failed, and same-character switch | Only a successful switch away buffs the actual incoming character | Ready for PlayMode |
| A08 | Cross 90% and 66% in both directions | Exact boundary state, no Max-HP feedback loop, one transition signal | Ready for PlayMode |
| A09 | Six Critical Primary hits then miss/multi-hit | Arms at six; miss does not consume; first confirmed Primary hit consumes once | Ready for PlayMode |

## Stress and Abuse Tests

- Equip duplicate definitions on different characters and verify independent cooldowns/stacks.
- Hit a multi-collider enemy and multiple enemies in one Particle pulse; verify target deduplication.
- Switch during an active wearer-following Particle; verify source-loss cleanup.
- Kill an enemy on the hit that creates a secondary event; verify result publishes before cleanup.
- Pause during cooldowns/Particles and verify gameplay-time timers stop.
- Run all nine definitions together while profiling managed allocations and frame time.
- Verify DOT ticks, Particle children, Explosion children, and Thunder Strike cannot recursively arm
  ordinary on-hit chains.

## Readability and Feel Checkpoint

Before balance approval, a new observer must identify direct hit, Particle pulse, DOT tick,
Explosion Splash, Focus stack, armed Thunder, and HP-threshold transition correctly in 8/10 cases.
Major activations require visual and audio feedback; repeated interval ticks must remain lower
priority. Icons, VFX prefabs, and audio clips are not yet authored and remain a human checkpoint.

