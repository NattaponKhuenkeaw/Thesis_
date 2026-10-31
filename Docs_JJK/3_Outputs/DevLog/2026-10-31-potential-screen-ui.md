# Potential Screen UI — 2026-10-31

GDD: `@tag:ui` Character Potential Panel; `@tag:progression` Permanent Potential Upgrade.
Task: `../Specs/potential-screen-ui-task-card.md`. Workflow: `/refactor`.

## Result
- Kept the existing node groups and right-side preview. Removed the proposed character/profile section after user feedback.
- Restored bottom-to-top level ordering (I → IV); disabled layout components that otherwise reordered nodes. Retained a clamped, draggable ScrollRect and viewport.
- Wired Main's Skill/Ultimate/Passive Potential sprites, shared UI frames, selection outline and state labels. Other characters use their kit ability icons.
- Preview retains authored descriptions, supports wrapped/scrollable content, and adds explicit close/cancel actions. Closing clears selection and cancels queued confirmation.
- Omitted fragment currency presentation and node-state legend; retained existing upgrade eligibility and fragment-spending authority.
- Changes use the pre-existing uncommitted progression/training integration as their starting point. No scene, balance, build or dependency changes were authored for this task.

## Validation
- Unity compiled without errors.
- Five focused `PotentialScreenTests` passed: insufficient resources / sequential gating, successful single spend, character switching / icon fallback, cancel dismissal, and bottom-to-top scrollable layout.
- Inspected screen and right-side preview renders at 1600×900 and 1200×900.
- Broader initial run: ten passed, one failed. `CameraGirlPotentialTests.CameraGirl_SkillTrack_ConfiguredCorrectly` expects 33 where the existing authored value is 43. That fixture and character data were not changed.
- Reports: `../Reports/PotentialScreen/`. The editor commands under `Project/UI` can reapply the scoped layout, capture previews and run focused tests.

Human visual review remains pending. No commit, merge or publishing performed.
