---
slug: action-window-icons
status: approved
gdd_tags: [ui, combat, platform-input]
owner: Codex
human_checkpoint: manual game-feel review
next_agent: human
blocked_by: []
---

# Ability icon and dash feedback regression

Source: approved icon design/architecture and the dash-feedback-drift task card.
GDD: @tag:ui HUD cooldown/readiness/follow-up display; @tag:combat action rules.

## Automated checks

Run Unity menu `Tools/Project/UI/Run Ability Icon Tests`.
Results and prefab previews: `Reports/ActionWindowIcons/`.

- Existing AbilityUIFeedbackTests cover dash cooldown readback, one ready rush,
  quiet rebinding, zone visibility and prefab wiring.
- ActionWindowIconTests cover all three combat input categories, timed replacement,
  expiry/consumption, resolver priority, counter gating without spending, missing
  art after character swap, Ground/Air mismatch, cooldown and AE dimming, NA prefab
  links, Main's assigned art and Ultimate-type Judgment Cut on the Skill slot.
- UIFeedbackWiggleTests retrigger feedback at simulated moving offsets and verify
  one tracked origin, disable restoration, separate targets and later layout moves.
  These are EditMode lifecycle checks, not an end-to-end input replay.

## Manual PC smoke test

1. Open Test_Playground or Training with Main. Confirm NA, Dark Chain and Jailbreak
   Punisher pictures; Dash shows CharacterObject_main.DashData.icon and retains its
   movement-owned cooldown.
2. Repeatedly press Dash during its cooldown and while otherwise blocked. Stop,
   wait for feedback to finish, and confirm every slot returns to its original position.
3. With Main's relevant passive unlocked, open the Judgment Cut opportunity.
   Confirm its picture appears on Skill and its window cue drains with gameplay.
   Use it and then repeat while allowing expiry; both restore Dark Chain.
4. Prepare Dimension Cut charges and a valid target. Confirm the NA replacement
   picture follows the resolver's actual target/counter requirements.
5. Check a low-AE Ultimate and a cooling Skill remain dim and show their current
   resource/cooldown cues. Opening the HUD must not grant or spend resources.
6. Try Ground/Air conditions. Confirm no replacement appears without an active
   timed window or when the action fails current context conditions. Swap characters
   while a window is open, hide/reopen the HUD, and return through SafeZone/CombatZone.
   No stale pictures or ready bounce.
7. Clear an Action Icon in the Animation Editor, save, and confirm fallback artwork
   after a replacement/swap; restore the authored icon after this manual test.
8. Assign CharacterMetaData.icon to a deployed character. Confirm that sprite appears
   in its team swap slot, and an unassigned character uses its metadata portrait.
9. Clear Main's DashData.icon and confirm the Dash UI restores its original prefab
   sprite. Reassign it and swap characters to confirm the displayed Dash icon follows
   the active character.

Pass: displayed opportunity matches the input's actual resolver selection, no
resource/window changes originate in the UI, and spam produces no resting-position drift.
Manual input feel and readability sign-off remain with the requester.
