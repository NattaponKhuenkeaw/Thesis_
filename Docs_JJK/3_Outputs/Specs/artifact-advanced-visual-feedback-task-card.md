---
slug: artifact-advanced-visual-feedback
status: needs-human
source: manual
gdd_tags: [combat, items]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Artifact Advanced Visual Feedback

## Goal and Authorization

Owner requested: remove indefinite effects from StatusContainer and continuous field feedback; classify Urn's Crit benefit as an Effect; add a faint blue hexagonal Scales shield with enter/loop/exit and a damage-pulse expansion matching its radius; add Chrono Mark, blood explosion, and Claw feedback.

Owner clarified that the Mark should have visual feedback for readability and may have a duration. Use the existing 10s armed mark window rather than inventing a new gameplay value. The Mark expires or is consumed, then its visual ends. Claw keeps its saved duration of 7s. Scales retains its saved 10s area lifetime, 2s pulse interval, and 3-unit radius.

Workflow: `/implement-feature`, with the approved shared field presentation architecture and the existing Particle runtime-prefab / Explosion presentation hooks. No combat authority, package, build, or dependency replacement is proposed.

GDD `@tag:combat` and `@tag:items`; follow inspected saved Artifact content rather than older generator values.

## Implementation Extension

- One shared finite-duration presentation eligibility helper for the status HUD and receiver.
- Urn Crit benefit uses `RuntimeEffectKind.Effect`; both its indefinite benefits and indefinite debuffs retain gameplay while producing no status-container or field loop.
- `FieldEffectVisual` gains Mark and Claw motifs through the same bounded pool/lifecycle.
- `ArtifactAreaVisual` supplies faint hex shield and radius-accurate pulse/blood explosion presentation, using shared materials and procedural textures.
- Particle runtime passes its existing center/radius and successful damage pulses into an opt-in area visual. Natural termination plays exit; teardown clears immediately; pooling reuses views.
- Explosion definition references an optional authored visual prefab; existing explosion code keeps the same center/radius and damage rules.
- Mark recipe becomes a timed debuff for its existing armed window. Its armed-state expiry check applies to consuming rules too, preventing an expired invisible Mark from exploding later.

## Visual Starting Values and Test

Shield enter 0.25s, exit 0.3s, pulse expansion 0.22s; looping outline alpha roughly 0.12 and fill roughly 0.02. Blood burst roughly 0.4s; Mark/Claw use the existing field enter/ending durations. These are starting presentation values, not gameplay timing changes. Test at combat zoom: observers should identify onset/consumption/pulse in at least 8/10 demonstrations, with attacks and silhouettes readable throughout. If unreadable, strengthen edges or shape; if distracting, reduce fill/opacity/size. Expansion must reach the exact authored hit radius without overshooting.

## Acceptance

- [x] Indefinite stat effects remain functional but are absent from StatusContainer and sustained field visuals.
- [x] Scales shield has enter/loop/exit and a successful-damage pulse reaching the true radius.
- [x] Chrono Mark is visible during its finite window and disappears on consumption/expiry; final combat-zoom feel remains human review.
- [x] Blood explosion uses the actual splash center/radius; Claw uses its existing timed effect lifetime.
- [x] No new colliders, delayed damage resolution, or changed damage/heal/radius values.
- [x] Pool reuse, disable/destroy cleanup, zero-time sampling, and expiry tested; death event cleanup wired. In-game pause/death/transition observation remains human review.
- [x] Compilation, 76 passing focused EditMode regressions, and actual Unity rendering checked.

Final in-game feel and crowded-combat profiling remain the existing human review checkpoint.
