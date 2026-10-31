---
slug: screen-impulse-time-options
status: draft
source: manual
gdd_tags: [visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Screen Impulse timing verification

Run `Tools > Project > Combat > Run Overlay and Camera Tests`. The ScreenImpulseCameraTests fixture includes native Cinemachine clock checks and an EnterPlayMode/ExitPlayMode regression; the runner restores the editor's scene afterwards. Results: `Temp/CombatPresentationTests.xml` and `Temp/CombatPresentationTests-summary.txt`.

Automated checks: all independent flag combinations; real-time/scaled delta and full freeze; explicit pause always holds; missing fields default off; existing factory defaults; concurrent native waveforms with different clocks; signal output at frozen engine time; preservation of the shared IgnoreTimeScale flag; restoration of Smart/Fixed camera and blend modes; Manual driver preservation; pooled-handle identity; actual camera output shake during HitData freeze alongside tier/event requests. Existing flash-envelope and bounded position-lock regressions remain in the focused run.

Manual checks:

1. Author Screen Impulse with both toggles off. Hit an enemy with nonzero HitData hitstop while the event is active; both tier and event shake continue at normal speed.
2. Turn on Follow Slow Motion only: shake slows during nonzero slow motion but continues normally in full hitstop. Turn on Follow Hitstop only: shake ignores nonzero slow motion but holds during hitstop. Turn on both to follow both effects.
3. Set HitData hitstop frames to zero; tier shake still triggers. Toggle Suppress Impact Feedback; neither automatic hitstop nor tier shake should trigger for that hit.
4. Hold camera position with Camera Animation Control and trigger tier/event impulses. Shake remains additive. Confirm room-edge/base composition follows existing bounds.
5. Open pause during active shake; pulse/waveform progress holds. Resume without catch-up bursts. Disable the impulse controller or change scenes; no owned shake or temporary camera mode remains.
6. Edit, undo, save and reopen the event toggles. Configure the same policies on GameImpulseController > Impact Profiles for the desired tier.

These tests establish behavior, not a subjective feel approval. No new strength/duration values are prescribed.
