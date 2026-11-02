---
slug: camera-girl-qte-team-ae
status: needs-human
source: manual
gdd_tags: [combat, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# CameraGirl QTE team Aura Energy reward

Source: Apricha's 2026-11-02 request. Award 7 Aura Energy to each living deployed teammate, including CameraGirl and the current active character, when CameraGirl's earned QTE intro actually commits. Apply the reward once on entry, let existing per-character AE caps clamp it, and grant nothing when the QTE is merely ready or falls back to an ordinary swap. No automated script tests, by prior user instruction.

Implementation: add `entryAuraEnergyPerAlly` to QTE entry rewards and set CameraGirl's value to 7. Use TeamLoader's existing roster, living-character and AE APIs. No extra gain multiplier or balance values.

Validation: Unity compilation and serialized asset inspection. Manual review should set AE below cap for multiple living teammates, earn/commit the QTE once, verify +7 for each, verify cap clamping and confirm a readiness-only or ordinary swap grants none. No script tests run. Human combat-feel approval remains pending.
