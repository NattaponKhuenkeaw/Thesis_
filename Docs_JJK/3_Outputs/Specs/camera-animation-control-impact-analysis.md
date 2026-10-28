---
slug: camera-animation-control
status: approved
source: manual
gdd_tags: [architecture]
owner: architect-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Camera Animation Control — proposed sequence and implementation scope

## Goal

Replace fragmented camera authoring with one event whose camera sequence is authored like MotionFrameData, using explicit frame ranges rather than hidden first/last-key and hold-tail rules.

## Authoring contract

- Event Trigger Frame starts the sequence at local frame 0. Segment Start Frame / End Frame are relative to that event, not the action or preceding segment.
- Each segment exposes a label, task, start/end frames, easing and only the fields relevant to its task. Equal start/end means an instant cut.
- Zoom targets a multiplier of the normal lens captured at event start. Below 1 zooms in; above 1 zooms out. The reached lens stays fixed through a hold, subject to room bounds.
- Move targets either a world XY point or an XY offset from the camera position captured at event start. Units are world units. Camera depth remains unchanged. Optional facing-relative X is explicit.
- Hold identifies Zoom, Position or Both. It freezes those camera values over its interval, not player input, gameplay or time.
- Return identifies Zoom, Position or Both and blends those channels to live normal camera framing over its interval. Normal means the camera system's current base composition, avoiding a stale restore after actor movement.
- Before a channel's first segment, it follows normal framing. After a Zoom/Move segment, its reached value persists through gaps until the next command on that channel.
- Zoom and position may animate simultaneously. Overlapping segments on the same channel are rejected with a visible validation message; no list-order-dependent results. Instant commands at a shared boundary have a defined stable order and cannot silently compete.
- Return is an explicit segment. At sequence completion, ownership releases. If the author omits Return, completion cuts to normal; the editor makes that visible instead of adding hidden return time.
- Segments appear as labeled ranges in the Animation Editor timeline, with an event row covering the complete sequence. The property editor follows MotionFrameData's segment cards with Add/Remove controls and a concise chronological summary.
- Scrubbing samples local sequence frames without starting gameplay, freezing world time or spawning another camera controller.

## Example sequence (illustrative, not tuned action data)

| Local frames | Task | Result |
| --- | --- | --- |
| 0–10 | Zoom | Transition to the authored zoom |
| 10–40 | Hold Zoom | Keep the peak zoom |
| 40–55 | Move | Pan to the authored XY target while retaining zoom |
| 55–70 | Hold Both | Hold lens and position |
| 70–90 | Return Both | Blend back to normal framing |

With event trigger 20, local frame 40 corresponds to action frame 60 while the action is still running. A local hold ending at 1000 remains authored as 1000 even if the attack ends first.

## Clock and cleanup

Retain the user-approved scaled gameplay timer at LOGIC_FPS. It starts when the event fires, pauses with hitstop/pause, respects slow motion and continues after natural action completion/auto-chain. An accepted interruption while owned by the action, death, hitstun, swap/field-session invalidation, room change, higher-priority focus/Ultimate, manager disable or replacement camera event clears the claim. Stale tokens cannot update or release newer sequences.

The sequence is sampled from its local frame rather than a chain of coroutine/tween completions. Capture per-run baseline data and resolve segment origins deterministically. Out-of-order preview sampling and skipped frames must produce the same values as continuous sampling. Read-only source data must not be mutated by runtime sampling or merely opening the editor.

## Responsibility and impact

