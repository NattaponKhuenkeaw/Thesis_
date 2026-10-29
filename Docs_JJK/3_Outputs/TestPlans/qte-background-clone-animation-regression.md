---
slug: qte-background-clone-animation
status: draft
source: manual
gdd_tags: [combat, combat-scope]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# QTE Outgoing Clone Animation Regression

Manual gameplay checks below have not been run. Use a valid ordinary character switch during a permitted recovery/cancel window; the fix does not permit midair or otherwise illegal swaps.

- [ ] Switch away during Lightning Plunge recovery. The outgoing clone starts at the current landing/standing pose and advances through the remaining authored action frames, then disappears.
- [ ] Continue to Execution Breakdown, then switch away during its legal window. The outgoing clone resumes at the same animation progress, completes the current step and disappears without staying frozen until the lifetime safety timeout.
- [ ] Swap at early and late legal points, repeat swaps and observe slow motion/pause. Neither animation restarts from zero nor continues advancing while scaled gameplay time is paused.
- [ ] CameraGirl and ordinary non-sampled actions still use their existing Animator playback and completion behavior.
- [ ] Outgoing attack hits retain original character attribution and already-hit history; no extra hits occur after the finite action timeline ends.
- [ ] Merely chaining Plunge into Execution Breakdown does not create a second actor.

Code inspection confirms both affected authored steps use syncAnimationToActionFrames and Main's corresponding Animator states have speed zero. The shared executor previously never called Play/Update again after copying its initial pose. The fix samples with the same frame/TotalFrames mapping as the live actor.
