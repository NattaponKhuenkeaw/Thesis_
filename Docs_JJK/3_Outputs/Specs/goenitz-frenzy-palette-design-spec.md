---
slug: goenitz-frenzy-palette
status: approved
source: manual
gdd_tags:
  - visual-audio
  - characters
owner: game-design-agent
human_checkpoint: not-required
next_agent: implementation-agent
blocked_by: []
---

# Goenitz Frenzy Palette

User accepted this mapping for runtime use on 2026-11-04 by requesting integration into Blood Frenzy Feedback. The selected profile is the full reference. Subsequent dash feedback requires afterimages to inherit the currently rendered palette. The design evidence below records the original draft; the accompanying architecture plan describes the authorized implementation.

## Player Experience and Scope

Proposed Blood Frenzy appearance: red clothing, optionally blonde hair, preserving existing poses, timing, and silhouettes. The supplied red screenshot is the visual target. No gameplay behavior or runtime architecture is implemented by this draft.

GDD `@tag:visual-audio`: "Character animation can be short and economical as long as the VFX sells the hit." This mapping supports the documented readable-silhouette direction. Exact red/blonde colors come from the user's reference and inspected source artwork, not an invented GDD requirement.

## Evidence and Mapping

The original indexed PNGs contain matching palette slots:

- Blue source: `Assets/Project/Character/Hero/test_15_goenitz/Goenitz sprite/Goenitz_idle_0.png`.
- Red source: `Goenitz_limit-break_23.png` in the same folder.
- Six limit-break frames (17, 18, 19, 20, 22, 23) already use the red/blonde palette.
- 21 non-background entries differ between these two palettes. Values are exact original PNG sRGB bytes, not estimated screenshot samples.

Canonical machine-readable draft: `../Reports/GoenitzPalette/mapping.json`, also exported as CSV. Match the source RGB simultaneously, preserve alpha, and pass unmatched colors through. PNG palette indices document provenance; a future shader cannot assume ordinary sampled Unity textures still expose those indices.

| Preview profile | Changed slots | Preserved |
|---|---|---|
| Clothing only | 12, 13, 15, 16, 17 | Hair, skin, accents, auxiliary highlights, outline and dark details |
| Clothing + blonde hair | Clothing plus 4, 5, 6 | Skin, accents, auxiliary highlights, outline and dark details |
| Full reference | All 21 mapped entries | Transparency, geometry, unmatched colors, unchanged skin highlight at slot 11 |

The full reference also alters skin shadows, dark hood/boot details, an accent, and auxiliary highlights. Group names are visual annotations, not segmentation masks. Shared body-region use of a color cannot be excluded by a color-frequency audit. Use the preview's color inspection to locate pixels before approving a selective profile.

## Proposed Interaction and Feedback

Normal appearance → existing Blood Frenzy becomes active → selected red palette → existing mode ends → original palette. State ownership, swap handling, and material strategy belong to a later architecture phase. This visual draft introduces no new audio, camera, particles, timings, input controls or combat rules.

Readability/fit: preserve original shading ramps and silhouettes. Response, resource costs, and combat motivation remain governed by the existing approved kit. The palette should make the state recognizable without obscuring hit feedback.

## Audit and Edge Cases

- Read all 385 PNGs in the sprite folder: 261 normal-character palette, six existing red-character palette, 118 other/effect palettes; nine distinct full PNG palettes.
- All 710,701 visible pixels in the 261 normal frames resolve to the corresponding red palette slot under the full mapping.
- No used duplicate-index/source-RGB ambiguity was found within the normal palette.
- None of the 118 other/effect PNGs contain visible source RGB values targeted by this mapping. This result is limited to this audited folder, not all project artwork.
- Existing red frames pass through unchanged; colors not in the active profile pass through unchanged. Transparent magenta/green PNG entries remain transparent, rather than becoming visible backgrounds.
- Auxiliary highlight/flash frames can contain no clothing colors. A clothing-only profile intentionally leaves those authored highlights unchanged.
- Existing white Frenzy overlay may temporarily hide the red palette; assess later in Unity.

## Review and Verification

Preview includes 11 original frames from idle, walk, attacks, hurt, jump, Ultimate and existing red limit-break artwork. Headless Chrome exercised all 33 frame/profile combinations and color inspection; verified equal dimensions, alpha preservation, and existing-red pass-through. The idle before/after screenshot was visually inspected. No Unity rendering or performance results are claimed.

Design review: choose a profile, inspect skin/hair boundaries and dark details across actions, then assess the chosen look in combat with white flashes and afterimages during later implementation. A failure should first adjust profile membership or individual mappings; use a mask only where shared colors actually prevent the desired separation. This draft adds no balance or timing values.

Selected workflow: `/implement-feature`, design phase. Its Step 3 requires design/game-feel approval before architecture work. The requested mapping is complete; architecture and runtime integration have not started.
