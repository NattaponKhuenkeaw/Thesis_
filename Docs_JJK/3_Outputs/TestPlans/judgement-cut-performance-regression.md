---
slug: judgement-cut-performance
status: draft
gdd_tags: [visual-audio, combat]
owner: implementation-agent
human_checkpoint: not-required
next_agent: human
blocked_by: []
---

# Judgement Cut performance regression

GDD: `@tag:visual-audio`, `@tag:combat`. Preserve the existing composition, damage, timing, visibility, world anchoring, and detached tails.

## Automated checks

Use **Tools/Project/Combat/Validate Judgement Cut Performance**. The existing `ActionVfxLifetimeTests` fixture includes bounded mesh-buffer coverage: maximum clamped ribbon counts, zero/all/mixed foreground partitions, simultaneous fields retaining independent meshes, fullscreen/barrage reuse without stale vertices/indices, both facings, and allocation-free warm mesh generation. Existing lifetime checks cover actor restoration, action cancellation, QTE/team swaps, pause, expiry and released refraction.

Use **Tools/Project/Combat/Profile Judgement Cut Presentation** for the isolated 1920×1080 presentation probe. It clones saved Judgement Cut settings and renders eight static Enemy1 prefabs plus CombatBackground_B1 in a disposable preview scene. It never saves a scene or authoring asset. Output: `Temp/judgement-cut-presentation.txt`.

For a paired comparison, export the pre-change ribbon shader to `Temp/judgement-cut-baseline.shader`. The tool compiles it only in memory, alternates old/new material order, warms both versions, and compares RGB8 captures at the same frame and seed. Inspect shader/import errors, medians, p95, and image deltas. GC.Alloc is reported as allocation **events**, not bytes. The render marker includes submission and synchronized GPU readback; it is not an isolated GPU timing.

## Required human combat confirmation

1. Profile a development player on the affected hardware in the combat map, with parallax and 5 then 8 live enemies. Keep resolution and quality identical for before/after runs.
2. Compare idle combat against first and repeated casts. Separate generation, render/GPU, GC, physics, AI, audio, hit feedback, and shader warm-up costs. Check p95/p99 spikes as well as averages against the project's 60 FPS target (16.67 ms total frame budget).
3. Exercise both facings, the fullscreen finisher, hitstop/pause, interruption, team/QTE swap and leaving the room during a tail. Damage contacts, actor restore and tail expiry must retain their existing behavior.
4. Confirm the generated glow, foreground/background ordering, refraction, readability and rhythm still feel correct. Isolated static-prefab timings cannot establish live encounter frame rates or guarantee removal of the reported spike.
