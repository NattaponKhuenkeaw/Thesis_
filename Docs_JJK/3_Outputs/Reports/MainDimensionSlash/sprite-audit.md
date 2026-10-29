---
slug: main-dimension-slash
status: draft
source: manual
gdd_tags:
  - visual-audio
  - combat
owner: game-design-agent
human_checkpoint: required
next_agent: game-design-agent
blocked_by: []
---

# New_sprite Actor and Pixel VFX Evidence

Inspection date: 2026-10-27. Research only; original PNGs, importer settings, animations, and gameplay assets were not changed.

## Scope and Evidence

Only `Assets/Project/Character/Hero/test_0_Main/sprite/New_sprite/` supplied actor art direction. Main animation YAML was inspected only to identify use of those sprites. The reference board `new-sprite-evidence.png` shows original source crops without resampling; cells are not normalized to world scale.

The GDD `@tag:visual-audio` explicitly prioritizes readable silhouettes, large attack shapes, short economical character animation, and VFX selling the hit (`GDD_Neo-JJK.md:71-82`). This supports a compact actor pose sequence with independently authored slash layers.

## Current Art Observations

- Recognizable Main traits: white/lavender hair with a trailing tuft, small warm face, slender long-legged silhouette, purple trousers, dark boots, purple/white weapon accents.
- The idle, skill, and most attack sheets show a white jacket. `Ch1_Ult1.PNG` and `Ch1_Ult2.PNG` show a dark flowing coat/cape and a purple sword. The folder therefore contains an existing costume distinction; its narrative/design intent is not documented by these assets.
- `Ch1_Ult1` provides a composed horizontal-blade preparation/hold with subtle hair/coat variation. `Ch1_Ult2` provides a crouched forward slash, overhead blade pose, and a strong low-blade recovery silhouette. These make a useful economical actor reference for the proposed action.
- Several attack frames already include broad white slash arcs and purple motion accents in the same PNG as the actor. Reusing them directly would duplicate VFX when the new slash burst is added. The action needs clean actor-only exports or redrawn active poses.
- `main_Idle.anim:24` onward resolves to `IMG_3091.png`; `main_ult.anim:40-54` uses `Ch1_Ult1` and then `Ch1_Ult2`. `main_ult-end.anim:135-141` uses `Ch1_Ult2`.

## Measured Export Grid and Import Differences

Programmatic RGBA inspection confirmed that **every pixel in every 4-by-4 block is identical** in the inspected `Ch1_Ult1`, `Ch1_Ult2`, `Ch1_ATK_3`, `Ch1_Skill`, and `IMG_3091` files. These are exact four-times exports of a coarser logical pixel grid, not native fine-detail HD painting.

| Source | Export cell | Effective logical cell | Current PPU | Current filter field | Current per-sprite pivot |
| --- | --- | --- | --- | --- | --- |
| `IMG_3091.png` | 512 x 512 | 128 x 128 | 105 | 0, Point | (0.5, 0) |
| `Ch1_Skill.PNG` | 188 x 376 | 47 x 94 | 115 | 0, Point | (0.5, 0) |
| `Ch1_ATK_3.PNG` | 436 x 396 | 109 x 99 | 110 | 0, Point | (0.3, 0) |
| `Ch1_Ult1.PNG` | 388 x 364 | 97 x 91 | 120 | 1, Bilinear | (0.4, 0) |
| `Ch1_Ult2.PNG` | 368 x 392 | 92 x 98 | 120 | 1, Bilinear | first (0.46, 0), later (0.4, 0) |

Source anchors: `IMG_3091.png.meta:55,69,149-152`; `Ch1_Skill.PNG.meta:52,66,145-149`; `Ch1_ATK_3.PNG.meta:52,66,146-149`; `Ch1_Ult1.PNG.meta:67,81,161-164`; `Ch1_Ult2.PNG.meta:61,75,155-180`.

Additional evidence:

