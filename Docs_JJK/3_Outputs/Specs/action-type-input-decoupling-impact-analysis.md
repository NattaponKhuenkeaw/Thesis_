---
slug: action-type-input-decoupling
status: approved
source: manual
gdd_tags: [combat, characters, platform-input, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Scoped Action Type implementation

The user rejected the earlier larger proposal. This document replaces it with the explicitly
requested metadata-only separation. Existing resolver collections and player states remain.

| Concern | Authority |
|---|---|
| Input selection and Skill/Ultimate HUD window | Existing resolver list and Category |
| AE, counter and cooldown costs | Existing action subtype and authored fields |
| Interrupt hierarchy and input gates | EffectiveActionType and its preset policy |
| MAX CANCEL | Source/destination effective types + source Recovery, at successful shared step commit |
| Ultimate per-hit white flash | Snapshotted execution/presentation type |
| Damage/passive classification and meter rules | Existing Category and action/step identity |
| QTE entry/proc exclusions | Existing prepared entry, field session and isQteIntro |

CombatActionType.FromInput keeps all old assets compatible without mass rewriting. Dimension
Slash alone receives an explicit Ultimate override. SkillAction does not gain an AE field;
the override does not grant the Ultimate state's invulnerability or alter authored overlays.

FrameDataPlayerState maps buffered combat inputs to the candidate's interrupt classification.
Each combat state reads the resolved action's policy; specialized QTE entry restrictions remain.
A small addition to existing TransitionContext carries source type/key/phase through cross-state
switches so the shared step commit can emit MAX CANCEL once. Same-state transitions use the
previous execution snapshot. Source resources/cleanup remain on the established paths.

PlayerCombat snapshots type into CombatExecutionContext. HitBoxDamage selects the existing
PlayHitFlash from that snapshot; projectiles and off-field clones retain presentation classification
without receiving false on-field QTE provenance. Category remains unchanged throughout.

Regression scope: zero-AE Skill-bound Ultimate type; startup/active commitment; Ultimate↔Skill
Recovery MAX CANCEL; same action/opt-out/cost rejection; type snapshot and hit flash; Skill-window
consumption; existing meter/QTE tests. No asset payload regeneration, scene changes or dependencies.
