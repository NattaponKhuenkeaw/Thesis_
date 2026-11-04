---
slug: goenitz-frenzy-palette
status: approved
source: manual
gdd_tags:
  - visual-audio
  - characters
owner: implementation-agent
human_checkpoint: not-required
next_agent: code-review-agent
blocked_by: []
---

# Goenitz Frenzy Palette Verification

GDD: `@tag:visual-audio` readable, economical animation; `@tag:characters`. User requested the existing full-reference mapping within Blood Frenzy feedback, then reported original dash sprites leaking.

Run `Tools/Project/Validation/Goenitz Frenzy Palette` in Unity. The validator uses isolated preview scenes and writes `../Reports/GoenitzPalette/unity-validation.json`.

## Executed on 2026-11-04

| Check | Result |
|---|---|
| Palette shader supported, imported and compiled | Passed |
| All 21 sampled source colors equal their red target controls after GPU rendering | Passed |
| Repeated GPU palette comparison with non-white renderer tint | Passed |
| Unmatched color and texture alpha preservation | Passed within render checks |
| Flash-only default, repeated ticks, reentry, same-renderer recolor flag change, source swap, mode exit, destroyed source, disposal | Passed |
| Ten samples of the real `goe_Dash.anim`: palette retained; flash sprite and authored visibility agree | Passed |
| Actual `PlayerMovement.SpawnAfterimage` copies source sprite/material in normal and Frenzy states; red ghost retains snapshot on mode exit | Passed |
| Unity console error query after checks | Zero entries |
| `git diff --check` | Passed |

Six named validation groups passed, zero failed. No source sprite/animation/import/project-setting changes. Shader preserves the installed URP Sprite-Lit shader's lit, normal and forward structure; recolor occurs before tint and lighting in color passes.

## Gameplay Review

In a combat/Training session, enter Frenzy, dash repeatedly in both directions, and inspect body/ghost shading under room lighting. Exit through AE depletion or Shin, swap away and back, and reset Training. Confirm normal blue returns on the body, existing red ghosts finish their fade, and other characters keep their own appearance. These are recommended visual acceptance checks; the automated preview-scene results above do not claim a full combat playthrough.
