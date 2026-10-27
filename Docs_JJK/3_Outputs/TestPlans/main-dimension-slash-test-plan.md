---
slug: main-dimension-slash
status: in-review
source: manual
gdd_tags: [visual-audio, combat, platform-input, inspirations]
owner: qa-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main Dimension Slash: QA Plan

Updated 2026-10-27 after approved implementation. See [implementation results](../Reports/MainDimensionSlash/implementation-report.md) for executed checks, current captures, failure classification and remaining cases. The detailed procedures below are the full planned matrix; they are not all certified by the smoke pass. Architecture approval was received (“LGTM, arch approval”).

Fresh pre-implementation baseline:79/79 passing tests (61 ShadowMeterTests,18 CombatDamageTypeTests). Post-implementation:19/19 new action tests pass, with134/137 passing across the six selected fixtures. The three failing assertions and limits of the runtime capture are documented in the implementation report rather than waived as a full pass.

## 1. Requirements and Decision Boundaries

Authoritative request: at **Passive Lv.4**, performing **five Dimension Cuts** opens a timed follow-up on the **Skill command** to execute a separate Dimension Slash action. The existing Ultimate and cached-cut action remain present. The user subsequently requested **new generated actor animation that mimics the reference motion**, with visual consistency based on `test_0_Main/sprite/New_sprite`; this is not a requirement to reuse existing actor frames. Actor and VFX are separate assets. Use the latest task card and approved design spec over their earlier research-stage Normal-input candidate.

Design truth: `GDD_Neo-JJK.md` `@tag:visual-audio` requires strong shapes, hitstop, camera shake and readable danger; `@tag:combat` keeps authored action phases and state-owned motion; `@tag:platform-input` makes PC keyboard/mouse primary. Single-player is documented; multiplayer tests are not applicable.

The [architecture proposal](../Specs/main-dimension-slash-arch-plan.md) uses a distinct **SkillAction in skillActions**, selected above the ordinary Skill only when Passive4 + the one-use ready tag are valid. This is a separate authored action, not a replacement of Main's existing kit. Do not validate the feature by adding a plain Skill-trigger StandaloneAction: current Skill execution requires the SkillAction subtype. The architecture was approved by the user; the implementation report records current coverage. This QA plan was cross-checked against that proposal after the gate audit.

| Parameter / rule | Status before implementation | QA treatment |
| --- | --- | --- |
| Passive track level4 and threshold5 | Direct user requirement | Hard acceptance gate; level3 cannot produce the opportunity |
| Qualifying cut | Proposed: accepted paid Normal-category start of exact `main.dash.dimension_cut`, root step `DimensionCut`, owned by Main on field | One increment per manual commitment, not per hit, target, callback replay, auto-chain step or completed animation |
| Canceled qualifying cut | Proposed: count remains after an accepted paid start | Later cancel/whiff does not refund; rejected preflight never counts |
| QTE intro / clone / off-field reuse | Proposed exclusions | New opt-in `requireManualOnFieldActionStart` uses the originating `actionStartExecution`: non-null, on-field, non-QTE, source/owner matching. Defaultfalse preserves unrelated passives. Same action ID or sprite/step name alone cannot qualify |
| Window length and clock | **Starting value:**1.5 scaled combat seconds; pause and global hitstop do not consume it | Boundary and pause tests below; shorten only if it feels excessively bankable, lengthen if intended follow-up is repeatedly lost after a readable cue |
| Threshold / extra cuts / expiry | Proposed: ordered count+1, then count>=5 adds ready and resets count0; cuts while ready are ignored | No timer refresh, stacked readiness or prebanking. Consume or expire requires five fresh cuts for the next opportunity |
| Swap, defeat, run/loadout reset | Proposed: reuse TeamLoader per-character runtime record | Ordinary swap preserves count; ready expiry continues off-field on scaled Time.time. Defeat and run/loadout reset follow existing record lifecycle. Scene entry alone is not assumed to clear it |
| Resource and cooldown | Proposed: authored cooldown0; no extra AE or cached-cut cost | Existing five manual cuts retain their normal costs. Follow-up can run while Dark Chain cools; it does not spend, restart or reset Dark Chain cooldown |
| Ground/air | Proposed: Both context with MatchInput; no forced ground snap | Test real ground/air input and output, including support loss; do not infer permission from a ground-only preview |
| Movement, invulnerability and cancel protection | Starting choreography / current FSM policy unless later approved otherwise | No teleport, new armor or full-control-at66 promise can be certified from the timing storyboard |
| Burst/frame sequence | **Starting values:** startup11/active48/recovery25, TotalFrames84; contacts12,20,27,33,38,42 then final54; two logic-frame windows; cancel target66; optional tail through83 | Frame0 is initial presentation; Active12-59, Recovery60, Cancel66, Done only above84. Verify exact clip/end sampling rather than assuming the0-83 storyboard is a new timing engine |

