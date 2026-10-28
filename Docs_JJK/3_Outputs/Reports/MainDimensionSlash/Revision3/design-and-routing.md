---
slug: main-dimension-slash
status: in-review
source: manual
gdd_tags: [combat, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Execution Breakdown and Judgment Cut Revision

Direct user requests2026-10-28 supersede the prior single-action presentation. Keep Judgment Cut, make its visuals world-anchored and30% larger, and widen the generated size range for stronger hierarchy. Restore the original seven-contact actor/sprite barrage as Execution Breakdown, retaining hits/VFX except the final upper cut. Replace that final drawn slash with a screen-length foreground procedural transparency tear through a white overlay. The user's latest architecture correction makes Execution Breakdown an ordinary combo step after QTE Dimension Cut: NA during Recovery advances the tree.

## Concrete routing

- Judgment Cut retains its current Skill ID and Passive4/five-paid-cuts gate,20 Low contacts and enabled crits. Detached visual roots eliminate parent translation/rotation; the action component retains ownership and destruction/visibility cleanup. Dimension/spread/width scale1.3 implements the requested30% increase. Starting range3–46 with viewport maximum1.8 diagonals, width0.08–1.8; five screen-centered cuts are guaranteed at least1.25 viewport diagonals, so both ends extend beyond opposite edges. Others retain random screen positions and sizes. Squared width sampling preserves fine cuts alongside larger accents. Their inter-cut spacing is2 logic frames with1–3-frame reveals and restored16–27-frame decay. The foreground sorting share is50%. Viewport fitting preserves the authored length ratio.
- `QteSwapDefinition.introComboTree` is optional generic content. Main authors a DimensionCut root followed by `ExecutionBreakdown` as `defaultNext`, with `autoChainToNext=false`. Legacy QTE profiles leave it empty and retain their isolated single-step entry. ActionResolver.BeginExplicitStep accepts the optional authored tree; subsequent NA calls ResolveNext rather than reselecting a standalone action.
- The QTE root copies current Dimension Cut animation, motion, hit and VFX data. Only its Recovery changes to a starting120 logic frames (2 combat seconds), with no overlapping Cancel window. Active ends26; Recovery27–146 accepts normal attack; Done147 exits without executing the next node. Ordinary paid Dimension Cut remains3/23/6 with its existing32-frame total.
- The normal input buffer and interrupt policy govern input. NA during Recovery advances once to the next node, at tree depth2 under the same Dimension Cut action context. It commits no additional cached resource and cannot increment the five-cut gate because the step is ExecutionBreakdown. Follow-up execution is player-driven rather than another free intro, so it can own its existing bounded camera envelope. Main's old standalone/timer/tag prototype is removed; there is no new passive availability rule, custom long-duration NA queue or action priority contest.
- Original84-frame sequence, actor clip and seven contacts12/20/27/33/38/42/54 are restored. Original damage shares8%×6 +52%, multiplier5,2/2/6 authored holds and final Heavy tier remain starting balance values from the prior reviewed build. Original burst/residual/seam/shards assets and audio are retained.
- At54, replace only the old final slash sprite with a30-frame foreground screen mask. The bright overlay contains an actual alpha hole, a long diagonal cut that narrows and fragmented holes that dissolve the bright sheet. The scene shows through those holes; a black bar is not painted over gameplay. Foreground order12000 stays below screen-space overlay HUD. Actor and mask remain separate.

The supplied images demonstrate a bright field with a dark/transparent diagonal seam, then a narrowing seam as the bright field recedes. They do not establish the reference game's internal particle/shader implementation. The procedural action event is reused with a screen-mask material. The subsequent impact follow-up adds an optional refraction wave/RGB split/localized blur through one gated stock URP fullscreen feature, as documented in [ADR015](../../../ADRs/015-action-owned-cut-refraction.md). No package, global VFX manager or build change is required.

Validation: optional legacy/author-authored tree resolution, NA at first/last Recovery frames, skipped-input completion, free cached-charge preservation, normal Skill/QTE regressions; seven authoritative Primary contacts and original VFX tracks; world-position invariance under player movement/rotation;30% geometry scale and broad size hierarchy; bright-mask hole visible in real capture and cleanup on cancel/disable. If the two-second recovery feels too long, reduce its authored Recovery frames; no timer code change is needed. Retain original scene and shared workspace edits. Workflow: `/implement-feature` within the existing action architecture; concrete prototype precedes final feel review.
