---
slug: artifact-crest-body-feedback
type: feature
status: implemented-awaiting-feel-review
source: manual
gdd_tags: [combat, items, visual-audio]
owner: Codex
workflow: implement-feature
human_checkpoint: final-feel-review
blocked_by: []
---

# Scales Collider Center and Rival's Crest Feedback

The user supersedes the ground-relative shield offset: Scales attack visuals and the circular damage query must expand from the player collider's center. Follow-wearer volumes cache the root body collider, resolve its bounds center each tick, and fall back to the player pivot if no enabled collider exists. Scales has radius 2, vertical multiplier 1, zero center offset and no ground snapping. The pulse snapshots this center for its world-space expansion while the body shield continues following the player.

Rival's Broken Crest gains a dark flame mark on its existing enemy-following Wrath volume. The saved skill trigger, 10s cooldown, 5s lifetime, 2s damage interval, radius 3, source-ATK ratio 2 and damage-taken effect remain the authority. The new view never schedules damage. It shows a broken crest glyph above the target, up to 12 dark violet particle flames around its body, and a subdued circular wave with up to 16 streaks only on confirmed damaging pulses. Enter is 0.2s; exit is 0.7s and stops emission. Disable, death, target loss, pool cleanup and reuse clear live particles and stale pulses.

GDD @tag:visual-audio calls for readable silhouettes, strong attack shapes and burst circles; @tag:combat/@tag:items use authored runtime effects. Phase-1 asset inspection provides the Crest values above because its detailed behavior is not documented in the GDD's retired artifact table. User explicitly authorizes implementation within the existing procedural view architecture.

Experience filter: identify the cursed enemy and its periodic hits without hiding attack tells; immediate visual response to actual damage; satisfaction through glyph flash/flames and existing damage numbers; fit through dark purple fire distinct from Chrono's scarlet curse. No new player state/resource transitions. Readability review: identify Crest vs Chrono and Scales pulse origin at combat zoom, including moving/airborne targets and overlapping effects.

Validation completed in Unity 6000.3.4f1: **31 passed, 0 failed, 0 skipped**, recorded in Reports/FieldEffectVisuals/scales-crest-tests.xml. The new checks exercise collider-centered Scales and Crest's actual saved proc, damaging radius, interval, following, cooldown and ending cleanup. Older Chrono/Dagger fixtures now position their enemies at the simulated hit coordinates, consistent with target-body anchoring. Targeted assets were generated and the five-column enter/loop/attack/exit snapshots were rendered and inspected. The final batch session compiled cleanly and exited successfully. No scene was saved, and no broad gameplay generation or dependency/build changes were requested. Final in-game feel and crowded-combat profiling remain human review.