- Ult1 mipmaps are already disabled (`Ch1_Ult1.PNG.meta:39`), but default texture compression is enabled (`:105`). IMG_3091 default compression is disabled (`IMG_3091.png.meta:93`). This is an inconsistency to evaluate in the later art/import pass, not proof that an in-game defect has occurred.
- Sprite PPU varies across this folder; source canvas dimensions cannot establish a single game-wide pixel density. Compare actor height and logical pixel size in the actual camera before choosing the new VFX PPU.
- Foot-origin pivots use y=0, but horizontal pivots vary intentionally. Bulk recentering existing sprites could change their action alignment. Preserve existing data and author new frames against a consistent foot/weapon registration guide.
- Some inspected PNGs contain nearly transparent nonzero alpha. Do not infer actor dimensions from an unthresholded alpha bounding box; compare visible silhouette and foot position.

## Production Direction to Review

### Actor layer

The user clarified this is a standalone action unlocked by Potential, not an Ultimate replacement. Use the regular New_sprite white jacket/purple trousers as the provisional actor baseline, with the same white hair/tuft, long-leg proportions, and blade silhouette. Keep anticipation readable, hold an authoritative pose through the VFX flurry, use a distinct committed pose for the final cut, and settle into an unambiguous recovery. Do not make the actor perform a full new sword swing for every VFX line.

Create clean actor-only frames. Keep sword, hand, feet, and hair anchors registered across cuts. A held pose can have hair/clothing secondary movement without changing the collision root or moving the actor in world space. Existing Ult1/Ult2 are useful pose references, but their dark coat is an alternate presentation requiring subjective approval; no costume transformation is implied by this design.

### Separate pixel VFX layers

| Asset layer | Shape and motion purpose | Style requirement |
| --- | --- | --- |
| Burst slashes | A small family of materially different crossing cuts with sharply timed reveals | Tapered hard pixel edges; authored stepped diagonals; avoid rotating a soft HD line |
| Burst impact core | Brief compact star/shape at the actual hit locus | High-value white/lavender center, purple surround, strong silhouette |
| Residual cuts | Thin broken trails left behind as the next slash appears | Darker purple, spatial separation, controlled lifetime |
| Dimension seam | A sparse straight/diagonal tension shape before release | Readable negative space; reserve the widest bright shape for release |
| Final cut | Dominant continuous blade/wedge that crosses the arena | Crisp core, lavender edge, dark-purple echo; stable pixel-grid presentation |
| Collapse/debris | Segmented shards and short disappearing accents | Clear dissipation so the room becomes readable again |

The VFX should share Main's white/lavender/purple vocabulary without importing another character design. Keep the dramatic bright core on a pixel grid; any soft supporting aura should sit behind it and should not erase the actor's silhouette. Match apparent logical pixel density at the intended camera view, rather than merely matching atlas dimensions.

### Practical authoring recommendation

Use a pixel-sprite editor with frame holds, onion-skin, and transparent PNG-strip export for the actor and the principal slash shapes. An Aseprite-style workflow fits these existing four-times exported strips; availability on this machine was not verified. Use Unity Animation/SpriteRenderer previews and the existing authored-action tooling for synchronization, with Unity MCP for asset inspection, configuration, and verification when implementation is authorized. AI bitmap generation can supply a concept reference, but final temporal continuity, pixel clusters, pivots, and clean alpha require inspection; a single generated image is not a finished animation asset.

## Validation and Unresolved Decisions

- Provisional costume direction is the regular white jacket and purple trousers because this is a standalone Potential-unlocked action. A dark-coat alternate would require subjective approval. Do not mix presentations accidentally within the sequence.
- Test actor-only playback with VFX hidden: feet and apparent body scale must remain registered, blade hands must not jump unexpectedly, and the final pose must read as the decisive cut.
- Test VFX-only playback on dark and bright gameplay backgrounds: individual burst directions and the final cut must remain distinct, with deliberate negative space.
- Test composited playback at actual gameplay scale in both facings: the actor, target, and relevant danger telegraphs must be locatable throughout.
- Test camera motion and zoom with the final pixel assets: changing screen scale must not make core slash edges dissolve or produce distracting shimmer.
- If body size jumps, fix pivot/PPU/pose registration before adding camera effects. If the flurry becomes a white cloud, reduce overlapping bright lifetime/coverage before adding detail.

No new animation timings, particle counts, damage numbers, or importer values are approved in this audit.
