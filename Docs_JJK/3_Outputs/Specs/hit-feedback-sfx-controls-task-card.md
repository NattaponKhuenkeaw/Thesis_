---
slug: hit-feedback-sfx-controls
status: implemented
source: manual
gdd_tags: [visual-audio]
owner: Codex
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Hit Feedback SFX Controls

## Request and Scope
User requested volume and pitch controls on Hit Feedback SFX. Workflow: `/implement-feature`.
GDD reference: `@tag:visual-audio` describes combat presentation; exact audio control values are not documented in the GDD and follow the user's authoring request.

## Implementation
- Normal and grab hitboxes expose Volume (0–2) and Pitch (0.1–3) under Hit Feedback (Override), with undo and dirty tracking.
- Both default to 1, preserving existing gain and pitch variation. Existing clip selection and audio manager routing remain in use.
- Controls also apply when a hitbox uses the default tier clip.
- No architecture or authored game-feel tuning changes; no asset values changed.

## Acceptance and Validation
- [x] Serialized fields and animation editor controls exist for normal and grab hitboxes.
- [x] Playback consumes volume and pitch with runtime clamping.
- [x] Diff whitespace check.
- [ ] Unity compilation and audible playtest: change each control, verify hit audio, save/reopen, and undo edits.
- [ ] Verify volume 0 mutes the hit, volume 2 boosts it, and pitch extremes play without exceeding the audio source range.

Existing unrelated workspace edits were preserved. Unity compilation and audible playback were not available through the local command-line project files.
