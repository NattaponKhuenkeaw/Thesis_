---
slug: main-void-area-particles
status: needs-human
source: manual
gdd_tags: [visual-audio, combat]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Void Area verification

Latest bright-root color pass: Unity compilation, rendering and existing geometry/motion/pooling checks passed with zero console errors. Visually inspected the combat PNG for pale blue roots, violet-blue folds and dark upper smoke, and regenerated the motion GIF. Base/Lv.3/Lv.4 shared-prefab references and 20/20/40-second lifetimes passed static checks. Confirm root brightness and actor readability in a real room; scene post-processing was not changed.

Latest reference-driven revision (2026-11-02): Unity compilation/rendering and existing width, facing, rise/death/fade, clear/disable and projectile pooling checks passed with zero console errors (237 particles in final sample). Base/Lv.3/Lv.4 saved definitions were statically checked for the same particle-prefab reference, null sprite clips and preserved 20/20/40-second lifetimes. Inspected final combat PNG after removing outline strands and reducing noise frequency; thick flame folds, smoke pockets and tiny filled embers replace the hair-like result. The animated preview advances both particle simulation and shader preview time. Gameplay variant casting and subjective readability in a real room remain human checks.

Unity 6000.3.4f1 compiled the changed scripts. `Tools/Project/Main Void Area/Verify and Capture` passed in an isolated preview scene on 2026-11-02, with zero console errors after the final run.

Verified: no Animator or SpriteRenderer in the new prefab; no old spawn/loop/expire sprite clips wired; 20-second base lifetime retained; former 0.1-second spawn-animation gate retained as a timer; 45-unit emission footprint; 22.5-unit width with size multiplier 0.5; populated entry; left-facing nonzero offset; hitbox Y independent of presentation height; clear/disable/restart; actual GenericProjectilePlayer Play, expiration hitbox disable, PrepareForPool and second Play. After the feedback revision to height 2 and lower flame/glow density, the same checks passed again, with 738 particles alive at the authored width. This is not a measured frame-time result.

Captures in `Reports/MainVoidAreaParticles/`: regenerated full-width overview and combat-scale crop with Main's actual idle sprite and EnemyCore. Both were visually inspected. The revised field has broader/taller black smoke and subdued dark violet roots; body silhouettes remain readable. Unity was stopped from Play mode to save the revised assets; the earlier authoring guard error was resolved by exiting Play mode. No changes were saved to the working scene.

## Human gameplay checks

- Cast Jailbreak Punisher facing left and right in a real room. Pass: field stays at the cast location and follows the intended floor; horizontal coverage agrees with debug hitbox.
- Check an enemy near each X edge. Pass: visual edge and damage reach agree within the soft flame boundary; existing periodic damage and passive meter effect still work.
- Test unlocked lifetime/size modifiers and recast after expiry. Pass: VFX persists for actual runtime lifetime, scales with the same width modifier, and leaves no stale pooled particles.
- Pause, slow time, swap character, leave the room, and end a run. Pass: particles respect scaled time and existing projectile lifetime/teardown policy.
- New-player/readability check: identify the floor hazard and both bounds without instruction. Stress/skill/abuse check: repeated casts and moving in/out preserve current damage/cancel/resource behavior. Adjust opacity first if silhouettes become obscured, density if floor gaps are distracting, and height if the revised two-unit field feels too tall/short.

These checks and final subjective design approval remain pending; gameplay Play mode was not run. The preview's lifecycle calls validate integration but do not replace a complete combat playtest or performance profiling.

## Halo and fragment revision

On 2026-11-02, Unity compilation and the expanded verification completed with zero console errors. Five emitters are present and populated at entry (854 active particles in the sampled full-width field). A single emitted fragment rises by more than 0.15 units over 0.6 simulated seconds and is gone after its 1.2-second test lifetime. Authored lifetime curves shrink it to zero size and alpha. Clear/disable and actual projectile pool-return assertions include both new emitters; reuse restarts fragments. Width, facing/offset and gameplay definition checks still pass.

The first diagnostic emitted into a stopped ParticleSystem and did not advance; the check now explicitly plays the system before manual emission and simulates fixed timesteps. Final verification passes. Regenerated combat/full-width captures and sampled motion frames were visually inspected. `motion.gif` contains 24 Unity frames at 0.1-second increments. Review whether the near-black halo and sparse amethyst-edged pieces fit the intended aesthetic during real combat; reduce halo opacity if it masks feet or fragment contrast/density if distracting.
