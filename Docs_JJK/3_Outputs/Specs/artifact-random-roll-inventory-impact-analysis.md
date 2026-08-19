---
slug: "artifact-random-roll-inventory"
status: approved
source: manual
gdd_tags:
  - "ui"
  - "progression"
  - "items"
owner: "architect-agent"
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Impact Analysis: Artifact Random Rolls and Inventory Discovery

## Summary

This is a save-schema and identity refactor, not only a stat generator. The current Artifact ScriptableObject is simultaneously definition, owned item, equipped reference, shop item, passive source, and fixed stat container. Introducing duplicate rolled instances changes every path that relies on reference equality or definition IDs.

## Impact Matrix

| Area | Current assumption | Required change | Risk |
| --- | --- | --- | --- |
| Artifact assets | Definition owns fixed `bonusStats` | Definition owns reusable content only | High: asset/reference migration |
| Run ownership | One non-stackable SO reference | Many instances per definition | High: save authority |
| Acquisition | Duplicate Artifact → fragments | Every acquisition generates instance | High: economy behavior |
| Meta Progression | Stores Artifact Transcendence fragments | Character Potential only | High: selective migration |
| Team slots | Store `Artifact` references | Store instance IDs | High: duplicate identity |
| Team draft/profile | Copy definition arrays | Copy instance-reference arrays | High: stale/orphan IDs |
| Deployment | Passes definition arrays | Resolves immutable runtime snapshots | High: ADR-005 boundary |
| Runtime stats | Reads `artifact.bonusStats` | Central roll aggregator | High: numerical regression |
| Artifact passives | Reads effects from equipped SO | Reads effects from snapshot definition | Medium |
| Shop sell | Sells definition ownership | Sells selected instance | High: wrong-copy deletion |
| Grid/preview | One card per definition | One card per instance | High: UI population growth |
| Sorting/filtering | None | Pure query engine/view models | Medium |
| Favourite | None | Persistent per-instance boolean | Medium |
| Damage | Float result with UI-side rounding | One integer result after float resolution | High: broad combat regression |
| Training Debug/UI | Independently formats floats | Uses resolved integer | Medium |

## Serialization and Data Risks

- Renaming `Artifact` without preserving its `.meta` GUID would break all nine asset script references.
- Saving only `definitionId` in a slot would make duplicate instances indistinguishable.
- Unity asset references and JSON instance records need separate migration handling.
- Current v2 saves may contain Artifact IDs in both owned-item records and team-slot records; migration must deduplicate them into one logical instance.
- Legacy stat values can fall outside new generation ranges and must not be clamped or assigned fabricated quality.
- Hidden SPD -1 exists on `a_4_Memory-Draining Urn` and would be lost by a buildable-only converter.
- Removing enum values before reading Meta v1 could reinterpret serialized fragment ordinals.

## Behavioral Risks

- Multiple copies can activate the same passive; passive stacking must remain explicitly authored.
- Changing stat aggregation order can alter HP/ATK/DEF percentage results if the clean base snapshot is not shared across equipped Artifacts.
- Favourite Mode changes card-click meaning and must suppress preview/drag while active.
- Query sorting by average quality alone can incorrectly imply that a one-line Artifact is stronger than a four-line Artifact; line count remains separate.
- Damage integer conversion can change low-value hits, DOT replacement tests, on-hit thresholds, total damage, and death timing.

## Performance Risks

- Large duplicate collections make Instantiate/Destroy grid rebuilding unsuitable; pool or virtualize cells.
- Avoid LINQ and closure allocations in query execution and stat aggregation.
- Rebuild instance/definition dictionaries on inventory mutation, not per card or hit.
- Runtime Artifact snapshots and effective stats are built on Deploy/stat revision, not per damage event.
- Damage remains allocation-free and quantizes without boxing/string creation in resolver/processor.

## Compatibility Strategy

1. Add new contracts beside legacy fields.
2. Dry-run both asset and save migrations.
3. Migrate readers before writers where dual-read is required.
4. Write only v3 after full validation; retain v2 rollback input.
5. Validate all assets, saves, profiles, prefabs, and scenes.
6. Remove legacy fields/adapters in a separate cleanup commit.

## Required Regression Coverage

- Existing nine Artifact numeric effects before versus imported instances after migration.
- Existing passive and Additional Damage sources after snapshot conversion.
- Team draft cancel/deploy, cross-character swaps, empty slots, and scene reload.
- Same-definition duplicate acquisition, favourite, independent equip, save/load, and sell.
- Flat/percentage stat application and repeated same-stat lines.
- Primary/DOT/Additional integer damage, critical results, mitigation, death, AE/on-hit eligibility, UI, and Training Debug totals.
- Character duplicate fragments and Potential progression unchanged.

## Rollback

- Do not overwrite or delete the v2 run-save key during the migration release.
- Do not remove legacy asset fields until migrated assets and references validate.
- If v3 read-back validation fails, log the report and continue from v2 without saving partial v3 state.
- Asset migration uses a reviewed dry-run report and source-control recovery; no automatic destructive cleanup.
