---
slug: training-tools-drawer
status: needs-human
source: manual
gdd_tags: [ui, scene-roles]
owner: implementation-agent
human_checkpoint: approved
next_agent: human
blocked_by: []
---

# Training Tools Drawer

User direction, 2026-10-30: remove duplicated TeamBuilder/Inspector controls because Player_UI already provides them. Put Training Config, Reset DMG and Stats in a vertical panel behind a Config button on the left. Open by sliding in from the left and close by sliding right, both ease-out.

Workflow: `/implement-feature` (this changes presentation/interaction behavior, despite the user's general use of "refactor"). This is a scoped refinement of the approved Training UI; data ownership and zone permissions are unchanged.

## Design and Implementation

- Reuse Player_UI's Builder and CharacterInspector buttons with their existing authored callbacks. Remove only duplicate direct children of TrainingCanvas and their redundant routing code.
- One Config button remains visible on the left below the currency HUD. A vertical panel below it contains Training Config, Reset DMG and Stats, starts hidden, and toggles on the Config button.
- Slide from a left-hand offset into its resting position; close rightward. Use cubic ease-out for position and opacity in both directions, in unscaled time. The existing menu's 0.2-second reveal is a starting value; verify completion/readability and rapid reversals in Play mode, adjusting the duration only if they are hard to follow.
- Drawer presentation belongs to TrainingToolsDrawer. TrainingConfigurationPanel continues to own level/potential editing. Real TeamBuilder/Inspector navigation remains in Player_UI/UIManager.
- Opening the full configuration modal or a loadout/pause panel closes the drawer. Stats selection closes the drawer and shows/hides stats without resetting collection. Reset DMG retains its existing behavior.
- The drawer does not pause or lock gameplay globally. UI click blocking stays on TrainingCanvas. Hidden content must not block clicks; animations must cancel cleanly and not accumulate position offsets.
- Keep the Config button accessible when stats are shown; temporarily suppress stats rendering while the drawer is presented.

## Validation

- Play-mode flow clicks actual Player_UI callbacks to access TeamBuilder and Inspector.
- Entry shows only Config, with no duplicate loadout buttons under TrainingCanvas.
- Open and close at zero timescale, checking direction, settled visibility/input state and rapid open/close reversals.
- Capture default, drawer-open and stats views at 1920x1080.
- Continue existing Training checks for configuration, damage, equipment, camera and persistence isolation.

Human game-feel review remains the existing Training checkpoint; the user has explicitly specified the requested interaction and motion direction.

## Completion

Implemented and verified: **73 Unity tests passed, 0 failed, 0 skipped**. Default, drawer-open and Stats captures reviewed. The integration test now clicks the real Player_UI callbacks and verifies animation direction/settling at zero timescale, reversal safety and hidden raycasts. Missing native scene event targets were repaired without modifying the shared prefab. Remaining work is the existing human playtest; no approval is needed for the completed implementation.

## Direction Correction

Follow-up user feedback: the entry side was wrong. The hidden drawer now starts left of its resting position and opens rightward into place. It still exits rightward when closed. The drawer test direction assertion was updated; this follow-up has not been rerun through Unity tests yet.

Subsequent stats/configuration fixes are tracked in `training-stats-and-config-bugfix-task-card.md`.
