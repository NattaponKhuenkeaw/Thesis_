# 2026-10-31 — Background overlay timing and additive camera impulse

Source: user's request; GDD `@tag:visual-audio`; existing ADR-016 camera ownership and bounds contract.

Added Fade In Frames and Hold Frames alongside Fade Out Frames, Flash Color and Peak Alpha in the shared background-flash authoring panel. Runtime samples all phases on the existing unscaled clock. Timeline duration and rescaling include the full envelope. Zero phases skip safely; new fields default to zero and existing saved fade-out durations remain unchanged. No authored combat values were retuned.

Confirmed that the camera extension's old Finalize position lock absorbed Noise-stage impulse displacement. Both HitData and ActionEventData ultimately use the same impulse pipeline. The extension now snapshots composition corrections before noise, applies bounded camera animation/legacy locking to that composition, and preserves the final impulse displacement and orientation. Finalize ordering remains in place for group framing and the second Body callback after Aim.

Validation: Unity compilation succeeded. The focused native EditMode run passed **34/34**, with no failures/skips: ScreenImpulseCameraTests 13 (including four flash tests), CameraAnimationSequenceTests 17, and four CameraPositionControlPreservesImpulseNoise cases. `git diff --check` passed for the changed source files. Re-run from Tools > Project > Combat > Run Overlay and Camera Tests; XML and summary are written under Temp/CombatPresentationTests*.

An earlier broader run passed 58/64 and failed six existing MainDimensionSlashTests: BarrageAndExistingCutProvideTwentyLowImpactBudgetedWindows (saved ratio sum 1.58 vs 1), CameraSequenceSurvivesNaturalCompletionAndStaleReleaseCannotCancelReplacement (sampled size 2.5 vs 5), JudgmentFieldIsDetachedAndUsesEnlargedWidthAndHeight / ProceduralPreviewRestoresHiddenActorAndSeedIsRepeatable / ViewportFittedCutsReachBeyondVisibleWidthAndHeight (more than one matching event), and TimedZoomSurvivesNormalActionEndAndReturnsAfterAuthoredHold (inactive-object coroutine log). These broader failures were not repaired or verified against a clean baseline as part of this scoped change. The four new camera-composition cases passed in that run as well.

Tooling: this connection does not expose the test-start command. Added a scoped editor menu runner with callback registration across domain reload. Unity initially imported the standalone new flash test script without including it in compilation; placing the checks in the existing camera-feedback test file resolved discovery. No package/build configuration changes were made for this task.

Human gameplay review remains pending: tune fade/hold values, observe enemy telegraph visibility, and compare hit/event impulses during position hold, slow motion, room-edge framing and interruption. Existing visual/audio feedback and input/state behavior remain unchanged.
