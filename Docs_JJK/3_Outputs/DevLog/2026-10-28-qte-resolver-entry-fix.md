---
slug: qte-resolver-entry-fix
status: needs-human
source: manual
gdd_tags: [combat, combat-scope, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# QTE resolver entry correction

User stop/recovery — 2026-10-28: Apricha prohibited further Unity script/test runs after validation overwrote the custom plunge clip. Recovered its four authored sprite keys from pre-test Codex checkpoint aaf1e20c, preserving the current 30 FPS setting. Restored all seven Execution Breakdown NormalHitbox records/references from the saved pre-migration CharacterObject data. The migration had omitted references stored as list entries; the earlier nine-object equivalence check did not cover those references and was insufficient. Removed the added QTE Entry Fix validation menu. No Unity scripts/tests were executed during this recovery. Further QTE work remains unfinished; do not interpret compilation or the interrupted validation as a completed gameplay fix.

Latest user correction: QTE now shares the ordinary Team Swap Policy, and Dimension Slash's saved/authoring window is five seconds. QTE previously cleared on every OnTeamChanged notification, including health/stat/buff refreshes; it now checks roster/source ownership instead. The portrait pulse reflects the retained opportunity, while the normal unavailable overlay/input policy controls whether a switch can happen now. Team HUD avoids reconstructing identical slot bindings during stat refresh. This addresses newly prepared windows being erased by CameraGirl's buffs and ordinary health updates. Runtime and Editor assemblies compile; focused regressions for shared policy, refresh preservation, both CameraGirl Skill ids, readiness during cooldown and five-second Dimension Slash are compiled but not executed. [Current fix](../Specs/qte-window-policy-fix-task-card.md).

Main's Lightning Plunge now has a dedicated `main.qte.lightning_plunge` action in `ActionResolverTable.qteActions`. Its existing LightningPlunge → ExecutionBreakdown managed-reference tree moved from CharacterObject without changing its payload. The animation editor shows QTE setup/root/substeps before ordinary combos, supports their action-level controls, and records table Undo/dirty state when changing the clip. Runtime, preview and authoring use the same table-owned combo. Existing linked ordinary-attack QTE profiles remain supported.

The plunge body/path clearance checks now ignore finishing `PlayerTeamSwitchState` bodies, matching the existing grounded entry check. Such overlaps could block Main's entry after CameraGirl's Skill. Source trigger ids, accepted-hit/provenance filters and terrain checks retain their existing rules.

Runtime and Editor C# compilation succeeded using Unity's current Roslyn response files and isolated Temp outputs. Static checks verify complete managed references and byte-equivalent payloads for all nine migrated objects after id remapping. Existing regression references were migrated; no new Unity tests or automated test runs. Live Editor validation was unavailable because the MCP server had no connected Editor and native UI initialization failed. Gameplay confirmation remains in the [manual checklist](../TestPlans/main-qte-lightning-plunge-test-plan.md); see the [task card](../Specs/qte-resolver-entry-fix-task-card.md).
