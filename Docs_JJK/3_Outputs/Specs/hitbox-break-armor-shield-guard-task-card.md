---
slug: hitbox-break-armor-shield-guard
status: approved
source: manual
gdd_tags: [enemies]
owner: implementation-agent
human_checkpoint: user-request-authorizes-behavior; playtest-pending
next_agent: human
blocked_by: []
---

# Hitbox Breaks Armor: Shield Guard

Workflow: `/fix-bug`. User requests Break Armor to completely break Shield enemy guard.
GDD reference: `GDD_Neo-JJK.md` @tag:enemies; the specific shield interaction comes from this request.

## Investigation and implementation

`NormalHitbox.breaksArmor` already reaches `HitReactionData` and removes trash Super Armor.
Shield blocking returned before armor processing, so the flag could not counter a frontal shield.
Primary armor-breaking hits now break shield guard before block evaluation. Guard break ends the
current action, releases its movement/facing locks, disables its hitbox, and suppresses shield
blocking and guard poses for this enemy instance. Later attack rounds remain available.
Existing Super Armor behavior stays intact. Additional damage and DOT cannot destroy the shield.
No new tuning values, assets, dependencies, or architecture changes.

## Player experience

- Clarity/response: the marked hit penetrates the shield immediately; later frontal hits land.
- Satisfaction: existing successful-hit damage, reaction, VFX/audio paths become available.
- Fit/motivation: armor-breaking attacks provide the requested counter to shield defense.
- State entry: primary hit with `breaksArmor`; guard break persists for this instance.
- State cleanup: end guard, cancel strike/warning, release facing and horizontal plant.
- Edge cases: guarding, windup, striking, rear hits, repeated breaks, secondary damage, inactive/dead target.

## Validation

Added `EnemyFlightGuardTests` regression cases for the real hitbox pipeline in guarding,
windup, and striking, cleanup, subsequent guard rounds/front hits, and secondary damage exclusion.
Existing normal frontal block and grab-counter tests remain in place.
`git diff --check` passes. Unity tests and playtest have not been run: no Unity MCP/test runner
is connected and Unity already has editor processes open.

Manual check: use an attack with Breaks Armor enabled against a guarding Shield enemy;
confirm the hit lands, active strike ends, guard pose disappears, and later ordinary frontal
hits land while the enemy can still attack. Repeat with Breaks Armor disabled to confirm blocks.
Check the visual/audio feedback makes the successful hit readable.
