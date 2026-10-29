# Main: Execution Breakdown as hold Ultimate

Moved the saved Execution Breakdown node from Lightning Plunge's QTE follow-up into the second Ultimate entry, preserving its complete authored reference graph. Tap retains Jailbreak Punisher or the ready Judgment Cut; hold selects Execution Breakdown once. Ultimate actions now author their input trigger, hold seconds and Ultimate-cancel permission. The input buffer retains the gesture through attack/jump/phase routing, while characters without hold actions retain immediate-press behavior. Starting threshold: 0.35 unscaled seconds, with the manual tuning procedure in the task card.

Moved Judgment Cut from Skill to Ultimate while retaining its stable backend ID. Updated its passive category filter/modifier and five-second window description. Disabled its Ultimate-cancel permission and prevented a ready window from overriding the current Ultimate's phase gate. Existing authoring source now preserves saved Ultimate nodes and no longer reconstructs the Execution Breakdown QTE chain. Free authored Ultimates also honor their zero cost when no TeamLoader exists.

Validation: read-only diff/reference review; all 139 managed-reference records remain, with only the QTE root's next-node link changed and no unresolved positive RIDs. Animation files and all Execution Breakdown supporting records are unchanged. Updated test source, including removing asset-authoring calls from its fixture, without executing it. No Unity scripts, authoring tools, compiler commands, tests or Play mode were run. Runtime input and feel review remain with Apricha, per the standing restriction.

Task: `Docs_JJK/3_Outputs/Specs/main-hold-ultimate-task-card.md`.
