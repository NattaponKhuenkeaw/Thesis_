---
slug: main-dimension-slash
status: draft
source: manual
gdd_tags: [visual-audio, combat]
owner: game-design-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Dimension Slash Art-Direction Generation

## Production Actor Pass

`dimension-slash-actor-source.png` is a later, separate built-in image-generation output: twelve new actor-only poses in a four-column/three-row sheet, referenced against the clean New_sprite idle. Production brief summary: white/lavender hair with trailing tuft, regular white jacket, purple trousers, dark boots and violet sword; brace/load, alternating burst keys, compressed hold, decisive final release, follow-through and recovery; face right, clean transparent background, consistent identity, no baked slash VFX. This paragraph summarizes the production brief rather than claiming to reproduce the exact tool prompt.

The unmodified generated source is retained. `MainDimensionSlashArtPipeline.cs` registers/crops it onto native128x128 cells, quantizes to an18-color palette and binary alpha, and creates the imported clip. VFX derive separately from the retained VFX direction source. Actual import audit and gameplay evidence now live beside this document; the historical development-plate limitations below still apply to those source plates themselves. Final artistic acceptance remains human review.

Built-in image generation. Reference image: `new-sprite-evidence.png` in this folder. This is an asset-development direction plate, not an imported or playable animation sheet; exact frame registration, transparency, palette, anatomy and pixel-cluster QA are still required before runtime use.

## Result and Inspection

- Output: `dimension-slash-art-direction-v1.png`, generated with the built-in image tool and copied into this workspace; original generated output retained.
- New poses and effects were generated; source sprites were not modified. The later user clarification explicitly permits new actor art that matches New_sprite's style.
- Visual check: regular white jacket, purple trousers, pale hair/tuft, violet blade, distinct brace/command/load/release poses, and separate actor/effect rows are present. Transparency is retained.
- This plate demonstrates silhouette/palette direction only. It is not an evenly registered animation atlas: cell margins, relative pose heights, logical-pixel grid consistency, stray/glowing edge pixels and final-blade canvas clipping require cleanup and a dedicated sprite-sheet authoring pass before import. Do not represent it as final game-ready pixel animation.
- Planned next art pass: new actor-only sheet against a fixed baseline, and separate native-grid burst/final flipbooks following `asset-production-manifest.md`; mimic the reference's snapped releases and overlapping residuals without requiring copied reference artwork.

## VFX Refinement

Second built-in output: `dimension-slash-vfx-direction-v2.png`, transparent2137x736 RGBA. This separate effects-only board improves the burst/seam/final shape hierarchy and leaves more negative space. It remains a **visual-development reference**, not an imported native-grid flipbook. Read-only inspection still finds many near-opaque colors and non-binary edge alpha; the prompt's palette/grid constraints were not fulfilled numerically. Do not approve it as production pixel art or silently substitute it for the manifest's authored animation assets.

### Exact refinement prompt

Make a NEW VFX-ONLY style sheet correcting the SOFT NOISY HD GLOW in reference1. Reference2's coarse 4x square pixel art is the required edge/cluster language. Do NOT include characters, swordsmen, actor poses, labels, text, scenery, borders, checkerboard or shadows. THREE ISOLATED DESIGNS arranged horizontally with generous transparent padding: LEFT compact crossing burst cut (two clean thin tapered slash blades, lavender with a SMALL white core); MIDDLE quiet sparse compression seam (a narrow short purple stepped line with tiny lavender sparks and abundant transparent negative space, far dimmer/smaller than burst); RIGHT one dominant GIANT continuous final dimension-cut blade (one broad extremely clean tapered diagonal white-lavender wedge, dark purple inner seam, a few deliberate chunky lavender shards, visibly the strongest largest shape). Each design fully inside canvas, at least64 imagepx clear from edges, no clipping. TRUE LOW RESOLUTION PIXEL ART designed on a coherent coarse logical pixel grid and presented nearest-neighbor enlarged. Hard SQUARE chunky pixels and clean stair-step diagonals, controlled flat limited palette of white, pale lavender, mid violet, dark purple. Target only12 total opaque palette colors and binary clean alpha. NO smooth line antialiasing, NO gradients, NO semitransparent mist, NO glow/bloom/blur, NO dither, NO fine noisy HD texture, NO neon cyan/magenta fringe, NO explosive cloud. Treat all transparent gaps and inside dark-purple seam shapes deliberately. The final effect should read as a single clean cutting blade with an inner split, NOT a lightning explosion. Burst and seam use much less white area than final. This is a precise pixel-art effects development sheet, not finished runtime atlas.

References in refinement:1 `dimension-slash-art-direction-v1.png` as design edit reference;2 `new-sprite-evidence.png` as pixel-cluster style reference. Originals retained; selected output copied into the workspace. Generation used the built-in tool, not the fallback CLI.

## Prompt

Use case: stylized-concept.

Asset type: pixel-art animation visual-development plate for Neo-JJK's new Main Dimension Slash action. This plate is a set of key poses and effect designs, not a finished animation atlas.

Reference image role: use the supplied New_sprite evidence board ONLY for actor identity, proportions, pixel cluster style and palette. The regular white-jacket actor in its top row is the character reference. Do not use the dark coat/cape costume in the bottom row.

Primary request: a carefully composed two-row, four-column sheet with eight isolated designs on a genuinely transparent background. Upper row: actor-only brace, composed burst command pose, compressed pre-finisher hold, and decisive final blade release. Lower row: effects ONLY, no actor: thin onset incision, a family of broad crossing burst slashes with fading residuals, sparse compressed dimension seam, and one giant final Dimension Cut wedge with collapsing shards. Preserve enough empty margin around every design so no design touches another.

Subject: slender long-legged swordsman with white/lavender hair and trailing tuft, small warm face, short white jacket, purple trousers, dark boots, violet blade. The same recognizable actor proportions and costume in all four upper-row poses; side-on view facing right with consistent scale and foot baseline. The body must stay readable and self-contained. No giant slash arcs baked into the actor row.

Style/medium: deliberate high-quality 2D pixel art with hard square pixel clusters and stepped diagonal edges, limited value ramp, clean alpha, crisp silhouettes and economical fighting-game key poses. White/lavender core, saturated purple edge, deep purple negative-space split and dim broken residuals. Principal cutting shapes are sharp; soft supporting glow must never dissolve the pixel core. Tiny readable shapes at gameplay size, not a smooth HD painting with a pixelation filter.

Composition: eight equal areas in a landscape plate, two rows of four columns. Actor row clean and relatively small. VFX row effects materially larger than the body but isolated, with a clear escalation from narrow slash through crossed bursts and quiet seam to dominant single final wedge. Final wedge has a crisp blade, taper, jagged sparse outer shards, and a dark inner tear. The compression seam is visibly quieter than both burst and final designs. Consistent invisible horizontal baselines. No contact-sheet labels, text, numbers, borders, UI, scenery, floor, extra characters, props, watermarks or checkerboard painted into the background.

Constraints: Main identity from the regular white-jacket New_sprite reference only; actor and effects separate by rows; no caped costume; no red/cyan palette substitution; no photographic textures; no antialiased vector curves; no screen-filling white fog; no additional weapons or limbs. Transparent negative space is essential.
