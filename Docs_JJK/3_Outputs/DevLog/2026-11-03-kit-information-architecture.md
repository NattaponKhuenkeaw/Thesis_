---
slug: kit-information
status: needs-human
source: manual
gdd_tags: [ui, characters, progression]
owner: architect-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# DevLog: 2026-11-03 — Kit Information Architecture Discussion

## Goal

Assess reliable ownership for multiple actions per category, QTE/standalone information, and Potential overrides.

## What I Did

- Inspected current panel, action definitions/table, runtime modifier evaluation, progression definitions, QTE references, and Main's authored data.
- Drafted `../Specs/kit-information-task-card.md` and `../Specs/kit-information-arch-plan.md`, including impact analysis and an unnumbered proposed ADR.

## Proposed Decisions

- Base action explanation belongs to CombatAction; sections/passive entries belong to kit presentation data; per-entry Potential patches feed a read-only information builder.
- Preserve current combat routing and action lists, including the withdrawn ADR-017 boundary.
- Extract numeric evaluation with runtime parity and represent unknown combat conditions explicitly.

## Findings

- Current panel only selects the first Skill and Ultimate, filters numeric upgrades by display track, and ignores runtime conditions while evaluating modifiers.
- QTE may reference an existing ordinary action; UI section identity must be separate from gameplay category.

## Verification and Game Feel

Source and asset inspection only. Documentation changes only; no Unity playtest or implementation claimed. Existing unrelated working-tree edits were left untouched.

## Implementation After Approval

The user approved the proposal with "lgtm". Implemented per-action copy, explicit kit sections/entries, targeted Potential patches, shared runtime value evaluation, nested card rendering, and event-based progression refresh. Added authoring fields to the action editor and migrated nine hero definitions while preserving existing execution payload lines and legacy serialized fields. CameraGirl's QTE name reference was converted to its equivalent stable action ID.

ADR-019 and the GDD UI section record the accepted ownership. The legacy description fields remain available for compatibility. Existing effect-specific narrative numbers remain authored content; scalar bindings support cooldown, AE cost, action scaling, and source stat.

## Final Verification

- Unity compilation completed without C# errors.
- 16 native validation groups passed, 0 failed; all nine heroes at tiers 0–4.
- Inspected wide and 4:3 previews, long text, nested sections, and bottom scrolling.
- Audited 18 assets against the pre-migration workspace snapshot. Original payload lines remain, except missing action IDs, standalone first-field indentation, and the equivalent QTE key migration.
- No active scene/prefab asset was saved. Existing unrelated workspace edits remain untouched. No gameplay playtest, commit, merge, or publishing is claimed.

## Follow-up

Human visual/gameplay review, then normal PR review if requested. Secondary-action prose and effect-specific narrative values can receive a separate content pass.

## User Follow-up: Potential Filtering and Header Badges

Implemented the user's explicit revision: root Lv.0–4 labels, Potential-driven visibility, no ordinary Normal Attack entries, and compact Ground/Airborne/Both, cooldown, and AE header badges. Learned actions stay visible through temporary runtime unavailability. Existing section data now explicitly owns track associations, with legacy entry association fallback retained.

Main's Dimension Slash reads its existing Passive 4 action condition. Two effect-granted follow-ups have presentation-only minimums backed by their existing Potential nodes: Rugal EX (Passive 4 / Omega Mode) and CameraGirl Shadow Assist (Ultimate 1 / Phantom Shutter). No gameplay unlock rule is changed.

Native validation now passes 20 groups with no failures. Checked all nine hero definitions at each tier and inspected wide/4:3 Lv.0 views. Tests render against temporary progression objects; persistent progression is not edited. Unity Play Mode was stopped for compilation/native preview validation; the active scene was not saved.

## Bug Found: Main Shadow Meter Action Keys

The stable action IDs added during kit migration made Main's legacy meter rules (`Ground` / `Air`) stop matching: meter evaluation prefers a non-empty action ID, while the rules still held the old action names. Updated the migration to re-key uniquely matched legacy meter rules to stable IDs and applied that conversion to Main's existing data. Meter values and runtime hit handling are unchanged. Manual gameplay regression remains unplayed.