## 2. Evidence to Inspect Again After the Diff

Line numbers below locate current code and can shift during implementation. Reconfirm final behavior from code and **live execution**, then update this plan with the tested revision.

| Current evidence | Why it changes the test strategy |
| --- | --- |
| `ActionResolver.cs:70` ResolveInitial; regular category list selection at105; standalone-first at80 | Resolver selection alone is insufficient. A new Skill must reach the real FSM and commit successfully; ordinary Skill fallback must still work |
| `PlayerStateMachine.cs:705` CanStartSkillAction; `PlayerSkillState.cs:107` TryStartFreshSkillAction | Both preview and live paths check SkillAction; buffering/recovery has a second selection path that must agree |
| `CombatPlayerState.cs:387` counter-cost commitment; `PlayerCharacter.cs:431` availability and472 commitment | A qualifying cached-cut cost commits before its start event. Follow-up preflight must not remove readiness or start cooldown; accepted follow-up entry consumes readiness once |
| `PlayerCombat.cs:299` NotifyActionStarted | Notification is emitted when a step is executed. Test auto-chain/step changes and duplicate callbacks so one cut cannot count twice |
| `PlayerCombat.cs:686,734` NotifyActionEnded from ResetCombo and cancellation | OnActionEnded is not proof of successful completion. Counting successful completed cuts requires additional authoritative completion evidence |
| `CombatExecutionContext.cs:7` isQteIntro; source ownership fields; `follow-up-gate-audit.md` | Exact action-ID matching alone can count the QTE intro, which reuses Main's cut action/step. Test the new defaultfalse opt-in filter, originating execution and source/session exclusions |
| `CharacterPassiveController.cs:978,998` action trigger dispatch;2687 runtime tags;2797 counter effects | Test actual ordering of count increment, threshold opening, ready-tag mutation/count reset and tag expiry, rather than assuming serialized effect order works |
| `CombatRuntimeState.cs:107` SetTag uses Time.time;221 expired-tag removal | A scaled-time window pauses during timeScale0; expiry/query order at the boundary can differ unless tested through the actual input route |
| `PlayerSkillState.cs:96` cooldown once per action;153 availability-window clear | Test that a successful follow-up cannot be reselected and that ordinary Skill cooldown is neither accidentally reset nor double-started |
| `FrameDataPlayerState.cs:223,250` FixedUpdate logic catch-up | Visiting every logic frame does not prove a Physics2D contact was simulated while a short collider window was enabled |
| `PlayerCombat.cs:1118` shared normal-hitbox activation; `HitBoxDamage.cs:61` OnTriggerEnter2D | Authoritative damage evidence must use a real collider, actual Physics2D simulation and real damage processor. Directly calling ProcessHit cannot prove contact-window reliability |
| `HitBoxDamage.cs:754` critical minimum freeze;796 global freeze | Middle critical contacts may add pauses even where authored hitstop is zero. Crowd size must not multiply the intended activation hold |
| `CombatPlayerState.cs:50,784` state/sequence cleanup; `PlayerCombat.cs:1342` VFX cleanup | Accepted cancel can truncate the action-owned tail. Test cleanup by owner, including another pause/time owner remaining active |
| `GameImpulseController.cs:209` persistent impulse coroutine; `CameraManager.cs:1163` ultimate blend | Existing shake has no action cancellation handle and existing zoom is a blend; visual cleanup cannot be inferred from step reset alone |

