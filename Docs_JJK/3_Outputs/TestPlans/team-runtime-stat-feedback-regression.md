---
slug: "team-runtime-stat-feedback"
status: draft
gdd_tags:
  - "progression"
owner: "qa-agent"
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Regression Plan: Team Runtime Stat Feedback

## Automated

- [x] Deploying an Artifact stores the slot reference and applies its HP/CDR.
- [x] Runtime refresh clamps current HP after Max HP decreases.
- [x] Runtime refresh reapplies the current Character Potential rank.
- [x] A CDR-only equip is classified as an upgrade.
- [x] Removing the same CDR bonus is classified as a downgrade.
- [x] Frame-based UI regression observes green on CDR-only equip and red on
  removal.
- [x] Added an asset-backed regression covering Shermie Skill Potential Lv.2
  and Ultimate Lv.0 self-stat isolation.
- [ ] Run
  `TeamRuntimeStatFeedbackTests.ShermieSelfStatBuffs_DoNotModifyTeammateStats`
  in the currently open Unity Editor (batch execution is blocked while that
  Editor owns the project).

## Play Mode

- [ ] Equip Starweave Ring: TeamSlot portrait/card gives a green pulse even
  though ATK, DEF, and HP do not change.
- [ ] Remove Starweave Ring: TeamSlot portrait/card gives a red pulse.
- [ ] Equip Fiend-Flesh Mask: ATK pulses green and DEF pulses red.
- [ ] Move an Artifact between two characters: both source and destination
  TeamSlots update and pulse.
- [ ] Deploy, close Team UI, and reopen it: equipped Artifacts remain assigned.
- [ ] Close Team UI after a Max HP reduction: current HP is clamped to Max HP.
- [ ] Close Team UI with +50% CDR equipped: runtime skill cooldown calculations
  and HUD continue to use the refreshed CDR value.
- [ ] With Shermie Skill Potential Lv.2, use Grab Dash and switch to a
  teammate: the ally receives the shield but keeps their original DEF.
- [ ] With Shermie's ally shield active, use Ultimate Lv.0 and switch to a
  teammate: only Shermie receives +50% Max HP.
- [ ] In both cases, verify Shermie still receives her own authored +50% stat
  increase.

## Edge Cases

- [ ] Deploying an intentionally empty Artifact loadout does not restore stale
  active-character Artifacts.
- [ ] First scene startup may backfill an active character only when TeamLoader
  has never captured an Artifact snapshot for that slot.
- [ ] Rapid equip/remove does not leave text or portrait scale/color stuck.
- [ ] Nested source-stat queries cannot leak a self-only multiplier into the
  teammate whose stats are currently being calculated.
