---
slug: qte-team-swap
status: approved
source: manual
gdd_tags: [combat, combat-scope, characters, platform-input, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

## Implemented revision - 2026-10-27

Apricha approved implementation, then playtested and requested strong slow motion at intro start and spawn offsets relative to the outgoing character. This revision supersedes the historical enemy-relative/no-slow-motion proposal below.

Current rule: spawn X = outgoing X minus outgoing facing times entryOffset (Main 3, CameraGirl 1), collision-limited and grounded. After placement, face the selected enemy and execute the exact referenced attack. Starting handoff slowdown: 0.08x speed for 0.24 real seconds. Settings are saved on Player prefab > TeamManager > QteTeamSwapController; entryOffset and exact action/step references are on each CharacterObject's QTE Team Swap profile. The five-second refresh-only window, accepted-hit triggers and anti-loop rules remain.

Tuning test: try both swaps and ten quick exchanges with a target behind and ahead. Pass when the outgoing body remains readable, the new body appears behind the outgoing facing, and slowdown clarifies control transfer without stalling the exchange. If too sticky, shorten introSlowMotionSeconds first; if too subtle, lower introTimeScale. These are editable starting values, not measurements from the reference games.

# QTE Team Swap implementation plan

Authorization: Apricha's “LGTM, implementation” on 2026-10-27 accepts the design and explicitly requests implementation. Proceed with the existing frame executor/FSM and target-owned damage pipeline; no global architecture replacement, dependencies or build changes.

| Responsibility | Owner |
|---|---|
| Authored trigger ids, referenced intro action/step, offset and ready sound | QteSwapDefinition on CharacterObject |
| Accepted-hit provenance and per-step confirmation | CombatExecutionContext carried by DamageRequest/ProjectileSpawnContext/DamageResolvedEvent |
| Five-second refresh-only offer, target capture, lifecycle and eligibility | QteTeamSwapController on TeamLoader |
| Single-step intro, authored action events, cancel and interruption | PlayerQteIntroState reusing CombatPlayerState / PlayerAttackState |
| Atomic ordinary/QTE handoff and outgoing executor capture | TeamLoader, through the existing swap entry point |
| Collision-checked grounded entry placement | PlayerMovement |
| Destination-aware input eligibility and buffer consumption | PlayerStateMachine / TeamSwitchHUD |
| White pulse and independent remaining-window display | CharacterSwitchSlot |

Damage context is optional for compatibility with existing damage producers. Only contexts issued by the real on-field combat executor can arm QTEs. A field-session generation invalidates delayed projectiles across swaps, including swapping back to the same character. Clone hits deliberately omit the context. Intro execution retains original attack ids for passives and independently marks intro origin.

The intro resolver wraps the referenced step as a terminal node; it does not mutate the source combo tree or auto-continue Attack5. Manual resource costs are bypassed only for the already-authorized intro entry. All hit payload and event data remain referenced. Existing ADR-003 state-owned movement, ADR-005 deployed stat authority and ADR-010 typed combat event ownership remain authoritative.

Validation: focused EditMode checks for actual assets, accepted-hit filtering, expiry/refresh, field-session provenance, safe placement, early swap, resource-free entry, preserved executor hit history and intro termination. Check Unity compilation/console and capture visual evidence where possible. Human remains responsible for final subjective combat feel.
