---
slug: judgement-cut-performance
status: needs-human
source: manual
gdd_tags: [visual-audio, combat]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Judgement Cut performance

## Goal and intake

User reports a spike casting Judgement Cut in a combat map with 5–8 enemies and parallax. Reduce presentation cost while preserving saved tuning, generated composition, damage, timing, actor visibility, world anchoring, and detached tails. PC target: stable 60 FPS (project optimization policy); the report has no supplied capture or hardware details.

## Routing and context

Workflow: `/fix-bug`, within the existing procedural ribbon / action-owned refraction architecture. GDD `@tag:visual-audio` requires large readable supernatural slashes; `@tag:combat` owns the existing action semantics. Related decision: ADR 015 (action-owned cut refraction).

Inspect `ProceduralSlashField.cs`, `DimensionSlashRibbon.shader`, `CutImpactRendererFeature.cs`, `CutImpactDistortion.shader`, and `CombatPlayerState.cs`. Profile before production changes. Use the current `fix/judgement-cut-performance` branch; retain pre-existing edits.

## Implementation boundaries

Local generation/rendering optimizations only. Reuse existing mesh representation and action ownership. No authored visual/feel reductions, damage changes, new rendering architecture, dependencies, project/build/CI settings, saves, or scene edits. Any needed design/architecture change requires a separate human checkpoint.

## Acceptance and validation

- Measure generation time/allocations and presentation rendering before/after with identical saved settings.
- Preserve seed determinism, both depth layers, facing, full-screen cut, pause/hitstop, actor restore, and tail cleanup.
- Run focused Unity regression checks and inspect compilation/console.
- Distinguish an isolated presentation benchmark from a full 5–8 live AI combat reproduction; document remaining limits honestly.
- Write a regression plan and session DevLog. Human gameplay review remains required for final feel and target-hardware confirmation; no merge/publication is authorized.

## Implemented result

Local buffer reuse and shader culling/temporal evaluation implemented. 22/22 native focused checks pass. The warm mesh-generation probe records 0 GC.Alloc events versus 12 before buffer reuse. The paired static-enemy/parallax benchmark shows approximately 5–18% lower render medians, with exact RGB8 matches at all six sampled frames. Full live-enemy spike reproduction and target-hardware p95/p99 remain for human combat validation. [Measured report](../Reports/JudgementCutPerformance/README.md); [regression plan](../TestPlans/judgement-cut-performance-regression.md).
