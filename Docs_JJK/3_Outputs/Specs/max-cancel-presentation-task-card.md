---
slug: max-cancel-presentation
status: approved
source: manual
gdd_tags: [combat, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# MAX CANCEL animation and independent VFX lifetimes

Workflow: `/implement-feature`. Source: user's explicit animation sequence and interruption-lifetime request, 2026-10-29. The requested behavior authorizes this presentation/lifecycle change within the existing action and VFX systems. Final visual feel remains a human checkpoint.

GDD: `@tag:combat` legal action interruption and `@tag:visual-audio` readable supernatural effects. The existing ADR-015 live-action-only cut eligibility is superseded only for already spawned visual tails by this request. Hitboxes, costs, input policy, actor hiding and camera ownership remain action-owned.

## Approved behavior

- Use the project's GNF TTF pixel font. Slide in from the left and fade in with ease-out.
- A white text-shaped aura grows outward and fades. During the readable hold, drift continuously right.
- Exit slides right and fades with ease-out. Repeated MAX CANCEL restarts the sequence cleanly.
- Already spawned pooled/sprite and procedural visuals finish their own authored duration through MAX CANCEL, advanced steps, policy interrupts and team/QTE swaps. No new cancelled action events or damage are executed.
- Release actor-hidden flags immediately; old visuals cannot hide the new fighter/action. Detached tails must survive outgoing character disable, preserve their world pose, respect pause/hitstop and clean themselves up.
- QTE impact tails continue after contact. An interrupted pre-contact plunge may fade its existing falling visuals/particles, but must not fabricate ground impact.

## Implementation plan

Keep `MaxCancelFeedback` as a presentation component, with a resource label prefab referencing the existing GNF font for builds. Animate one shared content transform and an additive-looking white duplicate-text pulse on unscaled time, so the label completes during hitstop/time stop; retain the existing red accent and action audio.

Replace action teardown's hard stop with a visual lifetime handoff: procedural fields release their owner and actor flags, then advance on their own scaled clock to `endFrame`; pooled VFX detach without cancelling their already running playback coroutine. Cut distortion follows the visible effect lifetime. QTE plunge visuals release independently with their existing tail timing. Hard stop remains available for previews, pooling, scene/component destruction and explicit reset.

Starting presentation values: 0.16s entrance, 0.50s drift, 0.22s exit, 0.36s aura pulse. Test entrance/hold/exit frame captures for legibility; lengthen hold if unreadable during combat and reduce aura opacity/scale if letters blur. These are starting values for user review, not balance changes.

Response/clarity: transitions retain visual continuity without continuing cancelled gameplay. Satisfaction/fit: pixel typography and moving white/gold/red feedback reinforce the existing next-action audio. Motivation remains the existing MAX CANCEL reward.

## Verification

Native tests cover animation phases/font/pulse/restart, procedural handoff/expiry/actor restoration, ordinary interrupt and MAX CANCEL/step replacement, pooled visual retention, field invalidation/team swap, QTE tails and no ghost impact. Render entrance/hold/exit captures using the existing isolated native test harness without saving the user's dirty scene. Human checks: rapid MAX CANCEL, both facings, swaps/QTE, pause/hitstop and overlapping effects; outgoing damage must still end and effects must expire.

Native result: **155 passed, 0 failed**, 2026-10-29 23:17 Bangkok. Thirteen `ActionVfxLifetimeTests` and two new MAX CANCEL/state-exit integration cases extend the previous 140 focused tests. Runtime/Editor compilation succeeds; fresh console error read is empty. An existing exact floating-point duration assertion was given a tolerance after producing 5.00000048 for an authored five-second window. Twenty-eight native camera frames were captured; entrance/hold/exit composition was visually inspected and the aura pivot corrected to the actual text width. Preview and summary are in `../Reports/MaxCancelPresentation/`.

The previous broader suite's seven unrelated unresolved failures were not rerun or declared fixed by this scoped follow-up. Live gameplay/feel and overlap-density review remain human checkpoints.
