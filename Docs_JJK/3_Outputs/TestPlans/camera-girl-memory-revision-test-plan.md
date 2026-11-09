# Camera Girl regression and playtest

Run `Tools/Project/Combat/Validate Camera Girl Memory Revision` in Edit mode. It creates an additive isolated test scene, restores the active scene, and writes `Temp/camera-girl-memory-tests.xml`. It does not save Test_Playground or invoke the native PlayMode runner.

Latest validation: **62 passed, 0 failed, 0 skipped**. Unity compiled successfully and the console returned zero errors after the run. This includes native HitBoxDamage requests from the spawned ShadowActionExecutor: base damage113.75 with50ATK and the existing3.5x/0.65 formula, upgraded damage650 with100DEF. Native PlayMode and human combat feel review remain unrun.

Automated coverage includes base random buff selection, rank replacement and recipient scopes; all eighteen nodes and six-rank reachability; summon refresh, whole-scene round-robin selection, dependency cleanup and dash expiry; DEF-derived ATK cap; assist probability ownership, source damage and explicit AE policy; Captured mitigation and delivery vulnerability; preexisting-mark follow-up gating; off-field source cooldown/procs; ten-stack cap/expiry; Mastery's cloned Attack6 hit data and Additional conversion; splash near/far targets; shared damage-type behavior; and Main's authored effects plus SetCharacter/HUD/icon lifecycle regressions.

| Manual scenario | Expected result |
| --- | --- |
| Base Capture, then Skill1/2/3 | Base grants exactly one buff to teammates except self. Rank1 grants both 17 CR/33 CD; rank2 replaces values with 31/64; rank3 includes self and grants self DEF+40% for15s. Crit buffs last10s. |
| Skill4 repeated Capture hits | 40% stun chance,2.5s duration; existing attack timing remains intact. |
| Skill5 cast, recast and switch | One lens icon follows the active ally; one source timer lasts10s and refreshes without stacking. Every second the next living scene enemy takes100% Camera Girl DEF. All allies gain SPD+1 and half dash cooldown. No target/dead target does not stall later selections. Expiry/removal ends the aura. |
| Skill6 on unmarked/marked targets | Skill and Skill EX hits generate350% ownDEF Additional damage. A Captured target additionally triggers1000% ownDEF splash, including the target and nearby enemies; distant enemies are excluded. Check the starting3-unit radius visually. Additional hits do not recursively generate procs. |
| Ultimate1/2/3/5 with Camera Girl off field | Assist lasts15s at1/2 and30s at3+. Ally attacks roll50%, then100% at5; max one active clone and existing internal cooldown remain. Rank2+ grants at most3AE once per damaging clone. Rank5 primary damage budget is650% Camera Girl DEF; earlier ranks retain original damage. |
| Ultimate4/6 | Ally ATK increases by5x ownDEF, capped at900,20s. Ultimate6 grants self ADI+100%20s. |
| Passive1/2 | Attack3/Capture applies Captured10s. DEF-40%; rank2 also RES-40% and incoming DOT/Additional+40%. Reapplication refreshes one mark. |
| Passive3/4 | First marking hit does not open follow-up. Attack3/Capture hitting an already Captured target opens10s Skill follow-up. Mastery uses cg_attack6 timings, both hitboxes and events; consumption closes the window. Rank4 converts its damage to Additional and grants5AE on completion. |
| Passive5/6 | Own marked-target hits reduce running Skill cooldown by0.5s and deal70%DEF Additional with shared1.5s cooldown. Ready skills gain no future cooldown credit. Rank6 grants3%ATK/DEF per hit, at most30%, expires3s after refresh. |

Human checks still required: in-game lens/mark readability, Attack6 follow-up feel, stun feedback, splash reach, source death/room transition cleanup and resource UI during actual combat. Native PlayMode was not invoked because the user's scene has unsaved changes. Existing Main split ATK/HP damage and purple chain icon behavior must remain unchanged.

## Rei Memory routing regression

The updated EditMode run passed all 63 checks. It reads all 18 authored descriptions through the shared map and Hard reward lookup, loads Rei's saved Memory ranks, and verifies that her kit reads Memory ranks in combat. Old fragment values remain in the profile as historical data.

In PlayMode, open Previous Upgrades for a legacy character, then switch to Rei in the inspector and Training. Rei should land on Memory with the legacy toggle hidden. Preview an unearned Skill node and a generic node. The first should show the authored Capture effect and the second should show its actual stat bonus. Earn or apply a Memory step and confirm its combat effect through the normal action input.

## Len Focus strike animation regression

With Skill Memory 5, summon Len Focus near a target. Each strike should open with the existing `cg_ult-projectile` animation at the target and continue animating after its one DEF-scaled hit. The final frame should hold briefly before release. Switch characters during the summon and check the next strike. Len Focus still selects one target per second, and each strike spends one hit from its own budget.

The updated EditMode run passed all 64 cases. The strike regression exercises the real projectile animator, verifies one 100% DEF hit and a sprite change after contact, then checks the final loop and timed release.
