---
slug: main-dimension-slash
status: in-review
source: manual
gdd_tags: [combat, visual-audio, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main Dimension Slash

Following the user's design and architecture approvals, implemented a separate Passive4 Skill opportunity earned by five paid Dimension Cut starts. Reused the resolver, Potential runtime counters/tags, Skill/HUD availability, damage pipeline, pooled VFX and existing audio. Added opt-in start provenance and guarded Skill commitment so previews/QTE reuse cannot spend or earn the opportunity incorrectly.

New actor-only poses and native pixel VFX provide six accelerating contacts, a compression beat and giant final cut. Capture exposed a doubled VFX offset and finite Animator clock drift; action-origin sprite pivots and an opt-in action-frame actor sampler resolved those in this new action. Camera presentation uses accepted ADR-013's token-owned bounded Cinemachine contribution.

The19 new tests pass;134/137 across selected fixtures, with three existing fixture assertions classified in the report. Six actual prefab casts register seven Primary contacts each, consume once and release camera; live pause/expiry/startup interruption checks pass. PNG capture overhead prevents an actual high-FPS performance claim. The user confirmed intentional scene/run-inventory saves during the shared session; those current files are retained. Meta progression/package/physics/timing hashes remain unchanged.

See [implementation report and footage](../Reports/MainDimensionSlash/implementation-report.md). Human art/game-feel review, the wider integration/performance matrix and regression fixture corrections remain. Changes are uncommitted on `codex/main-dimension-slash`; no publishing/merge occurred.
