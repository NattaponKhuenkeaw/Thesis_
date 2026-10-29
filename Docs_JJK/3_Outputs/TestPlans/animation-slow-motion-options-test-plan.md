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

# Animation slow-motion options verification

## Completed

- Runtime and editor assemblies compile with Unity's bundled compiler and current project response files, using temporary outputs. Existing unrelated warnings remain.
- Executed all 27 engine-independent tests in CameraAnimationSequenceTests, ScreenImpulseCameraTests and AnimationSlowMotionTests against the compiled assemblies with actual NUnit assertions in a temporary .NET harness: passed.
- New checks cover following versus independent progression, full freeze gating, mid-sequence time changes, per-run camera snapshot, non-compounding pitch/restore and backward-compatible defaults.

## Native tests pending

Run EditMode `AnimationSlowMotionNativeTests`:

- IndependentMotionRequestsFullDisplacementOverNormalSpeedTime: exercises the actual movement coroutine's velocity requests and integrates them over scaled physics time.
- CapturedWorldMotionIgnoresSlowMotionButStillFreezesAndCancels: exercises the captured path's independent fixed clock and prevents rewind on repeated action-frame samples. Its fixture omits body colliders, so it does not prove collision behavior.
- TimelineAudioUpdatesPitchAndDoesNotReturnPausedSourcesToPool: exercises actual AudioSource properties, pause protection, completion tracking, pitch reset and reuse. It does not replace listening tests.

These tests compile but have not run in Unity. Unity MCP is unavailable and the desktop helper previously failed to initialize; no native playback success is claimed.

## Camera manual checks

1. In Camera Animation Control, compare Follow Slow Motion on/off with a 60-frame transition and an illustrative nonzero time scale such as 0.25. On takes four real seconds; off takes one, excluding pause/hitstop. Trigger timing must still follow the action.
2. Change slow motion during zoom, movement, hold and return. Verify parallel lens/position remain on the same selected clock, 1000-frame holds complete, and normal cleanup/replacement still works.
3. Pause and trigger full hitstop. Neither mode may advance during those gates. Resume without jumps or double advancement. Verify saved option, Undo/Redo and unchanged existing defaults.

## Actor motion manual checks

1. Toggle Follow Slow Motion on MotionData. Test Relative, Absolute, SnapToEnemy, DashThroughTarget and CapturedWorldPoint, including a mid-segment slow-motion change.
2. At illustrative scales 1, 0.25 and 0.01 verify intended total displacement and approximate real-time duration. Physics cadence remains Unity's fixed-step cadence; do not claim render-frame path resolution.
3. Check walls, floor, ceiling and moving supports. The opt-out must not bypass existing collision/path clearance or ground-context ownership.
4. Confirm key starts, damage, animation phases, cooldowns and input windows remain on the action clock. Faster motion may arrive before a damage frame by design; review that action's readability.
5. Pause/hitstop, cancel, swap and die during motion. Check zero-duration cuts and repeated/looped keys. Existing cinematic explicit unscaled overrides must retain their behavior.
6. For captured-world motion, check normal-speed progression between slowed logic frames, stable landing position, no rewind at the next logic frame, clearance failure cleanup and correct contact timing.

## Audio manual checks

1. In AudioFrameData, play two entries concurrently with Slow Motion Aware on/off. Change time scale while they play. Only the opted-in clip should slow and lower pitch; returning to normal restores authored pitch.
2. Use authored pitch 1.2 and illustrative scale 0.25: opted-in effective pitch is 0.3, then restores to 1.2. This is coupled pitch/rate change, not pitch-preserving time stretch.
3. Full hitstop pauses opted-in clips; menu pause respects the existing listener pause for all ordinary timeline clips. Opted-out clips retain their established nonzero-scale behavior and may play through hitstop.
4. Resume before completion and confirm the source was not returned/reused while paused. Stop or finish a clip, then reuse the source for a non-aware clip: pitch must reset and awareness must not leak.
5. Replace a tracked Voice entry with another voice or an unscaled Ultimate voice. The old awareness state must not modify the replacement. Music, ambience and controlled SFX should retain existing behavior.
6. Verify completion follows actual playback, including a slow-motion change near the end. AudioEntry duration remains timeline visualization metadata and must not cut the clip.
7. Disable/destroy the audio manager with active timeline clips; no tracked playback or paused pool lease may survive.
