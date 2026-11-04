---
slug: goenitz-frenzy-palette
status: needs-human
source: manual
gdd_tags:
  - visual-audio
  - characters
owner: game-design-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Goenitz Frenzy Palette Mapping

User authorized building the mapping after planning a red-outfit palette swap. GDD `@tag:visual-audio` supports economical animation and preserved silhouettes; the supplied screenshot is the requested visual reference.

Found six existing red/blonde limit-break PNGs with corresponding indexed-palette slots. Built 21 exact source/target pairs plus clothing-only, clothing/hair and full-reference profiles. Audited 385 PNGs, including all 261 normal-character frames and 118 effects. All 710,701 visible normal-frame pixels match the target palette slots under the full mapping. No visible source-color overlap was found in those effects.

Produced JSON, CSV, a full hashed source inventory, reproducible generator, and interactive design preview. Headless Chrome passed 33 frame/profile combinations and inspection control checks; visually inspected idle before/after. Original sprites, Unity assets, animations, runtime scripts and project settings remain unchanged. No Unity validation claimed.

Draft design complete; look awaits human review under `/implement-feature` Step 3 before any later architecture/runtime phase. Shared colors within body regions and white-flash readability remain visual-review considerations, not automated guarantees.

## Authorized Runtime Integration and Dash Follow-Up

The user subsequently requested applying this mapping within Goenitz Blood Frenzy Feedback, accepting the full-reference look and authorizing the scoped integration. Added `GoenitzFrenzyPalette.shader`, based on the installed URP 17 Sprite-Lit passes. RGB lookup precedes tint and lighting and handles the project's linear color space. Existing normal-map/mask, normals and forward paths are retained.

`FrenzySpriteFeedback` temporarily uses the palette material on the source body, preserves the existing white overlay, restores the original material on source/flag change and disposal, and reuses the material across mode entries. TeamLoader supplies the Goenitz-specific flag using the kit definition and existing active mode state. No new gameplay state authority.

The user reported original dash sprites leaking through. The dash clip only keys sprite and visibility; the actual issue was `PlayerMovement.SpawnAfterimage` creating default-material ghosts. Ghosts now inherit the source shared material, preserving their current palette while fading even after the body leaves Frenzy.

Executed Unity validation: six groups passed, zero failed, including rendered comparison of all 21 mapping pairs with/without tint, alpha and unmatched controls, lifecycle/restoration, ten real dash animation samples, and actual afterimage creation in normal/Frenzy states. No console errors after validation. `git diff --check` passed. Full gameplay visual acceptance is still a recommended review, not a claimed test result.

User additionally requested Shin: Yaotome retain red clothing after spending Frenzy. TeamLoader now keeps the palette for the configured finisher's active owner-matched cast, through recovery. White flash still follows actual Frenzy only. Action completion/interruption or actor change releases the palette exception; no combat state, costs or duration changes.
