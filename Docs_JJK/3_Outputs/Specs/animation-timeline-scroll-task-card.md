---
slug: animation-timeline-scroll
status: approved
source: manual
gdd_tags: []
owner: implementation-agent
human_checkpoint: not-required
next_agent: human
blocked_by: []
---

# Animation timeline vertical scrolling

- Workflow: `/fix-bug`.
- Request: timeline tracks extending past the Animation Editor viewport must remain reachable by scrolling.
- Scope: editor timeline layout only. No gameplay design or architecture change; no applicable GDD tag documented.
- Root cause: `TimelineGridElement` used a horizontal-only ScrollView with the vertical scrollbar hidden, while its parent clipped overflow.
- Fix: enable both scroll axes, constrain the timeline to available height, synchronize the clipped label column with vertical scrolling, and prevent playback controls from shrinking.
- Acceptance: bottom tracks are reachable; labels remain aligned; horizontal scrolling, seeking, and playback controls continue working.
- Verification: scoped `git diff --check` passed. Unity compilation and interactive checks remain pending.
