---
slug: action-camera-motion-authoring-fix
status: approved
source: manual
gdd_tags: [architecture]
owner: implementation-agent
human_checkpoint: not-required
next_agent: human
blocked_by: []
---

# Action Camera Motion authoring fixes

Workflow: `/fix-bug`. User reports offset has no effect, first key applies before its relative frame, and hold/return fields are missing from Action Camera Motion. Follow-up to the approved Screen Impulse camera controls; reuse ADR-013 ownership and final-viewport bounds handling.

Root causes: offset was clamped to ±0.03 in authoring, sampling and rendering, then suppressed completely on static cameras. Sampling substituted the first future key when no previous key existed. Hold/return controls existed only on Screen Impulse.

Implemented: neutral output before first relative key; offsets up to ±1 screen width, relative to captured facing, on both static and following cameras; room confinement still wins. Action Camera Motion now exposes hold/return frames after its final key, releases ownership at envelope completion, and rescales those durations. Keys are grouped into labeled foldouts, with timing/unit help. Zero hold/return restores normal framing on the frame after the final key. Existing zoom limits remain unchanged.

Acceptance: event trigger 20 plus first key relative 10 yields first camera change at action frame 30. Positive/negative offsets move inside a sufficiently large room; offsets at room edges remain bounded. Final key holds and returns over authored frames. Timeline duration includes hold/return. No changes to action assets.

Validation: runtime/editor compilation passed with the bundled Unity compiler and isolated outputs. Added two NUnit tests for delayed first-key sampling, hold/return/rescale, screen-width offset and edge clamping. Tests compile; NUnit execution and in-editor visual checks are pending.

## Follow-up: long hold still appears short

User reports that setting 1000 frames still appears to zoom for only 5–10 frames. Inspection confirms two limits omitted from the previous fix: hold/return apply after the final key (the saved Main action's final key is normal zoom, multiplier 1), and `CombatPlayerState` releases the claim at both step replacement and state exit. `HasActionCamera` also requires the original execution to remain current and attacking. Increasing the hold cannot bypass those lifecycle conditions.

User selected a single zoom per event, holding at peak zoom and returning to original scale. Implemented a single-target envelope in the camera-motion panel, shared with Screen Impulse. Opening a legacy event converts its strongest zoom-in key to the target and its relative frame to transition-in length, preserving hold/return. The timed envelope uses scaled gameplay frames and survives normal action completion/auto-chain; interruption and camera/field ownership changes still cancel it. ADR-013 records this user-directed lifecycle extension.

Runtime/editor compilation passed. Added tests for a 1000-frame peak hold, legacy conversion, and timed camera ownership surviving ResetCombo. Unity test-runner execution and visual verification remain pending.

Executed all nine engine-independent `ScreenImpulseCameraTests` methods against the compiled project assemblies using a temporary .NET reflection harness and actual NUnit assertions: nine passed, including the 1000-frame envelope, legacy conversion, return, bounds and offset cases. The native Unity lifecycle test remains compile-verified only; run it in EditMode before claiming interactive validation.
