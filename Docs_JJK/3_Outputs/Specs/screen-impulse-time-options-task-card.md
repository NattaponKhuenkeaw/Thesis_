---
slug: screen-impulse-time-options
status: approved
source: manual
gdd_tags: [visual-audio, architecture]
owner: implementation-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Screen Impulse slow-motion and hitstop options

Workflow: `/implement-feature`. Source: user's 2026-10-31 follow-up requesting configurable slow-motion/hitstop awareness and requiring damage-tier/event shake to continue through HitData hitstop by default. This request authorizes the bounded extension to existing impulse playback. Human presentation-feel review remains pending.

GDD `@tag:visual-audio`: “Use large slash arcs, burst circles, impact flashes, hit-stop, and camera shake to sell attacks.” ADR-013 explicitly permits confirmed-impact vibration while ordinary hitstop holds action-frame camera presentation. Retain ADR-016 camera composition ownership and the preceding additive-shake fix.

## Authorized design and acceptance

| Control | Default | Enabled behavior |
|---|---|---|
| Follow Slow Motion | Off | Nonzero gameplay slow motion slows pulse scheduling and the native waveform. |
| Follow Hitstop | Off | Full freeze holds pulse scheduling and the native waveform. |

The controls are independent and apply per GameImpulseRequest, including each existing damage-tier profile and each Screen Impulse event. Old serialized data defaults to both off. Explicit GameFreezeManager pause holds all playback. Follow Slow Motion on / Follow Hitstop off uses scaled time during nonzero slow motion and normal real time during zero-scale hitstop. Trigger frames remain synchronized to the action timeline; the options govern playback after an event fires. The pre-existing Enable Slow Motion payload still requests a time effect after the pulse sequence and is separate from following time effects.

Clarity: two distinct toggles with help text; no hidden coupling to the slow-motion payload. Response: no input or combat-clock changes. Satisfaction/Fit: shake survives hitstop and position control. Motivation: combat rewards unchanged. No new combat tuning values or balance changes.

## Implementation contract

- Keep GameImpulseController's source, strength profiles and pulse coroutine. Select pulse delta through a small pure policy and snapshot the flags for active playback.
- Retain native Cinemachine event handles and their original SignalSource identity. Advance each waveform with its own chosen delta by retiming that event's StartTime before camera sampling. Never toggle the shared ImpulseManager.IgnoreTimeScale; concurrent policies must coexist.
- Use the original CinemachineImpulseDefinition and DefaultVelocity through CameraManager.CreateImpulse or the existing fallback source. Preserve signal shape, channel, distance and direction.
- Smart/Fixed cameras may skip sampling when physics stops. Only while a hitstop-independent signal is active in zero-scale hitstop, temporarily use LateUpdate camera/blend sampling with zero base delta. Restore prior modes on resume, explicit pause, completion or disable. Manual camera drivers retain their update ownership. No camera-transform writes or global clock changes.
- Guard pooled handles by signal identity; stop owned coroutines/signals and restore camera modes on disable. Expired native events retain native pooling/cleanup.
- Separate HitBoxDamage's accepted-hit damage-tier shake from its hitstop trigger. Zero hitstop frames must not suppress tier shake; Suppress Impact Feedback still suppresses both. Existing once-per-activation and projectile freeze rules remain.

## Verification

Test every slow-motion/hitstop flag combination, explicit pause, old-data defaults, concurrent native signals, waveform sampling at zero scaled time, pooled-handle safety, Smart/Fixed camera restoration and Manual driver preservation. Run a native enter/exit-Play-Mode regression with HitData hitstop, damage-tier and event requests, a physics-follow camera, and visible output motion while timeScale is zero. Repeat existing flash and camera-position composition regressions.

Manual feel review: compare each toggle combination during actual attacks; verify the same event and tier impulses at room edges, during a position hold, with zero HitData hitstop, during pause/resume and on scene/controller disable. If shake obscures threats, tune existing strength/duration values rather than combat timing. No external publishing or merge is authorized.
