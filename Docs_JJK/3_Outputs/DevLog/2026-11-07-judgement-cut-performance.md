---
slug: judgement-cut-performance
status: draft
gdd_tags: [visual-audio, combat]
owner: implementation-agent
human_checkpoint: not-required
next_agent: human
blocked_by: []
---

# Judgement Cut performance

Source: direct report of casting spikes with 5–8 enemies and parallax. Workflow: `/fix-bug`, within the existing action-owned presentation. Current implementation uses shared bounded generation buffers and per-vertex temporal shader evaluation with invisible-ribbon/zero-contribution culling. Authored tuning, damage, clocks and lifetime semantics are retained.

Measured warm mesh-generation allocation events: 12 → 0. Paired isolated 1080p rendering medians improved approximately 5–18% across sampled visible frames; all six sampled RGB8 comparisons matched exactly. 22/22 focused native tests pass, including both procedural geometry variants and existing released-tail regressions. The benchmark detaches its camera target before releasing its render texture.

[Evidence and limits](../Reports/JudgementCutPerformance/README.md). [Regression plan](../TestPlans/judgement-cut-performance-regression.md). Full live-AI encounter spike elimination remains unverified; development-player profiling and human gameplay review are still needed. Unrelated in-progress changes were preserved; nothing was committed, merged or published.
