---
slug: main-dimension-slash
status: in-review
source: manual
gdd_tags: [visual-audio, combat]
owner: game-design-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Dimension Slash Asset Production Manifest

Updated after architecture approval and implementation on 2026-10-27. The production assets below are imported and exercised in Unity. The later proposal sections retain the original planning contract and are not a claim that `.aseprite` source files were produced.

## Implemented Assets

- Source drawings: `dimension-slash-actor-source.png` (12 newly generated actor poses) and `dimension-slash-vfx-direction-v2.png` (independent effect designs), retained unchanged in this report folder.
- Reproducible authoring/import: `Assets/Project/Scripts/Editor/MainDimensionSlashArtPipeline.cs`; no desktop art application was installed. Unity-generated `.meta` files are retained.
- Eight native PNG atlases and eight SpriteRenderer clips under `Assets/Project/Character/Hero/test_0_Main/DimensionSlash/`: actor, BurstA/B/C, residual, seam, final, shards. No new contact-spark asset: existing confirmed-hit feedback is reused.
- Actor: 12 poses, 128x128 cells, foot registration x64/y2, pivot(0.5,0), 18-color palette. The clip uses explicit exposures over 84 logic frames. Its new Animator state is bound by exact clip name, speed0, sampled from the action clock.
- Effects: 256x128 cells; seam/final 512x256. They use action-origin pivots so their cut center is at (4,1.6) world units without the old spawner's doubled key offset. Trajectory keys remain zero. Residuals and compression use thin incisions to preserve negative space.
- All new textures use PPU26.25, Point, uncompressed, no mipmaps, binary alpha. See `pixel-import-audit.json` for measured sizes, alpha values and palette counts. Generated HD source drawings are not represented as native pixel atlases.
- Dedicated VFX enum values160–166 and `Main Dimension Slash` library category; pooled effects obey hitstop and action cleanup.
- New generated identity is a reviewable interpretation of New_sprite. Foot/body scale, anatomy, motion and final polish still require human art review; import correctness does not establish artistic approval.

## Original Production Proposal (Historical)

The parent supplied the latest human direction: approved action design; new standalone action; Passive Level 4; five Dimension Cuts build eligibility and then open a Skill opportunity. That trigger belongs to the architecture/runtime slice. It does not change the approved burst -> compression -> final-cut art timing below. The earlier task/design document status fields had not yet been updated when this manifest was prepared; this note does not claim architecture approval.

Subsequent user clarification explicitly permits **new generated actor sprites mimicking the reference motion**, using New_sprite for art consistency rather than frame reuse. The new `dimension-slash-art-direction-v1.png` and its documented prompt provide an initial generated direction plate outside Assets. It is not the registered native-grid runtime atlas specified below.

## Current Authoring Feasibility

| Capability | Verified current state | Production consequence |
| --- | --- | --- |
| Aseprite / LibreSprite / Krita CLI | `Get-Command` found none | Do not claim an available batch sprite editor |
| Common install locations | No matching installation in bounded Program Files / Program Files (x86) / Local Programs candidate locations | No whole-computer scan was performed; an installation elsewhere remains possible |
| Uninstall registrations | No Aseprite/LibreSprite/Krita DisplayName found in the bounded HKCU/HKLM uninstall keys | No installation should be attempted without the dependency checkpoint |
| Unity Aseprite import | `Packages/manifest.json` declares `com.unity.2d.aseprite` 3.0.1 | The importer exists; this does not establish that the Aseprite desktop authoring app is installed |
| Sprite clips | Existing Main actor and VFX use SpriteRenderer object-reference animation curves | Transparent PNG exports and `.anim` clips fit the current asset model |
| Action timeline | `AnimationEditorWindow.cs:52` exposes `Window/Animation Editor`; actor and VFX preview/scrubbing exists | Use the custom editor to validate authored phase timing after supported VFX registration |
| Pooled VFX | `VFXManager`, `GenericVFXPlayer`, `VFXGenericPrefab`, and `VFXAnimatorController` exist | New sprites/clips can use the current pooled sprite path; no dedicated new render feature is needed |

