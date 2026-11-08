# Judgement Cut performance — 2026-11-07

Implemented local optimizations in the existing procedural ribbon presentation. GDD: `@tag:visual-audio`, `@tag:combat`; action-owned refraction remains governed by ADR 015.

The saved barrage currently uses 31 ribbons, scale 5.5, 50% foreground sorting and broad, strong refraction. Generation itself measured about 0.04 ms per warm rebuild; rendering the overlapping ribbons was the larger measured cost. This supports optimizing presentation work, but does not identify every contributor to the reported live-combat spike.

## Changes

- `ProceduralSlashField`: bounded shared arrays replace six temporary mesh-building lists. Upload only the used vertex/index ranges, pre-size per-field cut metadata and rely on SetTriangles' existing bounds calculation. Meshes still retain independent copied contents.
- `DimensionSlashRibbon`: calculate constant reveal/decay/fade curves per vertex, collapse wholly invisible ribbons before rasterization and discard pixels whose additive contribution is already zero before evaluating taper/glow/glint math.
- Retain the existing vertex layout, RNG sequence, authored settings, both depth layers, world anchoring, actor control, action timing and lifetime ownership. Gameplay and hit processing are unchanged.
- Add a disposable preview-scene benchmark and nine buffer/geometry/allocation cases to the existing lifetime fixture.

## Measured evidence

Unity 6000.3.4f1, AMD Radeon(TM) Graphics, 1920×1080. Eight **static** Enemy1 prefabs plus CombatBackground_B1, saved barrage settings, seed 1234. Both shader versions use the same generated mesh and refraction source. Four warm-up renders per version, then 24 interleaved pairs per sampled frame with alternating order. ProfilerRecorder measures synchronous camera rendering plus GPU completion/readback; these are **not** pure GPU timings or development-player frame rates.

| Sampled action frame | Original median (ms) | Optimized median (ms) | Median reduction |
|---|---:|---:|---:|
| 0 | 6.078 | 4.956 | 18.5% |
| 24 | 8.270 | 7.571 | 8.5% |
| 42 | 7.944 | 7.558 | 4.9% |
| 64 | 8.132 | 7.232 | 11.1% |
| 75 | 6.662 | 6.023 | 9.6% |

The no-field control medians were 4.859 ms for the baseline material and 5.015 ms for the optimized material, illustrating editor/background noise. Tail latencies are noisy and not uniformly improved: frame 42 p95 was 8.867 ms baseline versus 19.793 ms optimized. **These measurements do not establish elimination of the reported spike.** See [complete paired results](paired-presentation.txt).

Warm generation fell from **12 GC.Alloc events per rebuild to zero** in the actor-free mesh-generation probe; before/after CPU timings were approximately 0.0384/0.0300 ms in separate runs. This does not imply a complete cast is allocation-free: new hosts, meshes, actor-renderer capture and first-use resources remain outside that warm probe. GC.Alloc event timings are not allocation byte counts.

All six RGB8 image comparisons (no-field control and frames 0, 24, 42, 64, 75) had zero changed pixels out of 2,073,600 per image. This checks one deterministic barrage composition; it is not exhaustive visual coverage.

**22/22 native focused tests passed**: nine new buffer/geometry/allocation cases plus thirteen existing action-VFX lifetime cases. Coverage includes maximum counts and depth partitions, overlapping mesh independence, fullscreen/barrage reuse, facing, warm allocations, actor restoration, interruption, pause, QTE/team swaps and released refraction. [Result XML](regression-tests.xml).

## Reproduction and remaining validation

Run the two menus documented in the [regression plan](../../TestPlans/judgement-cut-performance-regression.md). To enable the paired shader comparison, export `Assets/Project/Resources/DimensionSlashRibbon.shader` from baseline commit `d862aef8c8e4d7c4381ac4826843612b755fe243` to `Temp/judgement-cut-baseline.shader`; it is compiled only in memory. Generated captures stay in `Temp/`.

The original 5–8 live-enemy encounter has not been reproduced here. AI, physics contacts, damage feedback, first-use shader stalls and target-hardware p95/p99 require a development-player combat capture. Human playtesting remains the final confirmation of feel and spike behavior.

Existing camera, inventory, character, scene, font and profiling-bootstrap edits were retained. No project settings, dependencies, renderer assets or authoring data were changed by this task. Changes are uncommitted for review.
