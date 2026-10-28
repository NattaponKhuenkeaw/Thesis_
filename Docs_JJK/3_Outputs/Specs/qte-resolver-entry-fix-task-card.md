---
slug: qte-resolver-entry-fix
status: draft
source: manual
gdd_tags: [combat, combat-scope, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# QTE resolver entry and CameraGirl handoff correction

Latest user constraint/recovery (2026-10-28): no further Unity script or test execution. The migration missed hitbox list references; restored Execution Breakdown's seven records/references from the pre-migration saved CharacterObject. Validation's authoring calls overwrote the custom plunge clip; restored its four sprite keys from pre-test checkpoint aaf1e20c with the current 30 FPS preserved. Earlier statements that the migration fully preserved its payload were incorrect because the check omitted list references. Further handoff validation/work remains unfinished.

Source: Apricha reports a missing QTE root in ActionResolver, Main's new QTE still using `main.dash.dimension_cut`, and CameraGirl Skill failing to trigger the prepared entry. Workflow: `/fix-bug`. This implements the requested identity/authoring correction to the approved Main Lightning Plunge; subjective gameplay review remains human-owned.

Main's QTE used a CharacterObject-owned combo while borrowing Dimension Cut's CombatAction. Move its existing managed-reference subtree into a dedicated `ActionResolverTable.qteActions` entry with id `main.qte.lightning_plunge`. Keep the existing authored payload, follow-up, damage budget, placement/VFX/camera settings and manual cut. Dedicated QTE entries remain outside ordinary input selection. Display their setup/root/substeps at the top of the animation editor's resolver categories; retain linked legacy entries for profiles that reuse an ordinary attack.

Runtime and authoring/preview utilities resolve the same table-owned combo. Authoring regenerates the dedicated identity instead of reconnecting to Dimension Cut. Existing profiles continue to resolve their referenced ordinary action.

CameraGirl's configured triggers remain `cg.skill` and `cg.skill.ex`. The plunge clearance checks omitted the outgoing `PlayerTeamSwitchState` exclusion already used by grounded QTE placement, so an overlapping finishing body could reject preparation or abort descent. Apply that same exclusion to plunge body/path checks; terrain collision rejection remains active.

Acceptance: Main exposes LightningPlunge → ExecutionBreakdown under QTE Entry; the QTE has its own action identity and no cached-cut cost; CameraGirl's successful Skill/EX hits prepare Main and accepted swaps can descend through finishing bodies. Verify both facings, repeated swaps, a solid roof and unsafe landing terrain.

Validation: compiled runtime and Editor assemblies with the project's current Unity Roslyn response files, writing isolated outputs under Temp. Static serialization checks confirm no missing/duplicate managed references and all nine migrated objects match their previous payloads after id remapping. Existing test references/obsolete entry assertions were migrated; no automated Unity tests were added or run. Live Editor validation was unavailable: MCP server has no connected Unity session and native UI capture could not initialize (`failed to write kernel assets`). Gameplay acceptance remains unverified.
