---
slug: kit-information
status: approved
source: manual
gdd_tags: [ui, characters, progression]
owner: architect-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Character Kit Information

## Player-Facing Goal

Explain every authored player-facing action, including multiple actions in one category, QTE entries, contextual standalone actions, and passive effects, with accurate Potential-dependent information.

## Source

- Origin: user's architecture discussion in Codex, 2026-11-03.
- Requested by: project owner.
- Architecture and nested layout approved by the user ("lgtm"), 2026-11-03; implementation completed and natively verified.

## GDD Reference

- `Docs_JJK/1_Inputs_Templates/GDD_Neo-JJK.md`, `@tag: ui`: Character Skill Panel explains Basic Attack, Skill, Ultimate, Passive, and Potential-dependent values.
- `@tag:characters`, `@tag:progression`: character kits and independent permanent Potential tracks.
- QTE/Special sections are proposed extensions to the current UI design.

## Type and Router Decision

- Initial workflow: `/refactor`, through impact analysis and architecture proposal.
- Following user approval, selected workflow: `/implement-feature`, completing the approved multiple-entry UI slice and its supporting extraction.
- Runtime modifier extraction preserves existing combat semantics; additional sections are the approved UI behavior extension.
- References: `kit-information-design-spec.md`, `kit-information-arch-plan.md`, ADR-019, and `../TestPlans/kit-information-test-plan.md`.

## Scope

- Inspect CharacterSkillPanel, CombatAction, ActionResolverTable, ActionResolver, CharacterKitDefinition, PotentialNodeDefinition, CharacterPassiveController, and QteSwapDefinition.
- Propose presentation ownership, targeted overrides, shared numeric evaluation, and an incremental migration.
- Out of scope: combat routing redesign, action list unification, cost changes, save migration, dependency/configuration changes, and publishing. Existing prefab wiring is reused.

## Proposed Acceptance Criteria

- [x] Main exposes both Ultimates as independently described entries.
- [x] QTE and player-facing standalone actions are representable without new Potential tracks.
- [x] An upgrade can update one entry without overwriting another action's description.
- [x] Active upgrades across all tracks contribute to matching actions.
- [x] Conditional bonuses are excluded from unconditional values without generating a conditional-note phrase.
- [x] Menu and Training use CharacterStateContext at the panel boundary, without requiring a deployed player.
- [x] The builder labels verified action scaling and does not infer total damage from a first hit.
- [x] Runtime operations/clamps pass native parity checks; action execution payload lines are preserved. Manual gameplay playtest remains follow-up.

## Human Checkpoints

- Architecture and information hierarchy approved, 2026-11-03.
- Nested UI layout approved in the same user response; rendered previews provided for final visual review.
- Normal PR review before merge; no publishing is authorized.

## User Revision: Potential Filtering and Compact Headers

Authorized directly by the user's follow-up on 2026-11-03. Workflow remains `/implement-feature`.

- Hide Potential-locked/replaced entries; keep learned actions visible during temporary combat unavailability.
- Show current Lv.0–4 at the nesting root for its associated Potential track.
- Hide ordinary Normal Attack information, while preserving QTE reuse and standalone action entries.
- Present Ground/Airborne/Both, cooldown, and AE cost as compact header badges.
- Verify Main's Dimension Slash is absent below Passive 4, both legacy/new root-level associations, and native wide/4:3 rendering.
- Completed follow-up: removed generated damage-scaling prose from rich-text copy and restored the existing authored Potential descriptions; the builder no longer injects a damage-scaling sentence.
- Completed follow-up: removed auto-generated activation and conditional-upgrade sentences; authored activation copy remains supported.
