---
slug: main-shadow-meter
status: draft
gdd_tags: [combat, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Current architecture and authoring guide

This revision replaces hold routing with the user's press-Dash / cached-Normal loop. It extends the existing counter, passive event and Normal combo paths; runtime code has no main-character identity checks.

| System | Current responsibility |
|---|---|
| Base Passive counter effects | Normal/Skill/Ultimate hit gains; OnAfterDash Transfer spends 20 meter and adds one cached cut |
| CombatRuntimeState | Validates and transfers between named counters without spending an unaffordable amount |
| CombatAction | Authored cached-counter cost and optional enemy requirement; funded actions are gated even without an availability provider |
| CombatPlayerState | Spends counter cost once per action; substeps do not pay repeatedly |
| PlayerAttackState | Authored reselectOnAttackInput allows cached cuts to repeat on LMB in Recovery/Cancel and return to normal combos |
| PlayerInputHandler/FSM | Original immediate press dash; no hold timer or extra dash-attack state |
| MotionKeyframe | Horizontal Only, Minimum Travel Distance, overshoot, optional fixed enemy anchor and arrival facing |
| Player_UI / PlayerHUD | Serialized centered meter in the prefab layout, plus fallback row creation for other HUDs |

## Tune the kit

Open Main's ActionResolver table -> standalone **Dimension Cut** (`main.dash.dimension_cut`). It now uses **Normal** as trigger category, **Both** input contexts, **MatchInput** output, `main.shadow.cached_cuts` cost **1**, `commitsDash=false`, and **Reselect On Attack Input=true**. The stable action ID is retained for existing authoring references.

The Motion track's **DashThroughTarget** exposes **Horizontal Only=true** and **Minimum Travel Distance=3**. Start/end frames, easing, overshoot, target radius, anchor offset, enemy hold and arrival facing remain editable. Target Y never affects player displacement in horizontal mode. Minimum travel extends the endpoint; it does not teleport the player backward before the cut. Collision can shorten travel.

CharacterObject -> Kit -> Base Passive -> Counter Effects includes an **OnAfterDash / Transfer** effect: source `main.shadow`, cost 20, destination `main.shadow.cached_cuts`, reward 1. It fires from an actual dash commitment, never a raw button press. Gain rules for ordinary Normal actions exclude the Dimension Cut so spending charges cannot regenerate them for free.

CharacterObject -> Kit -> Combat Meter configures the meter counter and cached counter. Potential Meter Capacity Modifiers affect unspent meter boxes. Runtime state already belongs to each character via TeamLoader, preserving the existing switch/reset lifecycle.

### Potential authoring revision

Capacity is now 2/3/3/3/5 across base and Passive Lv1-4. Potential action modifiers expose Critical Rate Operation/Value and Ignore Defense Requires Critical Hit. The shared damage processor applies the rate before rolling crit and the Defense override after that roll, scoped to matching primary action hits. It clones stats only when necessary and preserves resistance/type reduction. Deployed sources use the same authored fields.

Passive Lv3 uses the existing resource effect with Trigger Damage Dealt and ratio 0.08. Passive Lv4 uses Counter Gain Modifiers: main.shadow, multiplier 2, requiring the main.void_area.shadow_gain runtime effect. The three field assets refresh this marker on their existing occupant checks; no Main-specific runtime branch is introduced. Positive Add gains are multiplied; costs, Set/Reset and transfers are unchanged.

Skill Lv4's vulnerability and the base vulnerability share an explicit stack key so the upgraded recipe replaces the base value. Its separate Allies recipe grants +15 CRIT RATE for 7 seconds on successful Skill hits. Ultimate progression remains unchanged.

`Player_UI.prefab` -> PlayerHP_UI -> **RuntimeStatusPanel / ShadowMeter / PlayerPanel**. The middle row participates in VerticalLayoutGroup; CombatMeterUI is centered inside it. PlayerHUD binds the serialized component instead of creating a duplicate above the HUD. The original saved container names are preserved.

The Dash clip, slash timing/damage and impact presentation remain placeholders for the user's final ActionResolver authoring. Live target choice, close-range overshoot and feel still need PlayMode testing.

The meter has no numeric CUT label. Box size/spacing are serialized (starting values 72x16, gap 6), and the prefab row is 28 units high. Dimension Cut captures the nearest target from a radial query, faces it before motion regardless of held direction, and keeps that target ahead of ordinary camera-facing choices. Align Held Target To Dash Height projects a held target onto the horizontal cut lane; the player remains at the same Y.

## Target Radius authoring

For Dimension Cut, edit Target Radius on the DashThroughTarget entry motion. Action Setup displays and edits the same range. The earliest targeted entry keyframe is authoritative, including when keyframes are stored out of timeline order. Availability and initial target capture call GetTargetSearchRadius; movement uses the same keyframe. The serialized action radius remains a fallback for actions without entry target motion. SnapToEnemy now also uses its authored keyframe radius instead of a fixed 8.
