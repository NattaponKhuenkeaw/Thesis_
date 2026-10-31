---
slug: dash-feedback-drift
status: approved
source: manual
gdd_tags: [ui, combat]
owner: Codex
human_checkpoint: review after maintenance fix
next_agent: human
blocked_by: []
---

# Dash rejected-feedback drift

Workflow: `/fix-bug`. Source: Apricha's direct request on 2026-10-30.

Player goal: repeated rejected dash inputs wiggle the slot without moving its resting position.
GDD: `GDD_Neo-JJK.md` @tag:ui, Combat HUD and Runtime Feedback; existing feedback
behavior is recorded in `ability-ui-zone-feedback-task-card.md`.

Root cause: AbilityUIManager supplies the currently animated anchored position to
every new wiggle. UIFeedbackDriver starts overlapping coroutines with different
origins, so the final writer can retain an accumulated offset.

Scope: UIFeedbackDriver's existing wiggle lifecycle and focused regression coverage.
Keep the existing animation distance, duration and callers. No architecture or feel tuning.
Preserve all pre-existing working-tree edits. Feature work is tracked separately.

Acceptance:
- Repeated feedback interrupts the previous wiggle and retains its original resting position.
- Completion and driver disable restore the original position.
- Separate targets remain independent; destroyed targets end quietly.
- A later wiggle after layout movement uses the new resting position.
- Unity compiles and focused feedback tests pass; manual dash feel review remains with the requester.

Implemented in UIFeedbackDriver. Focused test results are recorded alongside
the icon work in `Reports/ActionWindowIcons/`.
