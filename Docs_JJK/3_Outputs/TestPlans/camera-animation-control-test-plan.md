---
slug: camera-animation-control
status: draft
source: manual
gdd_tags: [architecture]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Camera Animation Control verification

## Completed

- Runtime/editor compilation using Unity's bundled compiler and current response files, with isolated temporary outputs: passed. Existing unrelated warnings remain.
- Engine-independent compiled-assembly tests: 13 CameraAnimationSequenceTests and 9 ScreenImpulseCameraTests passed using actual NUnit assertions via a temporary .NET harness.
- Coverage includes event-relative delay, 1000-frame peak hold, live return, simultaneous channels, world/relative moves, facing, cuts, gaps, out-of-order samples, snapshot isolation, overlap rejection, invalid input, rescale, enum compatibility, conversion preservation and explicit impulse splitting.
- Scoped whitespace checks: passed.

## Native Unity execution pending

Run EditMode suites `CameraAnimationSequenceTests`, `ScreenImpulseCameraTests`, and these `MainDimensionSlashTests` methods:

- `CameraSequenceSurvivesNaturalCompletionAndStaleReleaseCannotCancelReplacement`
- `TimedZoomSurvivesNormalActionEndAndReturnsAfterAuthoredHold`
- `OlderCameraOwnerCannotSampleOrReleaseReplacement`
- `QteAndInvalidatedFieldExecutionCannotOwnActionCamera`

The native lifecycle tests compile but have not run. Unity MCP is unavailable; the computer-use helper failed to initialize twice with an OS path error.

## Authoring and playback checks pending

1. Open Animation Editor, select an action's Events card, and choose Add Camera Animation Control. Add segments with explicit local Start/End frames and tasks. No actor MotionFrameData should change.
2. Use an illustrative sequence: Zoom 0–10, Hold Zoom 10–40, Move 40–55, Hold Both 55–70, Return Both 70–90. Assign suitable target values for the chosen test room; these frame values are examples, not tuned production settings.
3. Scrub out of order. Verify segment rows and the camera schematic agree with authored timing. Neither MainCamera nor saved assets should be changed by preview sampling.
4. Set the event trigger within the action. Trigger 20 plus local segment 10 means that segment starts at action frame 30. Set a 1000-frame hold; the timeline must extend beyond the actor clip and natural action completion must not truncate it.
5. Verify peak lens and reached XY remain fixed while the actor/base camera moves. Return should approach current normal framing. Cuts must occur when Start equals End.
6. Verify a world-point move and a facing-relative offset; camera Z must remain unchanged. At room edges, final viewport remains inside supported bounds. Unsupported bounds retain normal framing.
7. Check parallel Zoom/Move, conflicts on the same channel, duplicate cuts, end before start, missing Return and a trigger beyond the actor step. Invalid sequences must show an error and not start.
8. Verify pause/hitstop pause the sequence clock and slow motion respects scaled time. Check accepted interruption, hitstun, death, swap, room change, focus/Ultimate, manager disable and replacement event cleanup.
9. Opening existing camera panels must not convert data or mark it dirty. Refresh the explicit conversion preview, convert one event and test Undo/Redo. Legacy motion must retain its keys; Screen Impulse must retain shake/slow motion and receive a separate camera event at the same trigger.
10. Check visual framing after conversion, particularly moving-camera offsets, which become anchored to captured start. Ambiguous overlapping camera events and duplicate legacy key frames must block conversion until resolved.

Human feel review: the action and nearby threats remain readable; player input remains responsive; camera movement and cuts match the intended action. Do not interpret passing sampler tests as a native playback or feel approval.
