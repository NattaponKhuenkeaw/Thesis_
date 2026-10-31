# 2026-10-31 — Screen Impulse time options

Source: user's follow-up requiring optional slow-motion/hitstop awareness and uninterrupted damage-tier/event shake through HitData hitstop. GDD `@tag:visual-audio`, ADR-013/016 presentation ownership. Scope and semantics are recorded in `../Specs/screen-impulse-time-options-task-card.md`.

Added independent Follow Slow Motion and Follow Hitstop fields to GameImpulseRequest and the action editor. Both default off, including old serialized data and damage-tier profile factories. Nonzero slow motion and full freeze are independent choices; explicit GameFreezeManager pause always holds playback. The existing Enable Slow Motion payload remains a separate time-effect request after pulse emission. Action-frame triggers retain their original clock.

Pulse timing now selects its delta per request instead of waiting on a hardcoded realtime interval. The controller retains native Cinemachine event handles and retimes each waveform with its own selected delta, preserving source shape/direction/channel/distance behavior. Concurrent policies coexist without toggling ImpulseManager.IgnoreTimeScale. Signal identity protects pooled/recycled handles. Disable stops owned coroutines, silences owned signals and restores temporary camera modes.

During zero-scale hitstop with an independent signal, Smart/Fixed camera and blend sampling temporarily run in LateUpdate so the waveform can render even when physics stops. The base camera delta remains zero on the project's normal brain configuration. Modes restore on resume, pause, completion or disable; ManualUpdate drivers retain ownership.

Separated accepted-hit damage-tier shake from the world-hitstop trigger in HitBoxDamage. Zero hitstop frames now retain tier shake; Suppress Impact Feedback still suppresses both. Once-per-activation and projectile/world-freeze behavior remain in place.

Validation: Unity compilation succeeded; **47 focused checks passed, zero failed/skipped**. This includes 13 new clock/default/native cases, the previous flash/camera tests, and an EnterPlayMode/ExitPlayMode regression that verifies actual rendered shake during HitData freeze with concurrent tier/event requests, zero-frame hitstop, suppression and camera mode restoration. Shared-clock preservation, concurrent policies, explicit pause, pooled handles and disable cleanup are covered. Source whitespace checks passed.

The Play Mode fixture explicitly configures dynamically created source/listener fields because runtime AddComponent does not apply their editor Reset defaults. Its resume check waits for the player loop to process the restored scale. The original scene was restored by the test runner. A concurrently introduced CS0052 cache-type accessibility error in CharacterProgressionState was corrected by matching the nested type to its protected field; final compilation has no script errors.

Manual subjective game-feel review remains pending using `../TestPlans/screen-impulse-time-options-test-plan.md`. No new strength/duration tuning, build/dependency configuration, merge or team publishing was performed.