Recommended authoring route: create clean pixel animation source in an already available pixel editor if one is identified; export the exact native logical PNG sheets below; create clips and import settings through Unity Editor/MCP; validate there. If no desktop editor is available, image generation can produce a pose/design proposal, but final animation still needs deliberate pixel, silhouette, palette, registration and exposure cleanup. A generated contact sheet is not automatically a production atlas. Keep any exploratory images outside Unity Assets until validated.

### Existing tool limitations to account for

- `VFXEntry` exposes `vfxClip`, `vfxType`, `vfxPrefab`, trigger, duration and transform keys (`VFXFrameData.cs:31-39`). Runtime can use `VFXType.None` plus a direct clip through `VFXSpawner.cs` and `VFXManager.cs:56-85`.
- The custom editor currently skips an entry with neither a prefab nor non-None type (`AnimationEditorWindow.cs:896`); it does not sample a bare `vfxClip` override as its library preview. `GetOrCreateVfxInstance` and `SampleVfxState` use prefab/library definitions (`:951-975`, `:1037-1103`). Thus runtime clip support does not prove custom timeline preview parity.
- Prefer dedicated library entries for final custom-timeline authoring if approved. Otherwise standard Unity clip preview and gameplay capture can validate direct clips. Do not assign a dummy existing enum merely to force a preview: the runtime uses its existing definition and may ignore the supplied override when that definition exists.
- Main enum values 140-148 are already occupied; Orochi starts at150 (`VFXType.cs`). New symbolic keys/explicit unoccupied values require a separate reviewed registration edit. Tentative names are below; numeric values are deliberately not assigned here.
- The generic prefab/Animator require the existing state `Play` and override clip name `VFX_Default` (`GenericVFXPlayer.cs:49-57,138-142`). Reuse this contract rather than generating new Animator controllers per slash.
- Runtime lifespan comes from entry duration/logic FPS; animator playback can use scaled time when obeyHitstop is true (`GenericVFXPlayer.cs:134-172`). Verify holds, tail lifespan and cancellation in gameplay as well as the scrubber.
- Prefab fallback instances are not tracked by the returned pooled handle (`VFXSpawner.cs` fallback). Do not choose that path casually for action-owned cleanup.

## Proposed Pixel and Registration Contract

All dimensions, counts, PPUs, pivots, and exposures in the remaining sections are **Starting values for production**, not existing assets or measured BBEEx settings. They preserve the approved timing hypothesis and need the visual tests below before acceptance.

**Starting grid: author and export at native logical resolution, with PPU 26.25.** The inspected idle `IMG_3091` is an exact four-times export with 512px cells at105 PPU; 128 /26.25 =512 /105, so the proposed native grid has the same mathematical cell/world size and logical-pixel/world ratio as that idle. This is a concrete matching baseline, not a claim of a game-wide PPU standard. Other existing Main sheets vary in PPU.

- New actor cell128 x128; grid-foot pivot(0.5,0), meaning foot registration at logical x64/y0. Draw the stance against that guide rather than automatically centering the opaque bounds.
- New actor PNG export stays native logical resolution. Do not export at4x and retain26.25 PPU; that would enlarge the actor fourfold. If a4x export is deliberately chosen instead, equivalent PPU is105.
- VFX use the same native logical PPU26.25 and initial scale1 so core pixel clusters have comparable world size. Match shape bounds/placement to the approved damage region; adjust canvas/offset before introducing arbitrary scaling.
- New import proposal: Sprite multiple; Point filtering; no mipmaps; alpha transparency; lossless/default uncompressed art import for the first QA pass. These apply only to new art; existing assets are not bulk-normalized.
- Actor clip contains sprite swaps only. No root-position, scale, AttackAnchor, collision, camera, alpha-hide, or hit-event curves are baked into the PNG animation.
- VFX clips contain sprite swaps only in the first pass. Core is alpha-composited hard pixel art with white/lavender/purple value grouping. Supporting aura must not soften the core silhouette.
- Main body remains in the normal white-jacket/purple-trousers costume. No caped transformation is implied.

