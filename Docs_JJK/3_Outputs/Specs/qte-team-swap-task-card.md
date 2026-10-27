---
slug: qte-team-swap
status: approved
source: manual
gdd_tags: [combat, combat-scope, characters, platform-input, visual-audio]
owner: game-design-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

## Implemented revision - 2026-10-27

Apricha approved implementation, then playtested and requested strong slow motion at intro start and spawn offsets relative to the outgoing character. This revision supersedes the historical enemy-relative/no-slow-motion proposal below.

Current rule: spawn X = outgoing X minus outgoing facing times entryOffset (Main 3, CameraGirl 1), collision-limited and grounded. After placement, face the selected enemy and execute the exact referenced attack. Starting handoff slowdown: 0.08x speed for 0.24 real seconds. Settings are saved on Player prefab > TeamManager > QteTeamSwapController; entryOffset and exact action/step references are on each CharacterObject's QTE Team Swap profile. The five-second refresh-only window, accepted-hit triggers and anti-loop rules remain.

Tuning test: try both swaps and ten quick exchanges with a target behind and ahead. Pass when the outgoing body remains readable, the new body appears behind the outgoing facing, and slowdown clarifies control transfer without stalling the exchange. If too sticky, shorten introSlowMotionSeconds first; if too subtle, lower introTimeScale. These are editable starting values, not measurements from the reference games.

# QTE Team Swap

## Player-facing goal

Make character switching continue the attack rhythm: a confirmed outgoing hit opens a readable opportunity to bring in a teammate with their own attack, while the outgoing character finishes the current action.

Source: Apricha's request in the Codex chat on 2026-10-27, including clarification that X offsets are relative to the enemy target. Workflow: `/implement-feature`.

## Requested rules

- Main's successful Dimension Cut hit readies off-field teammates.
- CameraGirl's successful Skill hit readies off-field teammates.
- Readiness lasts five seconds; subsequent qualifying hits refresh, without stacking.
- Eligible teammate portraits pulse with a white overlay.
- Incoming Main appears at target X minus 3, mirrored by approach direction, then performs the exact Dimension Cut action data.
- Incoming CameraGirl appears at target X minus 1, mirrored by approach direction, then performs the exact NA Attack4 data / cg_attack4 clip.
- Outgoing action clone remains until its action/animation finishes.

## Inspected systems

TeamLoader, PlayerStateMachine, CombatPlayerState, PlayerCombat, PlayerTeamSwitchState, CombatEventHub, CharacterSwitchSlot, both character ActionResolverTables, and the active GDD's swap rules.

## Acceptance criteria

- [ ] Confirmed damage, rather than input/animation start, opens readiness.
- [ ] QTE ownership, expiry, refresh, consumption and anti-loop behavior follow the design spec.
- [ ] Main and CameraGirl use their authored gameplay data, not animation-only approximations.
- [ ] Target-relative positions mirror correctly and cannot place the incoming character inside terrain or across an invalid floor gap.
- [ ] Keyboard and HUD activation share the same validity checks.
- [ ] Outgoing hits are not replayed or reattributed to the incoming character.
- [ ] No incoming idle/landing frame interrupts the handoff.
- [ ] Ordinary swap, skill cooldowns, cached cuts, hitstun, death and scene reset remain correct.
- [ ] Human combat-feel playtest passes the linked test plan.

## GDD alignment

`@tag:combat` and `@tag:combat-scope` define authored frame data and grounded swaps. `@tag:characters` motivates distinct character identity. `@tag:platform-input` defines the swap controls and buffer. `@tag:visual-audio` calls for readable swap identity cues.

The existing GDD says incoming characters begin in locomotion and that swaps are recovery-gated. The requested QTE is a deliberate, narrow extension: prepared swaps may hand off after a confirmed hit and start a character-specific intro. This change is proposed in the design spec, not yet written into the canonical GDD.

## Current stage

Research and read-only project inspection completed. Design spec and test plan prepared. No runtime scripts, authored attacks, prefabs, scenes, dependencies or build configuration changed. Existing extensive uncommitted project changes preserved.

Next artifact after human design approval: `qte-team-swap-arch-plan.md`. Architecture approval remains required before runtime implementation under the repository workflow.
