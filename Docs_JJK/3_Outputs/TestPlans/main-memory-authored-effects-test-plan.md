---
slug: main-memory-authored-effects
status: needs-human
source: manual
gdd_tags: [characters, combat]
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Main Memory regression and playtest

Automated evidence is recorded in `../Specs/main-memory-authored-effects-task-card.md`. Run `Tools/Project/Combat/Validate Main Authored Memory Effects` for direct damage, authored ranks, meter/NA, magnetism, reactions and damage-type checks. Run `Tools/Project/Combat/Validate Main Summon Lifecycle` for the native Play-mode inventory/swap/cleanup check. The latter uses the Unity Test Runner and temporarily enters Play mode.

| Scenario | Expected |
| --- | --- |
| Skill rank 0/1/2/3 | No new base vulnerability; ranks 1/2 apply 20%, rank 3 replaces with 40%, all for 10s. Rank 2 retains weak pull; rank 3 uses stronger pull and 0.5s stun. |
| Skill rank 4, 100 ATK / 1000 HP | Every 2.5s for 15s, Dark Chain chooses the target closest to the active player's current position. Its strike lands at the target's ground projection and deals a 200% ATK Normal hit, then a separate 8% own-Max-HP Additional Darkness hit (80 at 1000 HP). Move between two enemies and switch away from Main to check the next target changes with the active player's position. Main remains the damage source. Recast to refresh the existing summon. |
| Skill ranks 5/6 | At 1200 own HP every ally gains 10 ADI/DM/DI for 10s. Rank 6 base Dark Chain cooldown is 10s. |
| Ultimate ranks 1/4/6 | At 100 ATK / 1000 HP, rank 1 retains a 200% ATK Void hit. Rank 4 produces a 500% ATK Normal hit, then a separate 6% own-Max-HP Additional Darkness hit (60); rank 6 produces a 1000% ATK Normal hit, then a separate 12% own-Max-HP Additional Darkness hit (120). All tick every 5s. Check two DamageResolved events for upgraded HP-scaling ticks: Primary/Normal, then Additional/Darkness. Lv2 vulnerability is 15% for 7s without stacking; Lv3 awards 5 AE per damaging tick even against several enemies. |
| Ultimate rank 5 | Void lasts 40s. Both Ultimates grant 40% Max HP for 12s and refresh the same buff; Passive rank 6 alone does not grant it. |
| Passive ranks 1–4 | Preserve normal stages 4–6, capacities 3/4/5 boxes, Dimension Cut +15 CRIT and critical DEF bypass, Execution 8% own-HP heal and existing large meter gain, five paid-cut 5s Judgment follow-up and doubled gains inside Void. |
| Passive rank 5 | Judgment action budget is 850%. Earlier Jailbreak/Execution hits do not proc the HP bonus; their authored final hits add Darkness equal to 20% of Main's current Max HP through RuntimeEffect. |
| Passive rank 6 | Judgment's last hit emits one room pulse for 14% own Max HP, including distant enemies and without multiplying the pulse by the number of primary targets. Completion grants 20 AE and halves every running Skill/Ultimate timer; ready actions stay ready without credit toward their next cast. |

Readability/feel review: verify chain start/loop visuals and existing debuff marker, stun clarity and elevated target grounding. Stress casts, switches, interruption, owner death, room transitions, invulnerable targets and repeated applications. Observe final-hit feedback and resource/cooldown UI; check that delayed effects never use the switched-in ally's ATK or HP. All numeric targets above are user-supplied; stronger pull retains the previous doubled Lv2 speed rather than introducing a new balance value.

Known unrelated failures remain in four dash/target expectations listed in the task card. Do not change dash routing or widen this revision to silence them.


## Following field icon regression

- Activate Dark Chain and Rei's Len Focus together. Both icons should stay visible in separate slots beside the active character. Swap in both directions and let one icon fade. The other should keep its slot.

- Dark Chain activation shows one small purple chain icon beside the player, with gentle bobbing. No persistent chain projectile is spawned as the indicator.
- Switching to an ally keeps the icon beside the active player; moving and facing changes keep it upright and nearby. The effect and remaining duration still belong to Main.
- Expiry/removal while Main is off field ends the icon; no lingering visual or copied ally effect remains.
- Shared buff/debuff presentation retains its existing recipient behavior unless a recipe explicitly opts into following the active character.


Switch/HUD regression: cast Dark Chain, switch to an ally, and rebuild the actual HUD repeatedly (including status changes). The following icon must remain active. Main retains one summon instance with its original remaining time; the ally must receive no copied summon instance. Switching back and recasting refreshes the source instance. Check expiry while Main is off field. The isolated scheduler regression tests timer advancement with no targets; the PlayMode wrapper additionally checks projectile spawning and damage.

