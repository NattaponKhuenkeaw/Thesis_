---
slug: goenitz-blood-frenzy-kit
status: needs-human
source: manual
gdd_tags:
  - combat
  - characters
owner: architect-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Goenitz Blood Frenzy Kit Rework

## Player-Facing Goal

Rework Goenitz around a hold-Skill red tornado and a hold-Ultimate Blood Frenzy state whose remaining lifetime is funded by the existing per-character Aura Energy gauge. Analyze existing systems before gameplay edits.

## Source

- Origin: user's kit request in this Codex chat, 2026-11-03.
- Requested by: repository user.
- Canonical target: `Assets/Project/Character/Hero/test_15_goenitz/CharacterObject_Goenitz.asset`.

## GDD Reference

- `@tag:combat`: per-character AE, authored action costs/cooldowns, shared Runtime Effect Recipes, shared damage delivery.
- `@tag:characters`: character-specific authored kit and Potential progression.
- User's detailed kit request is the intended behavior change; the current asset is evidence of existing behavior only.

## Type and Routing

- [x] Feature / kit rework.
- [ ] Behavior-preserving refactor.
- Selected workflow: `/implement-feature`. The user calls this a refactor, but the router classifies new player-facing behavior as feature work.
- Current phase: design approved with user revisions; architecture plan prepared for its required checkpoint.
- Next output: implementation after approval of `goenitz-blood-frenzy-kit-arch-plan.md` and ADR-020.
- No delegated agents required for this investigation.

## Scope

- Skill Potential remap, hold Skill input, EX projectile, and Lv.4 projectile upgrades.
- Hold Ultimate transformation, runtime tag plus recipe, AE sustain, gauge feedback, Frenzy-dependent follow-up, kill stacks and duration extension.
- Passive Potential remap, Frenzy-gated attack route and dash follow-ups, ally propagation and evasion.
- Kit presentation and exact damage budget verification.
- Shared systems only where the approved design cannot be expressed with existing authoring.

## Acceptance Criteria

- [ ] Skill base remains Yonokaze, 3 seconds, 350% ATK; critical damage clause is implemented with approved semantics.
- [ ] Skill Lv.1 unlocks hold EX Yonokaze, 7 seconds, homing red tornado, 10-second base lifespan, 250% ATK, critical one-shot Bleed damage 70% ATK and +5 AE per critical hit outside Frenzy.
- [ ] Skill Lv.2 grants CR +17%; Lv.3 grants CD +43%; Lv.4 upgrades both tornado variants.
- [ ] Ultimate Lv.1 unlocks zero-entry-cost Blood Frenzy on hold Ultimate, draining 3 AE each combat second; runtime tag and recipe always agree.
- [ ] Frenzy grants DI/DM/ADI +25% and attack-triggered one-shot Bleed damage 50% ATK; generic Bleed DOT may follow.
- [ ] Ultimate Lv.2 applies +15% incoming damage taken for 20 seconds on confirmed hits from any Frenzy actor, self or allies, without an Ultimate-category restriction.
- [ ] Ultimate Lv.3 permits Shin exactly while Frenzy is active, consumes Frenzy, and applies one-shot Bleed damage 500% ATK; Goenitz Ultimate/limit-break use grants one refreshed Blizzard stack lasting 15 seconds, including +10 ADI.
- [ ] Ultimate Lv.4 rewards only Goenitz's kills during Frenzy, any attack category: +3% ATK per stack, cap 10, no timer, removed only on damage during the run; qualifying kills restore 6 AE. Remembered eligibility lasts until the next qualifying damage resolution.
- [ ] Yamidokoku remains 18 seconds, 800% + 300% ATK; Shin: Yaotome is 850% ATK; both retain the requested critical clause.
- [ ] Base evasion is 35%; Passive Lv.1 critical hits permanently reduce target Physical resistance by 15%.
- [ ] Passive Lv.2 gates Attack2-2, 100% ATK Physical dash-through damage, post-dash first damaging action's 4% target-MaxHP Physical damage, and stage-4 normal start on Frenzy.
- [ ] Passive Lv.3 shares the approved Frenzy benefits with allies; Lv.4 grants allies +35% evasion while affected.
- [ ] Swaps, background projectiles/Ultimates, pause, low frame rates, death, room transition, Training reset, and Potential changes cannot duplicate or strand effects.
- [ ] Unrelated characters and existing AE generation/cost rules remain correct.
- [ ] During Frenzy, every ordinary Goenitz AE gain is blocked; only the explicitly classified kill extension may add AE. Allies share Goenitz's lifetime without paying AE themselves.

## Human Checkpoints

- Design/game-feel approval received: user LGTM, plus subsequent explicitly recorded revisions, 2026-11-03.
- Architecture approval required before gameplay implementation, as specified by the selected workflow.
- No merge, official team publishing, dependency or build configuration changes authorized by this task.

## User Clarifications Recorded

- Ultimate Lv.4 revised: Goenitz's kills only, any action category while Frenzy applies; +3% ATK only, cap 10, permanent until hit. The +6 AE extension remains.
- Ultimate Lv.2 revised: all Frenzy actors' confirmed hits, self or allies, apply the debuff; no Ultimate restriction.
- Ultimate Lv.3 adds Blizzard: one refreshed 15-second self stack from Goenitz Ultimate/limit-break use; existing +10% package plus +10 ADI.
- Shin availability and ally evasion duration follow Frenzy exactly, without independent windows.
- Bleed 70% / 50% / 500% are one-shot damage, with optional inherited generic Bleed DOT from the original SO.
- Frenzy drains Goenitz's AE even off-field; allies share its duration.
- Goenitz cannot generate AE during Frenzy; only the extension effect increases it, by 6 AE.
- Yamidokoku remains available at 70 AE and unavailable below 70; casting spends 70 and shortens/ends Frenzy accordingly.
- Frenzy eligibility is remembered until the next damage hit resolves, so an Ultimate that removes/depletes Frenzy can still receive a qualifying kill reward.
- Requested Echo effects migrate to Physical damage / Physical resistance shred.

See the approved design for retained defaults and the architecture plan for proposed implementation contracts.
