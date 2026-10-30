---
slug: action-window-icons
status: approved
gdd_tags: [ui, combat, platform-input]
owner: Codex
human_checkpoint: manual feel review
next_agent: human
blocked_by: []
---

# Validation: action window icons and dash rejection drift

2026-10-30, Unity 6000.3.4f1, Windows Editor.

- Ability icon/feedback tests: **19 passed, 0 failed, 0 skipped**.
  Result: `ActionWindowIcons/tests.xml`; summary: `ActionWindowIcons/test-summary.txt`.
- Existing Training regression suite: **72 passed, 0 failed, 0 skipped**.
  Result: `TrainingScene/tests.xml`.
- Script compilation succeeded; no compiler errors. Existing obsolete API warnings remain.
- Visual review of `ActionWindowIcons/main-ordinary.png` and
  `ActionWindowIcons/main-judgment-cut-window.png`: existing HUD layout retained,
  Main's ordinary Skill and replacement pictures render, and the timed fill uses
  the selected icon silhouette rather than obscuring it with a solid rectangle.
- Scoped self-review and whitespace check completed.

Automated tests use isolated character/table/progression objects and preview-prefab
contents. Wiggle stress tests simulate requests at moving offsets and explicitly
exercise the disable callback in EditMode. They do not replay live player input.

Manual PC gameplay/feel checks remain in `TestPlans/action-window-icons-test-plan.md`.
No merge, push, official team publishing or configuration/dependency changes.
