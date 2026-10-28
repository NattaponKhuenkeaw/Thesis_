---
slug: main-dimension-slash
status: needs-human
source: manual
gdd_tags: [combat, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Procedural Dimension Slash Revision

Revision3 adds the user's latest changes: Judgment Cut world anchoring,30% overall scale and a wider procedural size hierarchy; restored Execution Breakdown sprite barrage with a procedural foreground transparency tear at its finish. The user clarified routing during implementation: NA in QTE Dimension Cut's Recovery should advance a normal combo step. The prototype's standalone ready-tag action was replaced by an optional authored QTE ComboTree and existing ResolveNext routing. Recovery starts at120 frames for the suggested2seconds. Animation Editor now lists **QTE Entry Combo**. [Current design](../Reports/MainDimensionSlash/Revision3/design-and-routing.md).

Revision3 verification:28 focused tests pass; six live Judgment casts deliver20 hits each with stable world anchors, and Recovery NA advances to ExecutionBreakdown at depth2 with7 contacts and unchanged cached cuts. Skipped input, cleanup and pause/expiry checks pass. [Current report and previews](../Reports/MainDimensionSlash/Revision3/implementation-report.md). The text below records Revision2 and its earlier validation.

User playtesting found the first sprite barrage too stiff. Rebuilt its slash presentation as independent purple procedural ribbons: sliding alpha reveal, moving glow head, varying length/width/angle/timing/direction, then shrinking fade. A later user correction enlarges it to nearly full-screen and splits approximately20% into a higher foreground pass. The normal pass matches existing VFX sorting.

Actor presentation now uses anticipation/lunge, scoped disappearance during the barrage, New_sprite jump poses for the returning plunge, then a second Skill node copying existing Dimension Cut motion/animation.19+1 Low contacts retain the prior damage budget. Critical damage remains enabled following the user's typo correction; this action separately opts out of global hitstop/impulse and generic sparks.

22 focused tests pass. Six live casts each deliver20 Primary hits, retain timeScale1 including crits, consume once and restore actor visibility. Disable-while-hidden, pause/expiry and startup-damage checks pass. Requested FPS caps are recorded separately from measured25–37 capture FPS. Selected regressions are137/148;11 existing fixture failures are classified and retained in XML.

See [review report and updated video](../Reports/MainDimensionSlash/Revision2/implementation-report.md). Protected inventory/progression/package/build/physics/timing files match preflight. Test_Playground received a shared-session save to camera/activation data; its current version is retained and recorded as changed. No commit/publish/merge performed. Human feel review remains the next checkpoint.
