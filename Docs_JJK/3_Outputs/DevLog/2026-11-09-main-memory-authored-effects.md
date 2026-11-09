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

# Main Memory authored effects

Applied Apricha's requested Main upgrade changes through saved projectile assets and RuntimeEffect recipes. Removed Main's runtime-generated projectile variants, PlayerCombat-owned summon coroutine, action-name finisher lookup and custom PotentialNode configuration section. Existing potential replacements now select the saved projectile variants.

Normal hitboxes support optional additive stat terms, evaluated in the existing damage path against the correct current source stats. Empty terms preserve action-budget behavior. RuntimeEffects support periodic pooled projectile spawning, authored finisher-tag filters and once-per-execution room pulses. Inventory effect clearing invokes expiry callbacks for field cleanup. Existing resource effects can reduce cooldowns by a fraction of their current remaining time.

Updated Skill stun/summon behavior, moved the Ultimate HP buff to Ultimate Lv5, and added Passive Lv6 Judgment room damage and resource/cooldown completion effects. Preserved Main's unchanged meter, action, normal attack, source-stat and presentation systems, including the Dark Chain debuff marker.

Verification: Unity compilation succeeds. Broad regression: 161 passed, four unchanged dash/target expectation failures; nine new direct checks pass. Native summon/inventory/deployed-stat lifecycle: one passed, zero failed. Action data differs only by three authored finisher tags. Exact commands, results and remaining human feel checks are in the task card and test plan. No build/dependency/CI settings, merge or publication changes.

## User clarification: split ATK and Max HP hits

Apricha clarified that the Skill Lv4 chain and Ultimate Lv4/Lv6 ATK + Max HP formulas must resolve as two damage events. Reauthored each primary hit as ATK-only Normal damage and routed the own-Max-HP portion through the reusable additional-damage RuntimeEffect as separate Darkness damage. The base Void hit retains its prior Void type. The existing direct damage test and test plan now distinguish the two events. No tests were run after this correction.


## User clarification: following field icon

Replaced the persistent chain-field projectile with a procedural pixel-chain icon in deep purple, violet and lavender. The saved prefab uses the shared RuntimeEffect presentation pool with subtle bobbing beside the active player. Added a generic recipe presentation opt-in for off-field owners so switching does not transfer or duplicate the combat effect. Removed the presentation-only projectile parameter and asset; strike projectiles retain their combat behavior.


Field icon validation (2026-11-09): Unity compilation completed without C# errors. `Temp/main-recipe-field-icon-tests.xml`: 17 passed, 0 failed (following icon movement, switch ownership, off-field expiry, six shared visual lifecycle checks, authored summon asset and direct damage/cooldown regressions). Inspected `Temp/main-dark-chain-icon-preview.png` rendered beside Main. The initial broad artifact selection also encountered 16 failures in legacy artifact setup/recipe lookup; those unrelated fixtures were not changed. The native summon Play mode run was cancelled at Unity's unsaved-scene prompt to preserve Test_Playground, so the earlier native-run evidence does not validate this icon correction.


## Fix: HUD refresh cleared the icon after switching

Reproduced the disappearing icon by rebuilding PlayerHUD after switching to an ally: the shared recipe view entered Ending even though Main's inventory effect was still ticking. Removed PlayerHUD's active-character-only SyncVisuals call. The team-aware receiver is now the only gameplay owner of world effect presentation. Added HUD integration coverage and changed the summon regression to use SetCharacter rather than directly replacing characterObject; an isolated EditMode case verifies one shared timer without spawning projectiles.

Validation of the switch fix: the HUD regression failed before the fix with the icon in Ending after an incoming-character HUD rebuild. After removing the duplicate HUD SyncVisuals call, `Temp/main-recipe-field-icon-tests.xml` reports 18 passed, 0 failed. This includes real SetCharacter/inventory timer handling in isolated EditMode with no target projectiles, repeated HUD rebuilds, off-field expiry and existing damage/visual checks. Unity compiled without C# errors. Native PlayMode projectile verification was not rerun in this correction.

## Live targeting and simultaneous summon icons

Confirmed that the saved Dark Chain recipe uses the active player's position to find the nearest target at each strike tick. The strike still lands at the target's ground projection and resolves damage from Main's stats. The shared receiver now assigns distinct vertical slots to floating icons and reserves each slot through its fade. This keeps Dark Chain and Len Focus visible together as the active character changes.

The focused EditMode suite passed 19 cases. It verifies the live origin after movement and swapping, sprite separation through reorder and fade, and the existing swap and expiry lifecycle. The native PlayMode target selection check remains unrun.
