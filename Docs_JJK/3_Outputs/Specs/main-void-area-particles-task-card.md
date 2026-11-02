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

# Main Void Area particles

Latest color tuning: user requested a brighter flame-like base from blue/violet references. Added pale cyan-blue root highlights fading into blue-violet flame and dark smoke, plus additive floor glow. Unity compilation/rendering and existing focused checks passed. Updated motion preview is available for human feel review; gameplay fields remain unchanged.

Current visual revision: user supplied two dark-flame references and rejected the hair-like result. Replaced narrow outlines with broad shaded, curling violet/black flame clusters, a soft ground glow, smoke pockets and small filled embers. Approximately two-unit height and hitbox-width source remain. All base/Lv.3/Lv.4 saved definitions reference the same updated prefab, with 20/20/40-second lifetimes. Subjective in-game review remains pending.

Source: Apricha's 2026-11-02 request and attached purple/black ground-flame reference, followed by feedback requesting twice the height, lower color intensity, broader black smoke and a darker base flame. Workflow: `/implement-feature`, single-agent execution. Current presentation target is approximately two units tall. Final subjective feel remains for human review.

GDD `@tag:visual-audio`: "Character animation can be short and economical as long as the VFX sells the hit." `@tag:combat`: damage and VFX matter more than animation depth. Exact Void Area appearance is specified by this request rather than the GDD.

## Scope and acceptance

- Replace only `ProjectileData_main_void_area` presentation with a dedicated pooled particle prefab; preserve existing damage, hit interval, area effects, action routing and lifetime overrides.
- Emit across the routine hitbox's X size, including the existing projectile size multiplier; respect its horizontal offset and facing.
- Ground the field at the projectile's existing ground-level origin. Its hitbox Y offset/height describes damage reach, not flame height.
- Starting value after feedback: two-unit presentation height, darker/desaturated violet roots and broader charcoal smoke. Inspect full-width and combat-scale captures; reduce smoke opacity if it obscures bodies, increase coverage if floor gaps appear. Pass: coherent smoke-dominant field with a readable top around Y=2 and width matching the hitbox.
- Clear particles on pool return, disable and recast. Preserve scaled-time pause/slow motion behavior.
- Verify Unity compilation, material/shader import, geometry, left/right facing, scaled width, cleanup and pooled reuse; provide isolated Unity captures. Human gameplay/feel review remains required.

## Design / architecture

See `main-void-area-particles-design-spec.md` and `main-void-area-particles-arch-plan.md`. No new combat architecture, dependencies, build settings, scene edits or shared sprite-projectile prefab changes are required. Existing local edits are outside scope.

## Result

Implemented and verified in Unity. Sprite presentation replaced; base 20-second lifetime and the old 0.1-second entry gate preserved. Focused geometry/lifecycle checks and isolated Unity captures passed. See `../TestPlans/main-void-area-particles-test-plan.md` for evidence and pending real-combat/feel checks. Final design/architecture acceptance is requested through review of the concrete diff and captures.

Feedback revision: doubled height to 2, widened smoke particles from 1.1 to 1.8 units, reduced violet saturation/brightness and flame/glow density, and layered dark smoke over the flames. Updated captures and existing focused checks passed.

Accent revision: Apricha requested black glow and upward-floating flame pieces, explicitly allowing aesthetic choices. Added a soft shadow halo and sparse black-core, amethyst-edged fragments that curl upward, shrink and fade away. Both emitters use the existing field width/height and cleanup lifecycle. Expanded Unity checks pass for actual fragment rise, death, fade and pool cleanup. Static captures and `Reports/MainVoidAreaParticles/motion.gif` show the result for subjective review.