See `../Reports/MainDimensionSlash/resolver-audit.md`, `follow-up-gate-audit.md`, `sprite-audit.md`, `reference-analysis.md` and the approved spec for fuller evidence. Inspect final authoring data with `CharacterObject_main.asset`, `ActionResolverTable_main.asset`, imported clip lengths and animation events rather than copied sample data.

## 3. Required Test Evidence

| Layer | Required evidence | What it cannot certify |
| --- | --- | --- |
| Focused automated rules | Threshold, owner/execution exclusions, window boundary, readiness transaction, resolver precedence, cleanup tests against final code/data | Actual physics delivery, final frame alignment, pixel quality, game feel |
| Real FSM integration | Input -> preview availability -> state transition -> final resolver -> commitment -> consumption, with action ID and execution/session recorded | Sprite quality or reliable physical contact without real targets |
| PlayMode physical integration | Live Player prefab and imported Main kit, actual enemy collider and damage pipeline, frame/time/contact trace, HP delta | Subjective polish without capture and human review |
| Captured presentation | Normal-speed and frame-stepped footage at native game view; actor/VFX/camera/contact/audio onset traces | Damage correctness unless tied to accepted damage requests |
| Human feel checkpoint | Compare burst hierarchy, pre-cut held beat, finisher weight, return to legal input, reduced shake | Automated regression coverage |

For contact tests, record action ID, execution/session, logical step/frame, activation index, target ID, accepted Primary damage request, resolved damage and scaled/unscaled timestamps. Count accepted Primary deliveries separately from DOT/additional/artifact numbers. Evidence must identify live objects/authoring data and revision. Do not claim a real-physics pass from a condition-unit test, manually advanced frame method, or direct HitBoxDamage.ProcessHit call.

## 4. Functional Matrix

P0 means a correctness/release gate; P1 means presentation/edge/regression gate. **All cases below are Not run.**