| Area | Current | Proposed |
| --- | --- | --- |
| Event data | Legacy key list plus optional single-zoom settings; camera settings also on Screen Impulse | Dedicated CameraAnimationControl event and CameraAnimationFrameData segments |
| Sampling | Separate legacy-key and single-envelope paths | Pure camera-sequence evaluator with explicit per-channel results |
| CameraManager | Tokens, lens/lock sampling and gameplay clock | Existing token/clock owner delegates evaluation to the sequence sampler |
| Cinemachine extension | Final-viewport zoom/position correction | Apply evaluated lens and XY targets with the same final room-bound protection |
| CombatPlayerState | Dispatches camera variants and manages cancellation | Dispatch canonical sequence; compatibility adapters for legacy events |
| PropertyCardBuilder | Single zoom or older key fields | Motion-style segment cards and timing validation |
| AnimationEditorWindow / TimelineGridElement | Generic event ranges | Camera segment ranges, labels and read-only sequence preview |

Keep player MotionFrameData unchanged: its actor movement, target-dash and rotation logic must not be reused as a camera mover. Reuse its authoring pattern. No new package, vcam-priority scheme, MainCamera transform writes, build/render configuration or global director.

## Migration

- Append a new event enum value; retain existing numeric values and serialized fields so existing assets keep loading.
- New authoring uses Camera Animation Control. Legacy camera events remain readable through adapters until explicitly converted.
- Convert single zoom to Zoom, Hold and Return segments, with an equivalent position lock/offset representation where applicable.
- Convert a true legacy camera key list to its complete sequence; do not collapse it to only its strongest key. If single-zoom settings are already authoritative, convert those and retain the old keys as legacy data.
- For camera settings embedded in Screen Impulse, provide an explicit Undo-supported conversion into a separate camera event at the same trigger; preserve shake and slow motion on the original impulse event. Inspect any ambiguous overlap before changing assets.
- Opening a panel must not mark an asset dirty or migrate data automatically. Conversion is a named authoring action with Undo and a preview of affected events.
- No mass conversion or removal of saved assets in the initial implementation.

## Verification

- Pure sampler tests: event-relative timing, zero-length cuts, ordered and out-of-order samples, gaps, zoom-out, simultaneous lens/position, invalid overlap and return.
- Hold at peak for 1000 frames, then return for exactly the authored interval. Verify beyond natural action end using the runtime lifetime test, not just an envelope arithmetic test.
- Move to a point at the requested local frame; Z must remain unchanged. Hold must keep the reached position while the player moves.
- Verify camera claim replacement and every cleanup path, pause/hitstop and slow motion.
- Migration parity and Undo/Redo tests for key lists, single envelopes, combined Screen Impulse and disabled settings. Opening the editor must not alter serialized data.
- Final viewport remains inside supported room bounds; unsupported geometry retains base composition.
- Native Unity visual pass: sequence playback matches timeline preview, normal framing returns smoothly, and combat remains readable.

## Approval boundary

This changes the canonical camera-event schema, sequencing semantics, preview integration and migration surface beyond ADR-013's approved single-zoom envelope. User approved this packet with “lgtm” on 2026-10-29. Implementation follows the scope; action-specific feel and team publishing remain separate approvals.

## Implementation and validation

Implemented CameraAnimationControl as appended enum value 8, CameraAnimationFrameData/CameraAnimationSampler, explicit conversion and a partial editor card builder. The timeline extends through camera tails and shows segment rows; scrubbing displays a read-only schematic rather than moving MainCamera or writing assets. The schematic shows requested framing; runtime room confinement can limit it.

Runtime and editor assemblies compiled with Unity's bundled compiler against the current project response files, using temporary outputs. Executed 13 sequence/conversion tests plus 9 existing camera logic tests against those assemblies using actual NUnit assertions in a temporary .NET harness: all 22 passed. Native lifecycle tests (including sequence continuation and stale ownership) compile but were not executed. The desktop helper failed twice with `failed to write kernel assets: The system cannot find the path specified. (os error 3)`; Unity MCP is unavailable.

Legacy conversion preserves targets/keys and explicitly warns that offsets become anchored to captured camera start. This changes following-camera offset semantics; moving-camera visual parity is not claimed. No authored assets were mass-converted. Existing legacy playback remains available until explicit conversion.