## Exact Candidate Asset Paths

Paths are repo-relative proposed new files/folders; none were created in this preparation. Unity will generate `.meta` files during the later approved import pass.

### Editable source and review outputs outside Assets

Root: `Docs_JJK/3_Outputs/Reports/MainDimensionSlash/ArtSource/`.

| New path under root | Purpose |
| --- | --- |
| `MainDimensionSlash_Actor.aseprite` |12 clean actor pose keys on128 x128 canvas, with registration guides on non-export layers |
| `MainDimensionSlash_BurstA.aseprite` | Authored rising diagonal slash |
| `MainDimensionSlash_BurstB.aseprite` | Authored falling diagonal slash |
| `MainDimensionSlash_BurstC.aseprite` | Authored near-horizontal slash |
| `MainDimensionSlash_Residual.aseprite` | Dim broken residual line; no actor pixels |
| `MainDimensionSlash_Seam.aseprite` | Sparse compression seam; no contact spark |
| `MainDimensionSlash_Final.aseprite` | Giant finishing blade, collapse and segmented tail |
| `MainDimensionSlash_Impact.aseprite` | Compact confirmed-contact shape |
| `MainDimensionSlash_Shards.aseprite` | Optional short fading pixel debris |
| `MainDimensionSlash_ExposureManifest.json` | Pose identifiers, logic-frame exposures, cell rects, pivots, PPU and palette notes |
| `MainDimensionSlash_ActorOnly.gif` | Review playback without VFX; use explicit frame holds |
| `MainDimensionSlash_VFXOnly.gif` | Review of isolated effect exposures on dark/bright backing |

`.aseprite` is the preferred source contract if the editor is available. Do not fabricate a renamed file if it is unavailable; use an actual editable layered source produced by the chosen authoring tool and update this manifest accordingly. PNG/JSON interchange is sufficient for Unity, so no new package is required.

### Runtime PNG exports

Root: `Assets/Project/Character/Hero/test_0_Main/DimensionSlash/Sprites/`.

| New filename | Logical cell | Frame count / sheet grid | Full PNG dimensions | Pivot in each cell | Initial use |
| --- | --- | --- | --- | --- | --- |
| `MainDimensionSlash_Actor.png` |128 x128 |12 /4 columns x3 rows |512 x384 |(0.5,0) | Clean Main actor |
| `MainDimensionSlash_BurstA.png` |256 x128 |4 /4 x1 |1024 x128 |(0.5,0.5) | Rising diagonal core |
| `MainDimensionSlash_BurstB.png` |256 x128 |4 /4 x1 |1024 x128 |(0.5,0.5) | Falling diagonal core |
| `MainDimensionSlash_BurstC.png` |256 x128 |4 /4 x1 |1024 x128 |(0.5,0.5) | Near-horizontal core |
| `MainDimensionSlash_Residual.png` |256 x128 |4 /4 x1 |1024 x128 |(0.5,0.5) | Dim residuals |
| `MainDimensionSlash_Seam.png` |512 x256 |4 /4 x1 |2048 x256 |(0.5,0.5) | Compression cue |
| `MainDimensionSlash_Final.png` |512 x256 |8 /4 x2 |2048 x512 |(0.5,0.5) | Finishing blade/collapse |
| `MainDimensionSlash_Impact.png` |64 x64 |4 /4 x1 |256 x64 |(0.5,0.5) | Confirmed contact only |
| `MainDimensionSlash_Shards.png` |256 x128 |4 /4 x1 |1024 x128 |(0.5,0.5) | Optional late debris |

All proposed PNGs use native26.25 PPU. The final-cut canvas represents approximately19.50 x9.75 world units at scale1, burst canvas9.75 x4.88, actor canvas4.88 x4.88. **Canvas extent is not automatically damage extent or opaque extent.** Transparent margins and actual bright-core length must be fitted to gameplay framing and authored range, particularly at room boundaries. Do not fill the whole final canvas merely because it exists. A non-damaging aura/residual may extend beyond the region only when visibly secondary.