| ID | Priority | Given / when | Required result |
| --- | --- | --- | --- |
| DS-01 | P0 | Passive0 through3; perform at least five legitimate cuts, query Skill availability repeatedly and press Skill | No Dimension Slash opportunity, ready tag, selection or readiness cue. Ordinary Skill remains available under its existing rules |
| DS-02 | P0 | Passive4; commit legitimate cuts one through four inside/outside Void Area | Count increments once per accepted cut; no opportunity before fifth; no hit count, crit count, target count, Lv4 Shadow Meter multiplier or drawn slash can accelerate it |
| DS-03 | P0 | Passive4 count4; fifth legitimate cut commits | Exactly one ready tag opens with the proposed1.5scaled-second timer; count becomes0. At a legal Skill boundary, Skill selects the new SkillAction. Ordered conditional entry2 must observe entry1's increment in the same start event |
| DS-04 | P0 | Rejected cut request: no cached charge, no valid target where required, illegal state, cooldown or invalid context | No count, readiness, cached-charge loss, or follow-up resource mutation caused by rejection |
| DS-05 | P0 | One committed cut hits a crowd, crits, emits multiple Primary/additional/DOT events, whiffs or loses its target after commitment | It remains one qualifying action. Accepted paid start counts even without a hit or completed animation; preflight/rejected requests do not |
| DS-06 | P0 | Ordinary Main attack, Skill, Ultimate, wrong category/root step with similar name/ID, another character, DOT/projectile, Dimension Slash itself, QTE intro reusing cut ID, outgoing clone/off-field replay | None counts toward five or opens/refreshes the opportunity. Test actual execution context and deployed owner; exact ID alone is insufficient. Existing meter/heal/on-hit passives still run when they previously did; the opt-in filter must not globally suppress QTE callbacks |
| DS-07 | P0 | Trace scheduler notifications through preview queries, real root commitment, reset, recovery reselection and a later valid cut; exercise a nonroot step | Preview never emits a start. Each real qualifying root execution emits one start and increments once; exact-root filtering excludes other steps, and end/cancel notifications do not count. If actual scheduling emits twice, fix that observed source. No fabricated duplicate notification, new event-ID system or generic dedup feature is required |
| DS-08 | P0 | Opportunity open; perform more normal cuts before expiry; then consume or let expire | Extra cuts leave count0 and do not refresh expiry or stack readiness. After use/expiry, require five fresh accepted paid cuts; no recursive/self-rearmed opportunity |
| DS-09 | P0 | Readiness valid; poll UI/availability repeatedly, press Skill once then spam/hold Skill | Queries spend nothing. One accepted start consumes once; held/buffered input cannot repeat Dimension Slash. Next input follows base Skill availability |
| DS-10 | P0 | Before fifth cut, expired opportunity, Passive locked again or no ready tag | Normal Skill is selected when otherwise legal. A higher-priority ineligible follow-up never blocks ordinary Skill |
| DS-11 | P0 | Skill input just before expiry, exactly at expiry, just after expiry; buffer entered before expiry but committed after | Entry uses the approved authoritative timestamp and predicate; no stale preview can consume an expired opportunity. Record the boundary outcome, not only expected timer text |
| DS-12 | P0 | Follow-up rejected after preview due to changed context/resources/cooldown/target/scene; then retry while window remains | No cost/cooldown/consumption on failure. If no accepted start occurred, opportunity remains unless its timer/reset rule invalidated it |
| DS-13 | P0 | Base Skill ready/on cooldown; AE0/full; cached cuts0/positive after fifth commitment; base Skill cast before fifth cut and after readiness | Ready follow-up starts with authored cooldown0 and no extra AE/cached-cut cost. Dark Chain cooldown continues unchanged and controls its own later fallback. Failed entry consumes no ready tag. UI agrees with real start |
| DS-14 | P1 | Readiness opens during cut startup/active/recovery; press and buffer Skill at each phase | Input begins only at allowed FSM boundaries. Fifth-cut count/ready cue is not a promise of immediate cancel. No lost input outside documented buffer behavior |
| DS-15 | P0 | Passive4 ->3 while count4/ready, then unlock4 again | Locked ability becomes unavailable immediately. Whether still-live runtime readiness/count survives a live editor progression change is a content decision to record; it must never bypass the level condition |
| DS-16 | P1 | Swap Main away and back, death/revive, scene unload/reload, restart run or DeployLoadout/RefreshRuntimeState during accumulation/ready/action | Ordinary swap preserves Main's accumulation; ready time continues off-field. Another character inherits neither. Defeat and existing record-clear paths invalidate state. Test the actual scene lifecycle rather than claiming every scene transition clears state |
| DS-17 | P1 | Ground, air, coyote/support loss, moving/one-way platform, wall, ledge, opposite facing input | Both/MatchInput accepts valid ground/air entries and preserves correct output. Motion follows the approved choreography; no unrequested teleport, forced landing, wall penetration or control-mode drift |

## 5. Time, Damage Delivery and Rhythm

| ID | Priority | Procedure | Pass evidence |
| --- | --- | --- | --- |
| DS-18 | P0 | At nominal rendered30/60/120fps with existing fixed timestep unchanged, run real Collider2D targets stationary, entering/exiting windows, and near region edges | Every intended live contact is delivered once per target/window. No skipped short window from catch-up and no replay on later frames; outside targets receive no Primary damage |
| DS-19 | P0 | Keep one durable target in the approved damage region throughout a complete uncanceled cast | With proposed seven windows, exactly seven accepted Primary deliveries: burst12,20,27,33,38,42, final54. Sum resolved deliveries matches HP change after excluding separately identified secondary effects |
| DS-20 | P0 | Repeat DS-19 with sparse crowd, dense crowd, forced crit and forced noncrit, plus target death mid-burst | Per-target delivery remains correct; repeated contacts neither reuse a stale hit exclusion nor hit an already-dead target. Crowd does not multiply action count, global hitstop or one-time opportunity spending |
| DS-21 | P1 | Whiff, first/last burst confirmed, final confirmed, all crits, mixed crits; capture scaled/unscaled time | Proposed base holds2/2/6 occur only with accepted contact. Critical minimum pauses are recorded and cadence judged from real elapsed capture. Do not claim fixed1.4s wall-clock duration |
| DS-22 | P0 | Pause before threshold, during readiness, during each visual phase and during active hitstop; resume | Scaled-clock readiness does not expire while paused/hitstop if that clock is approved. Pause remains active when an action-owned freeze releases; action never restores timeScale against another owner |
| DS-23 | P1 | Force a rendering hitch spanning multiple action frames; repeat during freeze entry/exit | Required frames process once and physical delivery remains reliable. Recovery cannot leave hitbox enabled; audio/VFX/impulses do not retrigger multiple times per intended beat |
| DS-24 | P1 | Compare accelerated intervals8/7/6/5/4 with even-spacing counter-test, shake on/off and audio-only | Human can distinguish grouped barrage, compression and final cut. If smear/noise hides the sequence, increase separation or reduce accents before increasing impact magnitude |

