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

# ADR-016: Camera Animation Control Sequence

Accepted: user “lgtm”, 2026-10-29, approving `../Specs/camera-animation-control-impact-analysis.md`.

The canonical camera authoring event is a sequence of Zoom, Move, Hold and Return segments with event-relative start/end frames. Zoom and position are independent channels; same-channel overlap is invalid. Frame sampling is deterministic and allocation-free during playback; a sampler snapshots source data per run. CameraManager retains generation-scoped ownership and its scaled gameplay clock, while the existing Cinemachine extension applies final supported-room viewport bounds.

Natural action completion and auto-chain do not truncate a sequence. Accepted owning-action interruption, hitstun, death, field-session/swap invalidation, room change, higher-priority camera focus/Ultimate, disabling the manager and replacement camera events clear ownership. Camera presentation does not move the actor or freeze world time. No MainCamera transform writes, camera-priority changes, package additions or global director are introduced.

Zoom targets reference the captured initial normal lens. Completed Zoom/Move values persist through gaps and Hold intervals. Return blends to live normal framing. Missing Return means a visible cut to normal on release; there is no hidden return duration. Runtime bounds take precedence over requested composition. The schematic preview is read-only and does not claim to reproduce final scene pixels or confinement.

Append enum value 8 and retain previous values/fields for compatibility. Explicit Undo-supported conversion preserves legacy data for recovery; no migration occurs merely by opening the camera panel. Convert embedded Screen Impulse camera control into a separate camera event while preserving its shake/slow-motion payload. Original key lists retain every key in their converted sequence. The conversion preview calls out capture-relative position semantics and rejects ambiguous overlapping camera events or duplicate legacy key frames.

This extends ADR-013's ownership and bounds contract and supersedes its single-zoom-only authoring direction for new events. Legacy playback remains until explicitly converted. Validation and native Unity limitations are recorded in the impact analysis and test plan.
