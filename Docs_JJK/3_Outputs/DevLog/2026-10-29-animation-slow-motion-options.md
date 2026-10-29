---
slug: animation-slow-motion-options
status: draft
source: manual
gdd_tags: [architecture]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Animation slow-motion options

Implemented the approved Follow Slow Motion option on camera sequences and MotionData, default on, and Slow Motion Aware on audio entries, default off. The shared time policy selects active progression without changing action-frame triggers or combat timing. Camera clocks snapshot per run. Normal motion remains in the physics motor; captured-world motion has an independently gated fixed counter. Pause and full hitstop still stop opt-out camera/motion progression.

Timeline audio now retains authored pitch, optionally applies the live nonzero slow-motion factor, pauses opted-in sources on full freeze, and tracks actual playback completion. Paused/listener-paused clips cannot be returned prematurely. Pooled sources reset pitch, and voice replacement clears the prior awareness state. Existing music and controlled SFX paths are unchanged.

Validation: runtime/editor compilation passed and 27 engine-independent camera/clock/pitch tests passed. Three native motion/captured-world/audio lifecycle tests compile; native execution, collision testing and listening checks remain pending. No saved action assets, scenes, dependencies, global physics configuration or mixer assets were changed by this task.