A failure in DS-18/23 is a delivery defect, not permission to hide misses by adding duplicate damage or making every decorative line a hit. Repair the smallest approved scheduling/physics cause and retest actual simulation.

## 6. Interruption and Ownership

Run accepted cancel, attempted illegal cancel, nonlethal damage, lethal damage, legal swap, disable and scene exit at brace, burst, compression, final and recovery/tail. Use actual FSM policies: the storyboard's **cancel target66** is not a guarantee of full control or protection before66.

- **DS-25, P0:** accepted cancellation ends damage/motion immediately, stops owned pooled VFX/afterimages, releases owned anchors and restores actor animation mode. An accepted committed opportunity remains consumed; no refund/re-arm loop unless explicitly approved.
- **DS-26, P0:** illegal cancel leaves the current action valid and does not partially consume a second opportunity or reset a different cooldown. Nonlethal damage follows existing pseudo-super-armor policy; death always exits safely.
- **DS-27, P1:** camera offset/zoom, background darkening, UI fade, sound loop and delayed impulse/slow-motion tails settle/release according to approved ownership. A late coroutine cannot change camera/time after a new action or scene has taken ownership.
- **DS-28, P1:** cancel during optional tail may intentionally truncate action-owned residual VFX. No orphan effect, persistent screen flash, unintended full-screen obstruction or remaining live hitbox survives.
- **DS-29, P0:** repeat cast/reset cycles, then remove optional dependencies (missing VFX/audio/camera/target) in an isolated test fixture. Entry either rejects safely or degrades as specified; it never strands input/time/camera or corrupts inventory/run data.
- **DS-36, P0:** for the proposed scoped action-camera event/profile, hold the action clock with hitstop and pause, then cancel, switch/QTE-handoff, start another action and unload the scene. Framing progress follows its approved action-clock semantics; another owner is never overwritten by a stale sample or release. Verify left/right facing, center/room-edge/static-room bounds, normal combat camera and Ultimate/QTE camera composition. A preview curve and successful sample-unit test do not certify the real CameraManager result.
- **DS-37, P1:** inspect actual phase and end sampling at frames0/11/12/59/60/65/66/83/84/85 with the proposed finite step. Total84 and Done>84 must not leave a hitbox alive, omit the last cleanup, duplicate frame0 events or create a one-frame pose flash; active-relative contact offsets must resolve to the intended absolute frames.
- **DS-38, P1:** author/save/undo the new camera event, rescale authored FPS/key durations and scrub it in the existing action editor, then compare runtime samples. Same evaluator and relative-frame mapping agree; serialized pre-existing event enum values remain unchanged. Runtime bounds tests must inspect final corrected viewport with lens/aspect/PositionCorrection, suppress unsupported bounds/tilted/static-room translation and leave unrelated room/confiner assets unchanged.

## 7. Presentation and Pixel Verification

No editor-unit-test result can approve these cases. Capture the actual imported sprites, materials and real camera in Game view at supported resolutions/aspect ratios; inspect native output and nearest-neighbor enlarged frames.

