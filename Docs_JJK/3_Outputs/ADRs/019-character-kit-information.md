---
slug: kit-information
status: approved
source: manual
gdd_tags: [ui, characters, progression]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# ADR-019: Character Kit Information Ownership

| Field | Value |
|---|---|
| Status | Accepted; implementation authorized by user "lgtm" |
| Date | 2026-11-03 |
| GDD | `@tag: ui`, `@tag:characters`, `@tag:progression` |

## Context

CharacterSkillPanel selected the first Skill and Ultimate and applied one whole-track description. This cannot explain multiple actions, contextual standalone actions, or QTE usages independently. Its local numeric evaluation also diverged from combat.

## Decision

- CombatAction owns base description and activation copy in the existing action table. Its existing name/icon remain authoritative.
- CharacterKitDefinition.presentation owns ordered sections and entries. An entry references actionId; its own entryId allows one action to have a separate QTE explanation. Passive is a non-action entry.
- PotentialNodeDefinition.kitInfoPatches targets individual entry IDs. Explicit priority resolves replacement copy; ties are diagnostics and retain base copy. Named additive notes are ordered deterministically.
- CharacterKitInfoBuilder produces read-only section/entry data from a caller-supplied KitInfoContext. The panel only builds and binds UI and subscribes to progression changes.
- ActionModifierEvaluation supplies existing cooldown, energy, and action-scaling operations to combat and inspection. Reference views exclude all unknown runtime requirements, including negative conditions, and describe them separately.
- Scalar bindings are `{cooldown}`, `{energyCost}`, `{multiplierPercent}`, and `{sourceStat}`. Action scaling is not a claim of total action damage. Effect-specific prose remains authored; no combo simulator or arbitrary expression language is introduced.

## Alternatives

Runtime ActionResolver ownership couples menus to combat state. Whole-track nesting still cannot independently describe actions. A duplicated execution catalog creates another gameplay authority. Keep the current five lists and their routing.

## Consequences and Migration

Presentation needs explicit entry IDs and validation. Legacy strings remain serialized and are mapped only to the intended primary action/passive. An explicit editor migration authors existing characters and assigns missing action IDs. Existing execution payloads and legacy serialized fields are preserved. New characters can author their presentation directly; an unmigrated fallback remains available.

No previous ADR is superseded. Withdrawn ADR-017's unified runtime catalog remains out of scope. ADR-018 continues to govern Training progression context.

## Related

- `../Specs/kit-information-arch-plan.md`
- `../TestPlans/kit-information-test-plan.md`

## Approved UI Revision (2026-11-03)

The user requested Potential filtering, root Lv.0–4 labels, compact header tags, and removal of ordinary Normal Attack entries. Section definitions may explicitly own their Potential association; legacy primary-entry associations are read for compatibility. Progression level labels and direct Potential gates use the supplied progression state, rather than inferring level from which modifier nodes happen to exist.

Only permanently learned entries are rendered. Effect-granted follow-ups may author a presentation-only minimum level/track (Rugal EX: Passive 4; CameraGirl Shadow Assist: Ultimate 1). Runtime tags/counters remain activation conditions and do not hide learned entries. Combat definitions are unchanged. Structured header tags carry context/cooldown/AE independently of prose and use code-drawn symbols.

## Authored Copy Revision (2026-11-03)

The user requested removal of auto-generated activation and conditional-upgrade prose. The builder now displays activation text only when explicitly authored on the entry or non-QTE action. It does not synthesize QTE/Ultimate/target/resource activation sentences or `Conditional upgrade` notes. Conditional modifiers remain excluded from unconditional values.
