---
slug: goenitz-blood-frenzy-kit
status: needs-human
source: manual
gdd_tags:
  - combat
  - characters
owner: architect-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Goenitz Kit Investigation — 2026-11-03

- Routed the requested behavior-changing kit rework to `/implement-feature`; created its task card and design draft.
- Inspected Goenitz character/resolver/projectile/animation assets, AE authority and spend paths, hold input, shared recipes, deployed projectile source handling, damage delivery, native/custom Bleed, evasion and HUD state.
- Recorded user clarifications: off-field drain, shared duration, ordinary AE suppression during Frenzy, +6-AE-only extension, retained Yamidokoku 70 AE gate/cost, next-resolved-hit Frenzy memory, +5% ATK/CR/CD kill stacks, one-shot Bleed amounts plus native DOT, and Physical migration.
- Found material risks: persistent required tags treated as consumable action windows; missing hold Skill path; no existing AE sustain rule; no source passive hit callback after swap; render-frame projectile cadence; actual values diverging from descriptions/GDD; missing limit-break animation clip; shared evasion needs more than a stat recipe.
- Completed a concrete design with labeled defaults, current-code evidence and boundary/regression/playtest scenarios.
- Gameplay files and configuration unchanged. No runtime/Unity tests executed because this session stops at the required design checkpoint.
- Documentation structure/whitespace checks passed. Concurrent editor-side asset changes were preserved; whole-workspace diff checking reports Unity-serialized trailing spaces in that separate controller diff.
- Design approved by user LGTM with explicit amendments; the original design checkpoint is complete.

## Approved Revisions and Architecture Follow-up

- Shin/evasion availability lasts exactly with Frenzy. Kill ATK stacks have no timer and survive Frenzy exit.
- Ultimate Lv.4 now grants +3% ATK only, cap 10, for Goenitz kills from any category while Frenzy applies. Only Goenitz's kills extend by 6 AE; allies' kills do not.
- Ultimate Lv.2 vulnerability now comes from confirmed hits by any Frenzy actor, self or allies.
- Ultimate Lv.3 restores independent Blizzard on Goenitz Ultimate/limit-break use: one refreshed stack, 15 seconds, existing package plus +10 ADI.
- Updated the approved design/task card and produced a concrete architecture plan plus proposed ADR-020; no gameplay edits made.
- Found that native DOT from Additional Bleed needs an explicit proc-policy flag and that multi-step casts need shared cast identity distinct from per-step context.
- Next: required architecture approval of the plan and ADR before implementation. No repeat design approval required.

## Implementation and follow-up on 2026-11-04

- Implemented the AE-funded runtime and authored Goenitz’s updated actions and recipes. The drain accumulator uses double precision after a 144 FPS check exposed a late tick.
- EX tornadoes track along the horizontal launch plane. Attack2-2 follows Attack1 while Frenzy is active, with the separate post-dash Attack4 entry preserved.
- The cooldown panel shows the most recently used action with time remaining. Blood Frenzy shares a slow white pulse between the AE gauge and Goenitz’s sprite.
- Broader editor checks reported two kit-information failures and three Goenitz dash-recovery failures. These suites remain open for baseline comparison.
