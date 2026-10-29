---
slug: animation-slow-motion-options
status: approved
source: manual
gdd_tags: [architecture]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Camera, Motion and Audio slow-motion options

Workflow: `/implement-feature`.

User request: optional camera/Motion independence from slow motion and slow-motion-aware sound. GDD basis: `@tag:architecture`, data-authored behavior and gameplay-driven presentation. This packet proposes clock semantics; they are not existing GDD requirements.

## Inspected behavior

- CameraAnimationControl advances its local sequence using scaled delta time at LOGIC_FPS.
- MotionFrameData dispatches from PlayerCombat.ProcessMotion at action logic frames. PlayerMovement's normal motion coroutine uses scaled fixed time, with a special unscaled mode controlled by Ultimate sequences. CapturedWorldPoint has a separate action-owned physical path; it must be included in any clock change.
- AudioEntry triggers from PlayerCombat's action logic frames. GameAudioManager sets AudioSource.pitch to entry.pitchVariation once. It does not continuously track slow motion. Timeline audio.duration is visualization metadata, not a playback stop deadline.

## Proposed authoring behavior

| Data | Control | Existing-data default | Behavior |
| --- | --- | --- | --- |
| CameraAnimationFrameData | Follow Slow Motion | On | Off uses normal-speed local sequence progress after the event fires. |
| MotionFrameData | Follow Slow Motion | On | Off uses normal-speed movement progress after each motion key triggers, including captured-world motion. |
| AudioEntry | Slow Motion Aware | Off | On continuously multiplies authored pitch by the gameplay slow-motion factor, slowing playback and lowering pitch. Off retains authored pitch. |

These settings apply per camera sequence, per MotionData track, and per audio entry respectively. Do not change the whole combat state's clock, animation speed, hit timing, damage, input windows or cooldowns.

## Timing boundaries

- Trigger frames remain action-frame synchronized. Ignoring slow motion changes an active camera/movement segment's progression, not when its action-frame trigger fires. The UI help must say this explicitly.
- Hard pause and full hitstop/freeze still stop camera and actor motion even when Follow Slow Motion is off. Existing explicit Ultimate cinematic freeze/unscaled overrides retain their current meaning; a new generic toggle must not accidentally grant movement during a full freeze.
- Ordinary nonzero slow motion slows tracks with Follow Slow Motion on; off tracks use unscaled elapsed time while honoring the pause/freeze gate.
- Camera sequence lifetime, replacement and cleanup follow ADR-016. Movement retains the combat step's existing cancellation and authority rules under ADR-003; it does not become a surviving camera-like sequence.
- Actor movement that ignores slow motion may reach its destination before the action's damage/animation frames. This is an intentional authoring choice, requiring visual testing on any action that enables it.
- Preserve collision/motor authority. Do not simulate Rigidbody motion directly from render Update or change global physics timestep/settings. Compensate requests and sample the active path through the existing physics motor; test low time scales and acknowledge that physics cadence remains Unity's fixed-step cadence.

## Audio contract

- For opted-in timeline audio, retain a base pitch and compute effective pitch from it each update; never compound the previously adjusted pitch.
- Starting or changing slow motion while a clip is already playing must update that clip. Returning to normal restores its authored pitch.
- This is coupled playback-speed/pitch change. Pitch-preserving time stretch is outside scope and would require a separately approved DSP/dependency approach.
- Pause/full-freeze behavior remains distinct from nonzero slow-motion scaling. Reuse the project's listener/freeze contract; do not set pitch to zero and treat that as pause.
- Pool cleanup must follow actual playback completion rather than a startup-only clip-length/pitch delay. Return/reuse must clear the opt-in state and reset pitch so another SFX cannot inherit it. Respect the existing shared voice-source replacement behavior.
- Music and unrelated controlled SFX remain unchanged. The opt-in here belongs to AudioFrameData entries, including a Voice entry if the author explicitly enables it.

## Implementation surface

- Add backward-compatible serialized controls and editor fields/help to camera sequence, MotionData and AudioEntry.
- Centralize the scaled/unscaled delta selection plus pause/freeze gating in a small shared helper, with independent per-track counters.
- CameraManager selects its sequence delta from the new camera option, preserving the immutable playback snapshot.
- PlayerCombat/PlayerMovement integrate the selected motion clock with normal and captured-world movement without advancing the whole action clock.
- GameAudioManager tracks active opted-in sources, updates effective pitch, and returns pooled timeline sources when actual playback finishes.
- Update ADR-016 for selectable camera time and ADR-003 for selectable movement progression. No build/dependency/scene/mixer asset changes are needed for the proposed implementation.

## Verification plan

- At illustrative slow-motion factor 0.25, a 60-frame active segment takes four gameplay-scaled seconds when following slow motion and one real second when ignoring it. Both must stop during full pause/hitstop. These are clock checks, not tuned gameplay values.
- Camera: test a mid-sequence time-scale change, parallel Zoom/Move, long hold, return, cancellation and replacement.
- Motion: test Relative, Absolute, target dash and CapturedWorldPoint; verify total displacement, collision behavior, early completion, cancellation, zero-duration cuts and low-scale fixed-step cadence. Hit/animation frames must retain their original action clock.
- Audio: test opted-in and opted-out clips concurrently, a mid-clip slow-motion change, restore, voice replacement, pause/freeze and pool reuse. Base pitch 1.2 with scale 0.25 produces effective pitch 0.3 while opted in, then returns to 1.2.
- Existing assets with untouched defaults must retain behavior. Validate pure clock/pitch logic plus native Unity motion/audio playback; compilation alone is insufficient.

User approved this scope with “lgtm” on 2026-10-29. Implemented selectable camera and MotionData progression, captured-world fixed-motor sampling, slow-motion-aware timeline audio, pause-safe source tracking and editor controls. Runtime/editor compilation passed. All 27 engine-independent camera/time/pitch tests passed against compiled assemblies via the temporary .NET harness. Native motion, captured-world and audio-source lifecycle tests compile; execution and playback/collision/listening checks remain pending because Unity MCP and the desktop helper are unavailable. See `../TestPlans/animation-slow-motion-options-test-plan.md`.
