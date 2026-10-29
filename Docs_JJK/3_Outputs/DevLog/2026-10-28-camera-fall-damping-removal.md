# DevLog: 2026-10-28 — Remove Fall and Landing Camera Damping

## Goal

Apply the user's requested simplification of ordinary falling and landing camera behavior.

## What I Did

- Removed CameraLookAhead's downward velocity projection, vertical SmoothDamp and landing offset recovery. The proxy follows player Y directly.
- Removed CameraManager's legacy fall-triggered damping settings, state, API and coroutine, including its combat framing damping override.
- Preserved player-reference resolution, horizontal look-ahead, room confinement, normal Cinemachine damping and action/QTE camera paths.
- Removed eight obsolete serialized settings from CameraSetUp.prefab.

## Root Cause and Decision

The proxy introduced a downward offset while falling and eased it away after landing, creating the extra dip/rebound the user rejected. A disabled-by-default legacy implementation also retained fall-dependent damping complexity. Both are removed; no new tuning parameters or architecture are introduced.

## Validation

- Assembly-CSharp compiles successfully (exit 0) with Unity 6000.3.4f1's Roslyn compiler and existing editor response file, redirecting outputs to a unique temporary folder. Existing warnings remain.
- Scoped whitespace checks pass and removed APIs/settings have no remaining asset/source references.
- Manual gameplay/subjective camera validation was not run. Scenarios are documented in `../TestPlans/camera-fall-damping-removal-regression.md`.
- Other ongoing changes in the checkout were preserved. No commit or merge performed.

## Game Feel Notes

The removal direction comes directly from the user. Final feel remains for an in-editor playtest.

## Suggested Commit

`fix(camera): remove fall and landing damping overrides`

## Follow-up — Lightning Plunge Camera Removal

The user also approved removing Lightning Plunge's independent camera path after inspection identified its forced body-relative position and 0.45-second return as a separate source of vertical movement.

- Deleted the plunge camera controller and its Unity metadata; removed begin/sample/release hooks from PlayerQteIntroState and all plunge state/position/lens overrides from QteIntroCameraImpact.
- Removed obsolete camera offset/return settings from QtePlungeSettings and Main's asset. Preserved the existing unrelated combo-resolution edit and saved placement tuning.
- Normal follow and confinement now own the plunge view. Screen impulse, physical motion, damage, VFX and slowdown paths are unchanged; other QTE horizontal centering and intro pulse remain.
- Updated the canonical GDD camera direction and marked older plunge camera checklist expectations as superseded by the user's latest decision.
- Recompiled Assembly-CSharp successfully (exit 0) with temporary outputs and the deleted source removed from the cached response file. Scoped whitespace and removed-reference checks pass. No automated tests or manual gameplay were run; final subjective feel still needs an in-editor playtest.
