---
slug: action-window-icons
status: approved
gdd_tags: [ui, combat, platform-input]
owner: Codex
human_checkpoint: manual feel review
next_agent: human
blocked_by: []
---

# Scoped self-review

Reviewed against the requester-approved design and architecture. No outstanding
implementation findings in the scoped diff.

- CombatAction owns optional presentation metadata; the Animation Editor uses
  existing Undo/dirty handling. Resolver selection, input categories and commitment
  are unchanged. Existing Action Type changes remain intact.
- HUD previews share one selected action identity for its icon/window. Gameplay
  owns duration, cooldown, AE and counters; UI queries do not commit them.
- Prefab fallback sprites are cached before replacement and restored across swaps
  or missing art. The selected icon silhouette keeps the existing window tint legible.
- Dash continues to read movement. Repeated wiggles stop their predecessor, retain
  the original position and restore it on completion or driver disable.
- Existing NA images/window overlay were wired without changing hierarchy/layout.
- Main's clearly named existing Shin sprites were assigned to six existing actions.
  Unassigned art, including the hold Ultimate, uses the prefab fallback.
- Pre-existing working-tree changes are retained. No new dependencies, build/CI
  settings, secret changes, merge, push or team status publishing.

Verification: focused Unity tests and prefab renders are recorded in
`Reports/ActionWindowIcons/`; manual PC input/feel checks are in
`TestPlans/action-window-icons-test-plan.md`.
