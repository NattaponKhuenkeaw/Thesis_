---
slug: background-overlay-flash
status: approved
source: manual
gdd_tags: [visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Implementation within existing presentation ownership

- `ActionEventFrameData.cs`: extend BackgroundOverlayFlashSettings with phase fields, total duration, alpha sampling and rescaling; timeline uses total duration.
- `PropertyCardBuilder.cs`: expose all three phases with existing Undo/dirty handling, including the shared fullscreen-slash flash panel.
- `UltimateFlashOverlay.cs`: sample the envelope with unscaled elapsed logic frames; retain arbitration and cleanup.
- `ActionCameraMotionExtension.cs`: retain Finalize processing under ADR-016. Capture correction state at Body/Aim (including the deferred second Body pass), sample/bound composition without subsequent noise, and add its correction delta to the final noisy state. Preserve orientation noise. This avoids absorbing impulse displacement into position locks or allowing rotational noise to trip the unrotated-room guard.

Both `HitBoxDamage → CameraShakeController/CameraManager → GameImpulseController` and `CombatPlayerState.TriggerScreenImpulse → GameImpulseController` reach the same Cinemachine source/listener pipeline. The rig's listeners apply at Noise (`ApplyAfter: 2`). No changes to either payload path, suppression rules, camera priority, packages, scenes or saved content are needed.

No new architecture decision or gameplay values: this corrects composition of existing ADR-016 camera animation with existing impulse feedback. The room confines the base composition; impulse remains an additive rendered displacement, as with ordinary Cinemachine confinement.
