---
slug: main-dimension-slash
status: draft
source: manual
gdd_tags: [visual-audio, combat, inspirations]
owner: game-design-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Dimension Slash: Reference Clip Observations

Inspected 2026-10-27. Reference: `E:/Drive_E_Download/dimension-slash.mp4`. This is visual analysis of a recording, not knowledge of BBEEx's engine implementation.

## Recording and Inspection

- Video stream: 1440 x 2560, 60 fps, 126 decoded frames; decoded frame span 0.000-2.083 seconds, approximately 2.1 seconds total. Container duration is approximately 2.11 seconds.
- Gameplay occupies the central part of a portrait recording. Blurred material above and below it is presentation padding, not an additional game-camera effect.
- Frame numbers here are zero-based recording frames. They do not establish the game's simulation rate, sprite exposure rate, or input latency.
- Reviewed an overview at six-frame intervals, burst detail at two-frame intervals, and adjacent frames around the onset (64-81) and late burst (100-105).
- The file contains stereo AAC audio. It was not auditioned in this pass; the design's sound layers are proposals, not a transcription of the recording.
- The clip starts during an ongoing combat demonstration. The first second cannot safely be interpreted as the complete wind-up of this specific attack.

## Visible Timeline

| Recording time / frames | Direct observation | Design interpretation to test |
| --- | --- | --- |
| 0.000-0.900 / 0-54 | Airborne target descends; a low cyan/blue effect repeatedly expands and contracts around the actor region. A larger blue eruption appears near 0.7 seconds. | Preceding action/setup establishes spatial focus. Do not copy this entire span as a mandatory charge. |
| 1.000-1.133 / 60-68 | Actor-like outline/echo becomes a more solid figure; purple curved accents sweep around it. Bright activity is still localized. | Readable actor pose and restrained value prepare the larger release. The exact body/echo implementation is unknown. |
| 1.150-1.200 / 69-72 | A thin near-vertical cyan cut appears, then an intersecting diagonal cut. Their edges are sharply introduced. | Fine seams provide a directional cue before the broad blades. Not every VFX line needs its own damage event. |
| 1.217-1.350 / 73-81 | Purple-blue illumination expands; bright ground stars and broad diagonal slash wedges build over earlier thin lines. Multiple actor-like silhouettes/arcs occupy different locations. | Layered attack shapes and echoes give density without requiring one continuous actor swing. These may be copies, afterimages, or independent animation layers; the recording cannot decide. |
| 1.367-1.733 / 82-104 | Broad slashes change angle across the target region, including diagonal, near-vertical, and near-horizontal shapes. Some thin lines remain while the next broad blade arrives. | Fast leading-edge changes plus overlapping low-value tails can feel fluid even when individual poses are held. Alternating direction makes the barrage feel spatially composed. |
| 1.750-2.083 / 105-125 | Broad bands collapse to a thin crossed-line lattice and ground spark; the purple tint recedes and the room becomes readable again. | Deliberate decay gives closure. In this excerpt there is no clearly separate, later giant finishing cut after the barrage. The requested Dimension Cut is a new concluding beat for Neo-JJK. |

## Why the Visual Impact Reads Strongly

These are inferences from the visible composition, not verified BBEEx implementation details:

1. **Value and scale contrast:** thin cyan incisions precede broad high-value wedges, against a comparatively dark room.
2. **Snap plus overlap:** new leading edges appear quickly; earlier lines and arcs decay underneath them. Smoothness comes partly from continuity across layers, not uniformly slow motion.
3. **Changing directions around a stable region:** cut angles vary while the target region remains legible. Purely random full-screen lines would lose that spatial story.
4. **Actor and VFX have different jobs:** the body/echo silhouettes convey authority and orientation, while enormous cut shapes convey reach and speed.
5. **An ending that removes energy:** broad shapes narrow, tint recedes, and the room returns. Endless equally bright residuals would flatten the climax.

## Camera Evidence and Limits

The portrait padding and VFX motion can exaggerate perceived camera movement. To check background motion, an ORB feature match plus RANSAC partial affine fit tracked the upper gameplay background relative to recording frame60, at half resolution. Raw estimates, acceptance flags, and limitations are in `background-motion-estimates.json`.

- Accepted samples at frames66,68,70,72 show relative background scale of approximately 1.004, 1.016, 1.022, 1.032. This is consistent with a modest progressive push during the burst onset, around a three-percent screen-space enlargement by frame72.
- This is **not** a calibrated camera lens measurement. It may include capture/editing effects, background motion, and fit error. Do not convert it directly into Unity orthographic size or copy it as an exact BBEEx camera curve.
- Most later estimates fail confidence checks because bright effects cover the repeated tile background. Those rows are explicitly rejected. No reliable full-burst zoom, shake amplitude, or rotation curve is claimed.
- Repeated poses and similar adjacent recording frames do not establish exact hitstop durations. Source animation exposures, encoding, and separate VFX motion confound that measurement.

The practical design conclusion is a restrained framing push, brief impact punctuation, and a clear return. The evidence does not justify large constant screen shake or repeated camera cuts on every slash.

## Unity MCP Inspection Snapshot

Read-only checks found `Test_Playground` active and the expected Player, Enemy_Dummy, and CameraSetUp. CameraSetUp has explore, combat, static, and ultimate Cinemachine cameras, confiners, impulse listeners, and a target group.

The live MainCamera component list contained Camera, CinemachineBrain, UniversalAdditionalCameraData, AudioListener, and Transform. A scene-wide component search for PixelPerfectCamera, including inactive objects, returned zero results. Therefore pixel-perfect camera setup is **not verified as installed on this scene**, despite CameraManager's setup comment mentioning it. Changing this is outside the research phase and could affect the whole presentation.

The `mcpforunity://scene/cameras` resource returned an unsupported-command error, so camera evidence here comes from the supported hierarchy/component search and inspected source, not that resource.

## Review Images

- `reference-overview.jpg`: entire recording at 0.1-second intervals.
- `reference-burst-detail.jpg`: cropped gameplay at two-frame intervals over the burst and decay.
- `reference-transition-frames.jpg`: adjacent onset and late-burst samples.
- `new-sprite-evidence.png`: separate evidence board for Main's New_sprite actor direction.

Derived images are review aids only. Original video and source artwork were not modified or imported into gameplay assets.
