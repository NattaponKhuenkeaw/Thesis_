---
slug: camera-player-tracking
status: approved
source: manual
gdd_tags: [architecture]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Player-targeted camera animation

Workflow: `/implement-feature`. User requests camera zoom/position tracking the player with an offset. Scope is an optional target inside the existing ADR-016 sequence owner/sampler; it adds no camera ownership, clock, dependency or physics authority. The direct request authorizes the optional target; action-specific camera feel remains a human checkpoint.

## Implemented

- Appended Player + Offset (Live) position space for Move segments. XY offsets use world units; optional X mirroring retains the event's captured facing.
- Appended Zoom + Move task, controlling both existing channels over the same frame range. Separate Zoom and Move segments remain available for independent timing.
- Runtime supplies the owning on-field player's current root position to the existing sampler. Completed tracking moves keep following that root while the sequence remains active, including during Hold Zoom.
- Hold Position/Both captures the tracking result at its start. Return freezes the tracking origin at its start, then blends to current normal framing rather than chasing the moving player throughout the return.
- The existing manager captures player positions at position-command boundaries, interpolating between updates when a boundary is crossed. Storage is allocated at sequence start. Pure out-of-order evaluation accepts that boundary history explicitly, without changing source data.
- Room bounds, depth preservation, slow-motion selection, pause/freeze, cancellation, replacement and field-session checks remain authoritative.
- The schematic preview uses a static preview-actor/scene-player reference and labels this limitation; it does not simulate historical actor motion or final confinement.
- Existing serialized task/position enum values and default behavior remain unchanged. No action assets were automatically converted or tuned.

## Validation

Runtime and editor compilation passed using current project response files plus current source inventory, with temporary outputs. The initial temporary source list lacked another task's newly added CharacterInspectorFolder; refreshing the temporary list resolved that error without editing those sources.

All 31 engine-independent camera/clock/pitch tests passed against the compiled assemblies with actual NUnit assertions in the temporary .NET harness. Four new tests cover combined zoom/player tracking, captured Hold Position, stable return/out-of-order samples, facing offset and enum compatibility. The native lifecycle test `MainDimensionSlashTests.LivePlayerTrackingFreezesAtHoldBoundaryAndReplacementClearsCapturedPoints` compiles; execution and live visual/bounds verification remain pending while Unity automation is unavailable.
