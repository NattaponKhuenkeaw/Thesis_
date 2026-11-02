# DevLog: 2026-11-02 — Main Void Area particles

Minor color feedback: tinted the near-white hot roots slightly lavender by increasing red and lowering green in the HDR root color, retaining the existing blue channel, glow strength, shape and lifetime. Updated the material, shader default and authoring source consistently. Regenerated the Unity preview for visual review.

## Bright flame-root color tuning

Apricha supplied blue/violet fire references and asked for brighter flame-like roots. Added a world-height-based HDR cyan-blue root tint to the existing flame shader, fading upward into blue-violet folds and black smoke. Ground pools now use additive blending and brighter blue/violet colors. Renderer property blocks supply field height; upper smoke, shape, damage and lifetimes retain their existing behavior. No scene lighting, bloom or post-processing changes.

Exited Play mode, compiled and saved the revised materials/prefab, and passed the existing focused Unity checks with zero console errors. Inspected the new combat-scale PNG and regenerated the 24-frame GIF. Static checks confirm base/Lv.3/Lv.4 still share the prefab with 20/20/40-second lifetimes. In-game color/feel review remains pending.

## Reference-driven flame revision

Apricha rejected the hair-like look and supplied two new references. Replaced narrow procedural silhouettes and outline fragments with broad low-frequency turbulent puffs, shaded violet flame folds, black smoke pockets, soft ground pools and small filled cool embers. Reduced emitter density, enlarged puffs, randomized their shading seed and flame rotation, and softened the black halo. Added a world-space ground fade using cached MaterialPropertyBlock data so larger particles cannot visibly extend through the floor. No gameplay fields changed.

Exited Unity Play mode to save assets. Compilation, shader rendering and existing geometry/motion/pooling checks passed with zero console errors; final sample contained 237 particles. Verified statically that base/Lv.3/Lv.4 share the same saved prefab and retain 20/20/40-second lifetimes. Regenerated PNGs and motion GIF from 24 Unity frames; the preview now explicitly advances shader time as well as particle simulation. Real-combat feel approval remains pending.

## Goal

Replace Jailbreak Punisher's persistent Void Area sprite with the requested purple/black dark-flame smoke field. GDD: `@tag:visual-audio`, `@tag:combat`; exact appearance and one-unit height come from Apricha's request/reference.

## Work

- Added a dedicated pooled projectile prefab with ground glow, flame and smoke ParticleSystems, plus procedural URP shading/materials.
- Added optional cached presentation integration to GenericProjectilePlayer. Begin uses resolved hitbox width, horizontal offset and size multiplier; pool return/disable clears particles.
- Rewired only Void Area's projectile definition. Retained the old 0.1-second sprite entry duration as a timer; gameplay lifetime, damage, area effects and action data remain unchanged.
- Compiled in Unity and passed focused editor verification of width, facing/offset, ground height, populated entry, cleanup and projectile Play/expire/pool/reuse. Inspected full-width and combat-scale Unity captures. Fixed a stale reference-sprite path and replaced preview SendMessage with explicit reflection to avoid an editor-only lifecycle assertion.
- Removed Unity's unrelated default-field serialization from the final projectile definition diff. Left pre-existing worktree edits untouched.

## Review

Implementation is ready for human feel review. Actual combat Play mode, pause/slow-time behavior, progression variants and frame-time profiling remain manual checks in the test plan. No commit, merge or team publishing performed.

Suggested commit: `feat(vfx): replace Main Void Area sprite with ground flame particles`

## Feedback revision

Apricha requested twice the height, less intense color, broader black smoke and a darker flame base. Set presentation height to 2 units, widened smoke particles to 1.8 units, reduced violet brightness/saturation and flame/glow density, and placed the charcoal smoke over the flames. Asset recreation now skips saving an already-wired projectile definition, avoiding unrelated serialization changes. Stopped Unity Play mode to save assets, regenerated and visually inspected previews, and reran the existing focused checks successfully (738 active particles at full width). In-game subjective review remains pending.

## Void accents

Follow-up variant fix: Main Ultimate levels 3 and 4 select separate replacement ProjectileData assets. These still referenced the legacy sprite prefab/clips. Wired both to the same particle prefab, cleared legacy visual clips, and retained the 0.1-second entry gate. Their damage and 20/40-second lifetimes are unchanged. Static diff confirms only six presentation/entry-gate fields changed per variant; real-combat level checks remain for the owner.

Apricha requested black glow and flame-like floating pieces, with aesthetic choice delegated. Added a soft near-black halo with a muted violet fringe, and sparse pointed black flame fragments with amethyst edges. Particle noise and rotation curl their upward path; size/color curves shrink and fade them away. Both emitters join existing width/height configuration and clear-on-pool-return/disable behavior. Extended the shader with halo/fragment shapes and added two materials. No gameplay or shared lifecycle authority changes were needed.

Unity compilation and expanded rise/death/fade, width/facing, cleanup and actual projectile reuse checks pass with no console errors (854 particles in the sample). Corrected the motion check to start its ParticleSystem before manually emitting. Generated and visually inspected updated PNG captures and a 24-frame animated preview assembled with Pillow. Subjective in-combat review remains pending.
