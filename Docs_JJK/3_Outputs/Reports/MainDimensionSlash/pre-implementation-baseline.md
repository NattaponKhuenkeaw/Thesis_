---
slug: main-dimension-slash
status: draft
source: manual
gdd_tags: [combat, characters]
owner: qa-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Pre-Implementation Main Baseline

Executed 2026-10-27 at17:26 Bangkok time, while the architecture checkpoint remained pending. This is fresh evidence for the existing Main kit and shared damage pipeline, **not evidence that Dimension Slash is implemented or passes its feature requirements**.

## Command and Result

Unity MCP invoked the existing verified menu item `Tools/Project/Combat/Validate Main Potential`. The existing runner creates an isolated additive scene, executes the two fixtures, then restores the prior scene in its finally block. No validation scripts were added or changed.

| Fixture | Total | Passed | Failed | Skipped |
| --- | --- | --- | --- | --- |
| ShadowMeterTests |61 |61 |0 |0 |
| CombatDamageTypeTests |18 |18 |0 |0 |
| Total |79 |79 |0 |0 |

Duration:5.132352 seconds. XML records start10:26:25Z and end10:26:30Z. Both run start and output modification time were verified newer than the captured preflight timestamp, so the report is not a stale historical result.

- [Raw result](main-potential-baseline-tests.xml).
- [Summary](main-potential-baseline-summary.json).
- [Preflight hashes and invocation timestamp](baseline-preflight.json).

## Scope and Preservation

The fixtures establish a passing baseline for current Main Shadow Meter/cached-cut/Potential behavior and shared damage contracts covered by those tests. They do not cover the proposed five-cut window, Skill follow-up transaction, scoped camera extension, imported new sprites, real frame-rate contact integration, reference-like rhythm, or final art quality.

SHA256 comparisons found no changes to the nine protected files: CharacterObject_main, ActionResolverTable_main, Player prefab, Test_Playground scene, saved meta/run inventory assets, package manifest, Physics2D settings and TimeManager settings. Unity MCP confirmed the original Test_Playground scene active/loaded and isDirty=false after the run.

## Pending Gate

The [architecture proposal](../../Specs/main-dimension-slash-arch-plan.md) and proposed ADR-013 remain `needs-human`. The selected workflow requires architecture approval before runtime implementation. This baseline does not satisfy that checkpoint or authorize the proposed code/data changes. Feature QA cases remain Not run.
