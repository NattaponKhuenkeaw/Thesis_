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

## Edge Cases

- [ ] Deploying an intentionally empty Artifact loadout does not restore stale
  active-character Artifacts.
- [ ] First scene startup may backfill an active character only when TeamLoader
  has never captured an Artifact snapshot for that slot.
- [ ] Rapid equip/remove does not leave text or portrait scale/color stuck.
