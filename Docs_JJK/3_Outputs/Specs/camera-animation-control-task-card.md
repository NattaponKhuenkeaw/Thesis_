---
slug: camera-animation-control
status: approved
source: manual
gdd_tags: [architecture]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Camera Animation Control EventData

User requests replacing the current camera controls with a MotionData-like camera animation sequence: trigger on frames, zoom, hold, and move to another position at an authored frame.

Workflow: `/implement-feature`. Although requested as a refactor, this introduces new sequence behavior; the no-behavior-change `/refactor` workflow does not cover it.

GDD basis: `@tag:architecture`, authored data where practical and presentation reacting to gameplay. The sequence below is a proposal from the user request and inspected MotionFrameData, not an existing GDD requirement.

## Proposed result

One canonical Camera Animation Control event with explicit frame-range segments: Zoom, Move, Hold and Return. Follow MotionFrameData's authoring pattern, with a dedicated camera sampler and the existing bounded Cinemachine owner. Screen Impulse keeps shake/slow motion responsibility. Existing serialized controls remain readable during migration.

Review packet: `camera-animation-control-impact-analysis.md`.

User approved the review packet with “lgtm” on 2026-10-29. Implemented the event, deterministic sampler, runtime ownership, segment cards, timeline ranges, schematic scrub preview and explicit conversion tools. Runtime/editor compilation passed and 22 engine-independent tests passed against compiled project assemblies. Native Unity lifecycle tests compile; their execution and visual/game-feel review remain pending because the desktop verification helper failed to initialize.
