---
slug: camera-player-tracking
status: draft
source: manual
gdd_tags: [architecture]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Player-targeted camera animation

Added Player + Offset (Live) to camera movement and Zoom + Move as a combined task. The existing sampler reads the live on-field player's root position; zoom and position can be authored together or as independent parallel segments. Hold Zoom retains live tracking, whereas Hold Position captures the reached framing at its boundary. Return uses a captured origin and current normal framing. Existing bounds, depth, clocks and ownership remain unchanged.

The editor exposes world-unit player offset and captured-facing mirroring. The schematic uses a static actor reference and states that limitation. Existing serialized enum values and legacy playback are preserved; no saved action assets or scenes were modified by this task.

Validation: runtime/editor compilation and 31 engine-independent tests passed. A native player-tracking/boundary/replacement test compiles; Unity execution and visual feel/bounds checks remain pending. Temporary compiler lists were refreshed to include another task's new Character Inspector source without changing that work.
