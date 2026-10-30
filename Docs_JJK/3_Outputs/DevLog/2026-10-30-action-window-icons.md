---
slug: action-window-icons
status: approved
gdd_tags: [ui, combat, platform-input]
owner: Codex
human_checkpoint: requester manual feel review
next_agent: human
blocked_by: []
---

# Action window icons and stable rejection feedback

Requested by Apricha on 2026-10-30. Design and architecture approved in chat before
icon implementation. GDD reference: @tag:ui HUD readiness/follow-up windows,
@tag:combat and @tag:platform-input for existing gameplay ownership.

Added optional `CombatAction.icon` Sprite and the Animation Editor's Action Icon
field. AbilityUIManager previews existing resolver choices on NA/Skill/Ult input
slots, swaps artwork during available opportunities, reads the selected action's
existing passive timer, and restores cached prefab art when no icon is authored.
NA's existing image and window overlay are now wired. Existing Dash art/timer stay
movement-owned. Ultimate-type Judgment Cut stays on Skill as approved.

Assigned Main's existing Shin art to ground/air normal attacks, Dimension Cut,
Dark Chain, Judgment Cut and Jailbreak Punisher. Missing art retains prefab fallback.
Prefab preview exposed an opaque window fill hiding the replacement image; the
fill now follows the action silhouette while retaining its existing tint/timing.

Fixed dash rejection drift in UIFeedbackDriver: each target has one interruptible
wiggle and a stable original anchored position. Completion and driver disable
restore that position; destroyed targets end quietly.

Verification: 19 focused Unity EditMode tests passed, including the existing
ability feedback suite, new resolver/window/fallback/resource checks, repeated
wiggles at simulated moving offsets and real Main prefab renders. Tests model
the disable lifecycle explicitly in EditMode; manual input replay remains pending.
Results/previews: `Reports/ActionWindowIcons/`.
Checklist: `TestPlans/action-window-icons-test-plan.md`.
Compile completed with no compiler errors; existing obsolete-API warnings remain.
The existing Training focused suite also passed: 72 passed, 0 failed, 0 skipped.
Its original suite was rerun because the pre-existing global Training result
callback also writes its report after other suites; this restores correctly
scoped Training evidence without changing that user's validation runner.

No changes to input routes, Action Type behavior, commitment/cost logic, gameplay
timing, build/CI/dependencies/secrets, or other authored asset contents beyond the
listed icon references and NA wiring. Existing local work remains in place.
No commit, push, merge or official team status publishing was performed.
