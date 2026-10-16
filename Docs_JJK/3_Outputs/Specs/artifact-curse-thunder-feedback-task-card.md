---
slug: artifact-curse-thunder-feedback
type: feature
status: implemented-awaiting-feel-review
source: manual
gdd_tags: [combat, items]
owner: Codex
workflow: implement-feature
human_checkpoint: final-feel-review
blocked_by: []
---

# Curse and Thunder Artifact Feedback

User authorizes implementation within the existing procedural field/area view architecture. The latest owner correction supersedes Scales' ground lift: its body shield, circular damage area and attack expansion now share the player collider center, with zero offset and no ground snap. Targeted artifact bursts use the target body collider's bounds center as the shared query and visual center. Chrono visualizes its existing armed Mark, consumption, damage and Bleed; no new Mark damage-over-time rule. Shin's Dagger receives a sky lightning bolt and ground sparks matching its saved circular hit radius (3.5). Saved rules, ratios, cooldowns and lifetimes remain the gameplay authority.

GDD @tag:items describes authored runtime effects and retired legacy artifacts; the older artifact table does not describe these Phase-1 behaviors. Current saved Phase-1 assets and explicit user instructions define this follow-up.

Design/architecture: extend ArtifactAreaVisual with an authored ThunderStrike style using shared line/sprite materials and bounded particles. Extend particle volume with opt-in ground anchoring, and pass the resolved center to both hit query and presentation. No collision objects are added. Ground/OneWayPlatform surfaces are queried; triggers and non-supporting normals are ignored, and no-ground fallback keeps the original anchor. World strikes lock their center when spawned; following Scales re-resolves each pulse. Chrono gets a curse glyph, faint body seal, and a brief flash/sparks on the marked enemy's existing damage event, with unsubscribe on removal/disable. Indefinite Dagger Focus remains visually hidden.

Starting presentation values: lightning height 6 units, 0.12s expansion and 0.5s tail, 32 ground sparks, 16 curse damage sparks, faint body seal alpha 0.12. Ground probe starting distance 12 units. These are tunable readability values. Test: identify Mark application, cursed damage, and lightning impact in 8/10 demonstrations at combat zoom; reduce fill/count if silhouettes disappear and strengthen glyph/bolt rims if identification fails. Ground burst boundary must match the same circle as the hit query.

Experience filter: clarity through curse glyph and exact impact footprint; immediate damage response without delayed hit scheduling or action locks; satisfaction through bolt, core flash and particles plus existing damage presentation; fit through restrained scarlet curse and yellow lightning, glow and ground sparks as requested. Audio additions and gameplay camera changes are outside the requested visual work. No new player state/resource transitions.

Validation: Unity compilation, focused sequence/geometry/lifecycle checks, physics ground fixtures, isolated rendered snapshots. Stress repeated reuse, pause sampling, disabled targets and multiple overlapping strikes. Final combat feel and crowd profiling remain human review.

Implementation status: code and procedural assets delivered. The prior isolated Unity run passed 29 artifact checks in Reports/FieldEffectVisuals/curse-thunder-tests.xml. Follow-up fixes ignore stale damage callbacks on destroyed/disabled owners and center target-triggered artifact areas on the target body collider. Current Scales and Crest work, including updated validation, is tracked in artifact-crest-body-feedback-task-card.md; the earlier Scales ground offset is superseded. Combat feel and crowded-combat profiling remain human review.