| ID | Priority | Verification | Pass / adjustment |
| --- | --- | --- | --- |
| DS-30 | P1 | Actor-only, VFX-only and composite capture at brace, burst, compression, release and recovery | Actor matches New_sprite proportions/palette/weapon hand, feet/pivot do not jump, VFX are separate. Remove baked arcs/contamination before compositing final art |
| DS-31 | P1 | Normal-speed and stepped pixel edges with all camera motion, room edge/static room and narrow/wide aspect ratios | Logical pixel scale is coherent, edges remain intentional and actor/target stay legible. Reduce push/zoom or revise pixel registration if shimmer, stretched clusters or blur dominates |
| DS-32 | P1 | Shake off/on, crowded scene, bright/dark environment and enemy danger telegraph underneath | Final cut has the clearest largest shape; barrage tails do not hide threats continuously. Reduce overlay brightness/duration and burst shake if automatic hit flashes wash out the finish |
| DS-33 | P1 | Frame/time trace of actor release, contact activation, accepted damage, VFX leading edge, audio transient and principal camera kick | Same intended finishing beat; correct alignment before increasing shake/flash. Sound-only review distinguishes release from barrage; recording audio alone is not proof of game-audio event timing |
| DS-34 | P1 | Input opportunity opens/expires/consumes while base Skill is ready and cooling | Existing HUD shows the truthful approved affordance, time left and rejection state; cue clears on consume/expiry/reset and cannot promise an unavailable command |
| DS-35 | P1 | New observer, experienced player, rapid-input stress and exploit attempt | Observer identifies sequence and final direction; player can intentionally take/skip the follow-up; spam cannot get extra uses, skip costs or retain a stale ready window |

## 8. Regression, Performance and Run Record

Focused baseline suites to rerun **after implementation**: `ShadowMeterTests`, `CombatDamageTypeTests`, `PlayerActionInterruptTests`, relevant QTE tests and ability-UI feedback tests. Existing names establish useful coverage: ShadowMeter tests already cover capacity0-4, cache-on-dash/spend-on-Normal, no-target retention, normal-combo return, per-cut crit/Defense/heal behavior, Void Area gain, ground/target motion and radius. QTE tests exercise cut-step intro reuse, source execution and transferred hit history. Existing interrupt tests cover startup/active/recovery/cancel/lethal behavior. Add only tests for new failure modes, not checks that mirror implementation.

- Preserve current Main Passive4 +2-box capacity and Void Area gain, Passive1 cut crit/Defense scope, Passive3 cut healing, normal-cache consumption and AE/Ultimate flow unless the approved design explicitly changes them.
- Preserve ordinary Skill's debuff/buff behavior, selection, cooldown and fallback; preserve Main Ultimate's Void Area routing and QTE behavior. Manual cuts must still open outgoing QTE from valid hits, QTE intro starts must not advance this new count, and Dimension Slash is not silently added as a new QTE trigger. Read baseline kit data rather than replacing it with a convenient test-only action.
- Historical reports mention CameraGirl value/startup-hitstun fixture failures. Re-run and classify the actual current baseline; do not automatically waive a new failure or copy old pass counts.
- Profile a warmed real crowd cast and repeated casts: PC60fps target and zero gameplay per-frame GC allocation are existing project policy. Record CPU/GPU frame times, allocations, VFX pool growth and draw calls against the same scene without this action. Do not invent performance results from test count or static inspection.
- Primary PC keyboard/mouse uses the project's current Skill binding. Verify any available controller binding separately; no undocumented controller-parity claim. Networking is not applicable.

Save final results under `Docs_JJK/3_Outputs/Reports/MainDimensionSlash/`: tested revision, baseline failures, test output, actual scene/prefab and frame settings, input script, contact/time trace, representative game-view footage/stills, profiler capture and human feedback. Each case gets Pass / Fail / Blocked with evidence. Architecture/content choices still pending are Blocked by decision, not Pass.

### Bug report

`ID / severity / tested revision / scene & prefab / Passive level / progress & ready state / action ID & execution session / Skill cooldown & cost / render fps & fixed timestep / scaled-unscaled time / input sequence / expected / actual / frequency / contact trace & capture / cleanup state`.

### Exit gate

All P0 cases pass with authoritative integration evidence; P1 presentation and cleanup have reviewed captures; ordinary kit regressions are classified; performance is measured; human approves final rhythm, impact and pixel presentation. The implementation report records the executed smoke checks and remaining coverage. This broader exit gate is not yet certified.
