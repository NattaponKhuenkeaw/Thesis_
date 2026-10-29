---
slug: screen-impulse-camera-control
status: draft
gdd_tags: [architecture]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Screen Impulse Camera Control verification

Compilation: runtime and editor assemblies passed using the current Unity response files, with outputs isolated in a temporary folder. NUnit tests compile; execution is pending.

Run EditMode `ScreenImpulseCameraTests` plus the camera tests in `MainDimensionSlashTests`.

## Action Camera Motion follow-up

- Trigger at action frame 20 with a first relative key at 10: framing must stay neutral until action frame 30. Recheck with multiple unsorted keys.
- In a sufficiently wide room test horizontal offset ±0.5 with either camera type; movement should equal half the rendered screen width relative to captured facing. At room edges, confinement must limit it. A room exactly filled by the viewport has no room for a pan.
- Author a non-neutral final key with hold and return frames. Confirm hold begins at that key, return reaches neutral, and the camera claim releases. Zero return cuts back on the next frame after the hold.
- Confirm the new timing fields rescale and timeline length updates; existing zoom key values persist.

## Single-zoom clarification (supersedes final-key hold and step-end cleanup above)

- Open the event to show the single-target controls. Verify legacy strongest-zoom conversion, Undo/Redo and persistence.
- Set target 0.8, transition-in 10 frames, peak hold 1000 frames, return 100 frames. Peak begins after transition and lasts the complete hold, even after natural action completion. Return then blends to normal scale; it must not jump back after 5–10 frames.
- Normal auto-chain steps must not cancel the envelope. A new camera event replaces it; accepted combat interruption, hitstun, death, swap, room change, Ultimate/focus, or manager disable must clear it.
- Pause/hitstop pause the envelope; slow motion advances it with scaled gameplay time. At LOGIC_FPS 60, 1000 hold frames represent about 16.67 gameplay seconds, excluding transition and return.
- Run `ScreenImpulseCameraTests` and `MainDimensionSlashTests.TimedZoomSurvivesNormalActionEndAndReturnsAfterAuthoredHold`.

Manual checks pending:

- In Animation Editor > Screen Impulse > Camera Control, enable the feature. Verify fields persist and Undo/Redo works.
- Author multiplier below 1 and above 1, transition/hold/return frames fitting within the step. Verify zoom-in/out and restoration to current framing. Defaults are neutral; existing assets should retain their behavior.
- Enable Jump Cut (Lock Position), move the actor, and verify framing holds immediately. Return must approach the current normal framing, including when the actor moved during the hold.
- Test zero transition/return as cuts, a hold longer than the step, and hitstop during each phase. Hitstop must pause envelope progress and step end must restore framing.
- At each room edge, test zoom-out beyond the room size; no outside-room area should be revealed. Unsupported rotated/polygonal bounds must retain base composition.
- Interrupt, swap, die, change room, trigger Ultimate/focus, and replace with another camera event. No stale lock or lens effect should survive; stale releases must not clear the replacement.
- Keep shake strength at zero to inspect the lock alone, then verify shake and slow motion still work independently.
- Human feel review: nearby threats remain visible, cuts communicate the action, and player control remains responsive. Reduce zoom-in/hold or disable lock on any action that hides needed threats.
