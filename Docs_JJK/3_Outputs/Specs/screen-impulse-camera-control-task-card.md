---
slug: screen-impulse-camera-control
status: approved
source: manual
gdd_tags: [architecture]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Screen Impulse Camera Control

Workflow: `/implement-feature`.

User request: add zoom in/out and Jump Cut (Lock) to Screen Impulse ActionEventData in the Animation Editor.

GDD basis: `@tag:architecture`, presentation reacts to gameplay. Specific zoom/lock behavior is proposed here, not existing GDD truth.

## Proposed behavior for approval

- Add an optional Camera Control section to Screen Impulse events. Disabled by default so existing assets retain their behavior.
- Expose target zoom as a multiplier of normal framing: below 1 zooms in, above 1 zooms out.
- Expose transition-in, hold, and return lengths in action frames. Zero transition frames means an instant cut; zero return frames means an instant restoration.
- Jump Cut (Lock) captures the currently rendered camera position at the event trigger and holds that world position while the actor can continue moving. It does not lock the actor or player input. It may combine with zoom.
- Return to current normal framing after the hold, rather than restoring a stale camera transform.
- Cancel/step end, death, swap, room change, and higher-priority camera sequences release this event's camera claim. Existing shake and slow motion remain independent.
- Room confinement takes precedence over requested zoom/position. Unsupported bounds must degrade safely rather than expose outside-room framing.
- Starting defaults are neutral zoom, no lock, and zero transition/hold/return frames; authoring the new feature is opt-in. No existing action assets are tuned automatically.

## Proposed integration for approval

Reuse the generation-scoped action-camera ownership in `CameraManager.ActionMotion.cs` and `CombatPlayerState`; do not use Ultimate camera priority switching or write MainCamera transforms. Extend the action-camera presentation layer to support a captured position and zoom-out while preserving the existing ActionCameraMotion event's limits and behavior. Settings live under ScreenImpulseEventSettings; the Animation Editor provides undo-aware fields. Timeline duration includes the camera envelope.

This extends ADR-013: its current lens contract only allows a restrained zoom-in and additive positional correction, so zoom-out and a held world position need explicit architecture approval before implementation.

## Acceptance / verification plan

- Existing Screen Impulse assets behave identically with Camera Control disabled.
- Authored zoom-in and zoom-out reach their targets and return to current base framing.
- Zero transition cuts immediately; lock holds framing while the actor moves.
- Action hitstop holds camera-envelope progress; cancellation, replacement, swap, and scene changes cannot leave a stale lock or lens effect.
- Test room edges and small rooms at zoom-out: final framing remains confined.
- Stale claims cannot release newer camera events. Verify overlap with ActionCameraMotion, Ultimate, and temporary focus.
- Fields persist and support Undo/Redo; timeline covers the envelope duration.
- Human feel pass: combat and threats remain readable during zoom/lock. If the view hides needed threats, reduce zoom-in/hold or disable lock on that action.

User approved behavior and integration with “lgtm” on 2026-10-29. Implementation completed; runtime and editor assemblies compiled with Unity's bundled C# compiler using current project response files and isolated temporary outputs. Existing unrelated warnings remain. Added five focused NUnit regression tests; test execution and in-editor feel review remain pending because the Unity MCP connection is unavailable.
