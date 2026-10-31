---
slug: training-tools-drawer
status: needs-human
source: manual
gdd_tags: [ui, scene-roles]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Training Controls Drawer

- Removed duplicated TrainingCanvas TeamBuilder/Inspector controls and their extra controller routing. Existing Player_UI buttons remain the loadout entry points.
- Added a left-side Config toggle with a vertical Training Config / Reset DMG / Stats drawer. It starts closed, slides in from a right offset, slides right on close, and uses cubic ease-out for motion/fade in unscaled time.
- Extracted drawer presentation into TrainingToolsDrawer. Level/potential configuration remains in TrainingConfigurationPanel. Hidden content releases raycasts, rapid toggling reverses from the current pose, and modals/pause close the drawer.
- The actual Player_UI click regression revealed missing Training scene event targets. Scene authoring now repairs those existing button callbacks through UIManager while retaining unrelated authored callbacks and the shared prefab.
- Added Play-mode animation direction, zero-timescale, rapid reversal and raycast checks, plus clicks through the existing Player_UI persistent events. Captured the drawer-open view.
- No inventory, camera, zone or persistence authority changes; existing unrelated prefab/Lobby edits are preserved.
- Final Unity run: **73 passed, 0 failed, 0 skipped**, including real Player_UI callbacks, timeScale-0 drawer motion, rapid reversals, hidden raycasts, and all existing Training persistence checks. Screenshots reviewed at 1920x1080. Remaining human review is motion feel/controller ergonomics and other resolutions.

## Training Stats / Config Follow-up

- Stats rendering no longer returns early when the full configuration modal opens, so its visibility preference stays enabled beneath the overlay.
- Team swaps and Training setting changes no longer clear accumulated damage/DPS. Team damage rows are retained and new roster members are added; Reset DMG is the explicit reset.
- Training potential lookup now defaults to zero for all characters while the temporary session is active, and the temporary shared team level starts at 1. Saved progression is restored when the session ends.
- The Training configuration overlay is reparented to a separate root Screen Space Overlay Canvas at sorting order 1000, with its own GraphicRaycaster so it renders and receives input above the HUD.
- Automated tests were not run for this follow-up.