### Runtime clips and candidate library names

Root: `Assets/Project/Character/Hero/test_0_Main/DimensionSlash/Animations/`.

| New clip filename | Candidate VFXType name if dedicated registration is approved | Clip responsibility |
| --- | --- | --- |
| `main_dimension_slash_actor.anim` | None, actor track |84-frame actor exposure sequence |
| `main_dimension_slash_burst_a.anim` | `Main_DimensionSlash_BurstA` |4-frame bright slash |
| `main_dimension_slash_burst_b.anim` | `Main_DimensionSlash_BurstB` |4-frame bright slash |
| `main_dimension_slash_burst_c.anim` | `Main_DimensionSlash_BurstC` |4-frame bright slash |
| `main_dimension_slash_residual.anim` | `Main_DimensionSlash_Residual` |12-frame dim decay |
| `main_dimension_slash_seam.anim` | `Main_DimensionSlash_Seam` |10-frame compression cue |
| `main_dimension_slash_final.anim` | `Main_DimensionSlash_Final` |30-frame release through natural finish |
| `main_dimension_slash_impact.anim` | `Main_DimensionSlash_Impact` |6-frame confirmed-contact spark |
| `main_dimension_slash_shards.anim` | `Main_DimensionSlash_Shards` |18-frame optional late debris |

No new character Animator controller, render material, screen-distortion shader, VFX Graph, camera asset or prefab is required by this art manifest. Those would be separate architecture decisions if needed. If dedicated keys are selected, later registration touches existing `VFXType.cs` and `VFXLibrary.asset`; both already have unrelated working-tree changes and must be changed narrowly.

## Default Exposure and Action Timing Packet

Keep the approved Starting timeline: logic0-11 brace; burst contacts12,20,27,33,38,42; compression44-53; finishing contact54; recovery60-83; target cancel66. Logic rate60Hz; contact hitstop adds elapsed time. These timings describe exposure, not unique image count, and do not authorize damage semantics.

### Actor key map

| Key index / label | First logic frame | Exposure through | Required silhouette |
| --- | --- | --- | --- |
|00 Brace |0 |5 | Readable planted sword preparation |
|01 BraceLoad |6 |11 | Lower/compressed stance |
|02 BurstA |12,27,38 |19,32,41 respectively | Economical leading pose with clean blade |
|03 BurstB |20,33,42 |26,37,43 respectively | Counter pose/secondary follow through |
|04 Compression |44 |49 | Quiet held preparation distinct from idle |
|05 FinalLoad |50 |53 | Silhouette anticipates decisive release |
|06 FinalKey |54 |55 | Biggest actor directional accent aligned to contact |
|07 FinalFollow |56 |59 | Strong continuation, no new body teleport |
|08 RecoverA |60 |65 | Settle blade and stance |
|09 RecoverB |66 |73 | Can truncate cleanly on accepted cancel |
|10 IdleBridge |74 |79 | Return to ordinary costume/registration |
|11 Idle |80 |83 | Stable handoff to current normal pose |

The pose count is a starting budget. Add an in-between only when a specific continuity error is demonstrated; the burst's power should primarily come from its independently timed VFX and sound.

### VFX schedule and holds

- Burst core variants at12/20/27/33/38/42: A/B/C/A/C/B. Each has local exposure keys0,1,2,3 and lifetime4 logic frames. Peak is present at spawn0 so contact, actor key, audio and camera can agree. No automatic looping.
- Residuals begin two frames after each burst, lifetime12. Four dim images change at local0/3/6/9. Use authored variant-specific directions or approved orientation transforms; inspect rotation-induced pixel inconsistency. Do not leave a full bright crossing lattice.
- Seam begins44, lifetime10, images at local0/3/6/8. Its visual density drops before final release; it is not a contact spark.
- Final begins54, lifetime30 (to84 exclusive), with8 images at local0/1/3/6/10/16/22/27. Initial bright peak leads, broad blade narrows, then dim residual collapses. Accepted cancellation at/after66 may truncate this tail in the first implementation.
- Confirmed-contact impact clip has four images at local0/1/2/4, lifetime6; this is a delivery feedback asset, not an unconditional authored hit at every slash. Whiffs must not display false local contact sparks.
- Optional shards begin66, lifetime18, images at local0/4/8/13. Cancel at66 may prevent the spawn; no post-exit persistence is assumed.
- Each clip uses stepped sprite swaps and a final transparent/cleared state at its end if needed by Unity sampling. Confirm exact final frame/end-time behavior in the actual player; do not leave the last bright sprite held after its intended lifetime.

