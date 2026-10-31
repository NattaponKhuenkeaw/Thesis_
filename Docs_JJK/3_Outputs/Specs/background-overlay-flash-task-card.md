---
slug: background-overlay-flash
status: approved
source: manual
gdd_tags: [visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Background overlay flash and camera impulse preservation

Workflow: `/implement-feature`. Source: user's 2026-10-31 request for Fade In / Hold / Fade Out / Color and investigation of camera position control overriding HitData and ActionEventData impulses. The requested configurable phases authorize this bounded implementation within the existing presentation systems; subjective feel remains for human review.

GDD `@tag:visual-audio`: “Use large slash arcs, burst circles, impact flashes, hit-stop, and camera shake to sell attacks.” ADR-016 retains CameraManager ownership, room bounds and additive presentation. No new camera authority is introduced.

## Acceptance

- Editable nonnegative Fade In Frames, Hold Frames, Fade Out Frames, existing Flash Color and Peak Alpha.
- Runtime, timeline length and rescaling use all phases; zero-length phases skip safely.
- Existing assets preserve instant peak, no hold and their saved fade-out duration. No content retuning.
- Camera position animation/legacy lock preserves impulse displacement and rotation after confinement.
- Preserve hit acceptance/suppression, action interruption, overlay priority and unscaled flash timing.
- Verify compilation and focused EditMode regressions; human gameplay feel review remains pending.
