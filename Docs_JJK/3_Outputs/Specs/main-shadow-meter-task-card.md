---
slug: main-shadow-meter
status: draft
source: manual
gdd_tags: [combat, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main: Shadow Meter and cached Dimension Cuts

Source: Apricha's 2026-10-27 request and two follow-ups: successful hits only, followed by horizontal movement, minimum travel, prefab HUD placement and press-Dash-to-cache / Normal-Attack-to-spend. This revision replaces the initial hold-Dash design. GDD references: `@tag:combat`, `@tag:characters`. The exact kit is specified by the user's request, not previously documented in the GDD.

Workflow for this correction: `/fix-bug`. Preserve the user's unrelated local combat, prefab, asset and animation edits. The user owns final animation/motion and feel approval. No dependencies, build settings, CI or publishing work.

## Current rules

- Successful ordinary attack hits build Shadow Meter; whiffs and incoming damage do not.
- Each meter box is 20 points. Capacity is 2 at base and 3/3/3/5 at Passive Lv1/2/3/4. Attack4-6's Lv2 gate remains.
- Dash is immediate on press. Each **committed** normal dash converts one affordable box into one cached cut. A rejected/buffered-but-uncommitted dash spends nothing. No enemy is required to cache.
- Dash keeps its existing movement, cooldown and air quota. Multiple valid dashes can cache multiple cuts.
- Normal Attack/LMB selects Dimension Cut when a cached charge and live target are available. Commitment spends one cached charge. Additional LMB inputs in Recovery/Cancel spend subsequent charges, then resume ordinary combos when the cache is empty.
- A cached cut does not consume another dash cooldown or air quota. Without a target, ordinary NA remains available and cached charges remain stored. An interrupted/whiffed committed cut keeps its cost.
- Dimension Cut moves on X only, preserves player Y and uses Match Input output context. It does not launch a grounded player. Ordinary falling from lost support remains physical movement behavior.
- Close targets still produce a configurable minimum X travel. Walls may shorten actual travel. Enemy anchoring and reaction immunity continue to use existing systems.
- `Player_UI.prefab` contains a ShadowMeter row between the existing RuntimeStatusPanel and PlayerPanel sections (the status/stat containers from the user's request). No cached-count text label is shown.
- Cache uses the existing per-character runtime counter lifecycle. No extra expiration or cache cap is imposed; meter storage still obeys its box cap.

## Editable starting values and test targets

User values: 20 points/box, 2 base boxes, maximum 5 meter boxes. Other values remain editable starting values: normal hit +2, Attack3 +4 total, Attack6 +6 total, Skill +8, Ultimate +10. Dimension Cut does not refill itself. Crowd hits can fill faster through existing per-hit proc policy.

Starting minimum cut travel: **3 world units**; overshoot 1.2; target radius 8; placeholder multiplier 5 and existing Dash clip. Test close targets left/right/at the same X: cut travel must be at least 3 units unless blocked, Y must remain unchanged, and the endpoint must be past the target. If it feels excessive in tight spaces, reduce minimum travel; if it still looks cramped, increase it. Author final clip/timing/VFX/audio in ActionResolver.

Human checkpoint: final animation/motion, live combat and game-feel approval after implementation review.

## Latest presentation and targeting correction

The user requested removal of the cached-cut label and a larger meter. Editable UI starting values are 72 by 16 canvas units per box with 6 units between boxes, in a 28-unit layout row. Visually check two- and five-box layouts for overlap with status icons and stats; reduce width/spacing if they collide. Auto targeting queries nearby targets in every direction, independent of movement input. Captured targets own cut facing instead of the general camera lock-on. Horizontal cuts can align a held target above/below onto the current dash height without changing player Y, using the authored Align Held Target To Dash Height option. Enemy reaction immunity remains respected.

## Radius regression correction — 2026-10-27

Root cause: action availability and commitment used CombatAction.targetRadius (8) while the target-motion keyframe had an independent editable radius. Increasing only the motion radius could not select the action for distant enemies. Targeted entry motion now supplies the same search radius to availability, target capture and movement; actions without targeted entry motion retain their authored action fallback. Animation Editor action/motion radius controls are synchronized. No balance or asset radius value was forced to 1000.

## Potential revision — 2026-10-27

The Skill-track rules in this historical revision are superseded by `main-skill-potential-task-card.md` (2026-11-02). Passive-track rules below remain applicable.

Source: Apricha's follow-up in this chat. Skill Lv1-3 and the Ultimate track keep their current behavior. Skill Lv4 replaces magnetism with 40% increased enemy damage taken (replacing the base 15%, retaining its 16-second duration). Successful Skill hits also grant all allies, including Main, +15 percentage points of CRIT RATE for 7 seconds; further hits refresh without stacking.

- Passive Lv1: capacity +1; Dimension Cut gains +15 percentage points of CRIT RATE. Only its critical hits ignore Defense; resistance and elemental reductions still apply.
- Passive Lv2: unlock NA4-6.
- Passive Lv3: Dimension Cut heals Main for 8% of actual damage dealt. No additional meter box is awarded here.
- Passive Lv4: capacity +2, totaling 5 boxes, and 2x positive Shadow Meter gains while inside Void Area (multiplier explicitly chosen by Apricha). Dash conversion costs and cached charges are not multiplied.

The old Void Area speed, ally DOT Mastery and enemy slow applications are replaced in every Main field variant by a short refreshed occupancy effect. It gates the authored counter gain multiplier and expires after leaving the field (0.3-second duration, 0.25-second refresh). Crit and Defense behavior use generic action modifiers matched by the stable Dimension Cut action id; healing uses the existing triggered-damage resource effect.

## Buffered action facing correction — 2026-10-27

User report: with Face Target On Arrival disabled on DashThroughTarget, buffered NA, Skill and Ultimate retain travel facing after passing the enemy. Workflow: `/fix-bug`. Each manually committed step must receive its own initial auto-target turn, even if the next step has the same combo depth and no idle movement tick occurs. The arrival-facing option still controls the cut's endpoint; automatic chains retain facing and held horizontal input keeps its existing targeting override.

Root cause: PlayerMovement inferred step starts from attacking/idle samples and combo-depth changes. Buffered replacements at the same depth retained the previous step's completed or cancelled initial-lock flag. PlayerCombat now explicitly calls BeginCombatStepFacing after committing the new step metadata and before animation/payload processing.

## Multi-enemy dash anchoring — 2026-11-05

The dash still selects one enemy within Target Radius to determine travel. With Hold Target At Anchor enabled, each eligible enemy damaged by the cut's melee hitbox is held at an anchor calculated from its position at contact. The authored offset and dash-height alignment apply separately to each hit. Guard and reaction immunity still apply. The selected enemy receives a hold only if the hitbox connects.

## Damage-Taken Debuff Visual Follow-up — 2026-11-01

Source: Apricha's follow-up in chat. Reuse the approved downward-chevron `FieldPowerDown` visual for Main's `Damage Taken Increase` Skill recipe at both base strength and Potential Lv.4. This is presentation-only: preserve the 16-second duration, refresh behavior, and existing damage-taken values. The saved recipe references are in `CharacterObject_main.asset`; manual combat readability and expiry review remain pending.
