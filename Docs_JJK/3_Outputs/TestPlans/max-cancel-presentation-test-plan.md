---
slug: max-cancel-presentation
status: draft
source: manual
gdd_tags: [combat, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# MAX CANCEL presentation acceptance

Native isolated focused runner: **155/155 passed**, 2026-10-29 23:17 Bangkok. Source: `Temp/main-meter-qte-max-cancel-tests.xml`; persistent summary and native preview in `../Reports/MaxCancelPresentation/`.

Automated coverage: GNF font resource reference, ease-out entrance/fade, expanding white aura, continuous drift, exit/restart; procedural lifetime/expiry, immediate actor restoration and no later ownership of the new action's hidden flags; independent distortion through field invalidation and pause; real MAX CANCEL/state exit handoff; reset/advanced step/cancel/disable releasing timeline VFX; pooled playback-generation protection; captured local trajectory across actor disable; QTE tails after contact and pre-contact fade without impact emission. Existing input/cost/MAX CANCEL/QTE/meter cases remain green.

Manual review:

1. New-player/readability: trigger MAX CANCEL on both facings. GNF text and growing white aura must read clearly over the next Ultimate; the HUD must remain usable.
2. Skill/stress: prepare held Ultimate early and MAX CANCEL repeatedly. Verify each accepted commit restarts the moving callout once and old visual tails expire.
3. Continuity: interrupt a procedural barrage into another Ultimate, advance a combo step, policy-cancel and swap into QTE. Existing visuals finish; no future cancelled gameplay events execute.
4. Abuse: cancel/disable a QTE before contact. Falling visuals may decay, but no unearned ground impact, light burst or damage should appear.
5. Pause/hitstop: detached procedural/QTE clocks pause and resume coherently. Sprite VFX retain their authored clock choice.
6. Density/performance: spam overlapping cuts and swaps, then wait beyond their longest authored duration. No residual meshes, pooled objects stuck active, hidden actors or ongoing refraction should remain. If overlap obscures enemy tells, shorten the relevant authored visual duration or lower its opacity; do not reintroduce abrupt action-teardown deletion.

Starting UI times are 0.16s entrance, 0.50s drift, 0.22s exit and 0.36s aura. Increase the readable hold if the label is missed during combat; reduce aura opacity/expansion if it obscures the letters. These timings still need human feel approval.
