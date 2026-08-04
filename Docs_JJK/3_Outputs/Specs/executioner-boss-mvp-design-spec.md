# Design Spec: Executioner Boss MVP

GDD: `@tag:bosses`

## Player Experience

A large, slow heavy swordsman. Attacks are readable and punishing. The player learns to dash through active frames, then burst during recovery.

Loop: **Telegraph → Dodge → Recovery punish → repeat**, denser in phase 2.

## Patterns

| Id | Name | Range | Notes |
|----|------|-------|-------|
| 1 | Heavy Cleave | Short | Wide frontal slash |
| 2 | Targeted Jump Smash | Long | Lock player XY, parabola leap, AoE on land |
| 3 | Bull Charge | Long | Phase 2 only, straight-line rush |

## Phases

- **Phase 1 (HP > 50%)**: Cleave + Jump Smash. Slow chase. Recovery 1.5–2s.
- **Phase 2 (HP ≤ 50%)**: Adds Charge. After Jump Smash, may immediately Cleave once with no recovery (anti-punish trap).

## Timing Targets

- Telegraph: 1.0–1.5s (warning icon + optional ground marker)
- Active: 0.3–0.5s
- Recovery: 1.0–2.0s (phase dependent)

## Juice (MVP)

- Attack warning icon (existing `EnemyWorldUI`)
- Red ground marker for Jump Smash landing
- Optional sprite flash during telegraph
- Animator Attack trigger (pattern-specific triggers optional)

## Edge Cases

- Interrupt mid-telegraph by hit reaction only when not armored
- During Active/Telegraph: armored (`ignoreHitReactions`)
- Death mid-pattern cleans hitboxes + restores RB
- No target: idle / reacquire by Player tag
