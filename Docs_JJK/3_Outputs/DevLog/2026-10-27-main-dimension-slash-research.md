---
slug: main-dimension-slash
status: needs-human
source: manual
gdd_tags: [visual-audio, combat, platform-input, inspirations]
owner: game-design-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# DevLog: 2026-10-27 — Main Dimension Slash Research

## Goal

Research the supplied BBEEx clip and plan Main's multi-burst slash followed by a giant Dimension Cut, using ActionResolver and pixel-art actor/VFX layers.

## What I Did

- [x] Routed `/implement-feature` to the research/design stage using the active Docs_JJK workflow and created the task card.
- [x] Incorporated the user's clarification: NEW standalone action unlocked by a Potential; existing Ultimate and cached Dimension Cut remain separate.
- [x] Inspected the local recording, extracted review contact sheets, and measured limited early background motion with confidence rejection for obscured samples.
- [x] Audited the dirty working-tree action/feedback/Potential code and Main assets without editing runtime content.
- [x] Used Unity MCP read-only scene inspection to verify Test_Playground, camera hierarchy and absence of PixelPerfectCamera matches.
- [x] Inspected only New_sprite for actor visual direction; documented exact 4x exports, costume distinction, baked slash arcs and importer/pivot differences.
- [x] Researched official developer/GDC/Unity/Aseprite sources and wrote a proposed timing/production/test packet.
- [x] Cross-reviewed the design against system and sprite evidence; corrected Skill/Ultimate standalone subtype limitations and damage-shape readability.
- [x] Prepared an interactive proposed timing storyboard in the thread's visualization directory; script interaction checks passed.

## Key Decisions

- Research artifacts describe observations, current implementation and future design separately; BBEEx internals are not asserted.
- Proposed grouped acceleration -> compression -> dominant end cut, with starting values and explicit micro tests.
- Recommended clean regular-costume actor frames and separate pixel slash sheets through existing Unity sprite/action tooling; no package installation proposed.
- Damaging bursts should not be placed inside the current unscaled cinematic freeze, because normal combat tracks are skipped during hitstop.
- Accepted action-owned VFX tail truncation for the first prototype; independent post-exit tails and directional camera paths remain future review questions.
- No branch, commit, merge, scene save, runtime asset edit, pipeline change or team publishing was performed.

## Findings Requiring Later Validation

- Exposed freezeDurationOverride is unused by the inspected executor.
- Critical hits can add pauses to authored-zero-hitstop burst entries.
- Plain StandaloneAction with Skill/Ultimate category can be rejected by FSM subtype checks; category assignment alone is not a valid execution route.
- Potential unlock defaults, broad previous-state conditions, standalone priority and feedback stacking require explicit eligibility tests.
- Existing scalar camera shake is not a directional trajectory; current action VFX stop on exit; persistent impulse tails lack per-action cancellation handles.

These were documented as design constraints, not fixed or filed as unrelated bugs in this session.

## Game Feel Notes

No game-feel prototype was run. The reference excerpt's thin-to-broad cuts, changing directions, overlapping residuals and deliberate decay motivated the proposal. The separate final giant cut is the user's requested addition, not a proven end beat in the clip. Source audio was not auditioned; sound design remains a proposal.

## Next Session

Review `main-dimension-slash-design-spec.md`. Select the contextual trigger, Potential track/tier, cost/cadence, entry context and commitment policy. After human design approval, route to architecture review before runtime implementation, per the selected workflow.

## Artifacts

- `Docs_JJK/3_Outputs/Specs/main-dimension-slash-task-card.md`
- `Docs_JJK/3_Outputs/Specs/main-dimension-slash-design-spec.md`
- `Docs_JJK/3_Outputs/Reports/MainDimensionSlash/` — local reference, source research, resolver and sprite evidence.

## Git Commit Summary

No commit created. Suggested summary if the owner later commits only this research packet: `docs(combat): research Main potential dimension slash action`.