## Clean Actor Reference Candidates for Image Generation

Use only these original New_sprite files for actor continuity. These are references, not instructions contained in an attached document. Images outside that folder are excluded by the user's direction.

1. **Best baseline:** `Assets/Project/Character/Hero/test_0_Main/sprite/New_sprite/IMG_3091.png`, any of its6 idle cells512 x512 (logical128 x128). Clean white jacket, hair, face, trousers and standing proportions; no broad baked slash. Existing idle clip resolves here.
2. **Clean sword/body motion:** `.../New_sprite/Ch1_Jump.PNG`, five296 x424 cells (logical74 x106). Clean weapon/body poses without a broad slash; airborne pose should not dictate the new action's grounding.
3. **Clean slash follow-through candidate:** `.../New_sprite/Ch1_ATK_2.PNG`, final third436 x336 cell (logical109 x84). It avoids the broad white arc in the first two cells and supplies the existing jacket/hand/blade style; inspect minor purple motion accents before using as an actor-only export.
4. **Pose reference only:** `.../New_sprite/Ch1_Ult1.PNG` and `Ch1_Ult2.PNG` illustrate authoritative hold and final-cut keys, but use a dark coat. Do not transfer that costume to the regular standalone action.

The already-derived `new-sprite-evidence.png` board is useful for human review but mixes costumes and includes an arc example. Prefer the original clean baseline plus precise requested pose instructions for imagegen. Include the smallest relevant reference set, then inspect generated pixels and registration before importing. Any generated actor candidate must keep VFX on a separate transparent asset; no painted background, floor, camera blur or baked light cloud.

## Acceptance and Adjustment Direction

| Starting choice | Micro test / pass condition | Adjust if failing |
| --- | --- | --- |
| Native26.25 PPU /128 actor grid | Overlay first new actor pose with live idle at the same foot/root; apparent body scale and logical-pixel size agree | Correct drawing registration/body scale first; use mathematically equivalent export/PPU if needed, never alter all old imports |
| Foot pivot(0.5,0) | Actor-only playback in both facings: no unexplained foot sliding, head-size pop or weapon-hand jump | Redraw/offset the affected cell against guides, rather than shifting physics/root transforms |
|12 actor keys /84 logic frames | Normal-speed reviewer identifies brace, flurry, compression and decisive cut with VFX hidden | Improve silhouettes/exposures before adding frames or glow |
|256 x128 burst /512 x256 final | Actual game view at room center and edge: no accidental clipping of the decisive shape; bright core communicates the damage region | Change transparent canvas/shape/offset; reduce oversized non-damaging core rather than masking bad range with more particles |
|4-frame cores /12-frame residuals | Normal-speed VFX-only playback: each leading cut reads, final remains largest, field clears | Shorten/dim residual overlap first; compare contact spacing only after shape readability |
| Final8 images /30-frame tail | No jagged scale morph, retained bright last frame or lingering effect after cancel | Adjust stepped shape progression/end clear and runtime lifespan; do not promise persistence beyond action ownership |
| Point/no mipmaps/lossless proposal | Capture dark/bright backgrounds at actual resolution, both facing directions and proposed camera push | Fix import/palette/rotation sampling or reduce push if edges soften/shimmer; no blanket camera retrofit |
| Separate actor/VFX | Hide each track: actor has no duplicated white arc; VFX has no body/clothing fragments | Clean/redraw source layers before final import |

Design-approved timing is the baseline; a successful Unity runtime pass and human art/game-feel review are still required for acceptance. This manifest is ready for the architecture/production checkpoint and does not bypass it.
