---
slug: animation-timeline-scroll
status: draft
gdd_tags: []
owner: implementation-agent
human_checkpoint: not-required
next_agent: human
blocked_by: []
---

# Manual regression checks

Pending in Unity:

- Open Window > Animation Editor and select an action with enough tracks to exceed the timeline height.
- Use the wheel and vertical scrollbar to reach the last track; verify labels align with phase and track rows throughout.
- Scroll horizontally on a long action; labels must stay fixed horizontally.
- At nonzero scroll offsets, click and drag to seek; confirm the expected frame is selected.
- Resize the window and verify playback controls stay visible and all tracks remain reachable.
- Switch to an action with few tracks and back; verify scroll bounds update without blank or inaccessible content.
- Confirm compilation and the Console have no new errors.
