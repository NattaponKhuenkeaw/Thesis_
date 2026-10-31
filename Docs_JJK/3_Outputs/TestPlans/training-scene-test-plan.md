---
slug: training-scene
status: draft
source: manual
gdd_tags:
  - scene-roles
  - characters
  - progression
  - ui
  - enemies
owner: qa-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Training Scene Test Plan

## Automated Coverage

Run **Tools > Project > Training > Run Focused Tests** in Unity Edit mode. The runner includes an actual enter-Play-mode flow; results and captures are written to `Docs_JJK/3_Outputs/Reports/TrainingScene/`.

| Scenario | Expected |
| --- | --- |
| MainMenu Training button | Authored scene button, intro group and navigation links; loads Test_Playground |
| Independent practice level | Increase/decrease within the practice cap; the saved inventory remains unchanged throughout |
| Independent practice potentials | Fresh per-character tracks, caller data copied, reductions invalidate unlock caches; saved progression is never used |
| Training baseline isolation | Session owns separate TrainingInventoryState and TrainingProgressionState objects at level 1 and potential 0; saved assets are unchanged before, during and after practice |
| Session cleanup | Scene exit destroys practice state; no saved-state restoration or ownership tokens are involved |
| Configuration Apply/Cancel | Apply refreshes live stats and preserves damage history; Cancel discards draft; input/time released |
| Stats separation | Stats recording active with collider drawings off; HUD starts hidden; Stats toggles windows without clearing the sample |
| Configuration overlay | Opening configuration preserves the selected Stats visibility; the configuration Canvas (6100) renders above the visible Stats Canvas (5500) and Config controls (6000) |
| Independent Training windows | Open Stats and Config, then Training Configuration, TeamBuilder, Inspector and Pause. Stats remains drawn and Config remains open; only their explicit buttons change visibility. Config can be closed/reopened while another window holds the gameplay input lock. |
| Team-wide damage across swaps | Deal damage, swap active characters/team members, then deal more; prior sample and each character's cumulative row remain; Reset DMG clears them explicitly |
| Dummy recovery | Opted-out or disabled brain cannot resume; ordinary permitted brain can resume |
| Real damage pipeline | Dummy accepts resolved damage; health and attributed DPS sample update |
| Camera selection/return | Training camera is active before hits and after room initialization; `useStaticCamera` is off for the Training room; static camera never takes priority; Training framing resumes after ultimate zoom |
| Saved progression | Slot meta, boss checkpoints, headers, occupied flags and active slot unchanged |
| Interrupted scene load | Practice objects and Training-created persistent managers are destroyed only at scene teardown; startup and opening/closing UI preserve managers |
| Training loadout access | TeamBuilder is editable, Inspector opens for undeployed characters, actual room mode remains CombatZone and combat HUD remains visible |
| Existing Player_UI navigation | Actual authored Builder/CharacterInspector callbacks open the existing panels; no duplicate loadout buttons remain under TrainingCanvas |
| Config drawer motion | Starts closed; three vertical actions; opens from left, closes right with ease-out at zero timescale; rapid reversals settle correctly; hidden panel does not intercept clicks |
| Practice defaults | Each Training entry starts fresh at level 1 and potential 0, exposes the full character/artifact catalog, and deploys Main (`char-0-main`) as the only team member; saved team/equipment/progression do not seed practice |
| Existing regression tests | Shared level, enemy lifecycle, combat registration and camera animation remain functional |

## Human Playtest

1. Launch MainMenu; use mouse and keyboard/controller navigation to enter Training. Check the button is visible and settings/quit still work.
2. Move/jump to both room edges, hit/launch/grab the dummy and perform swaps/skills/ultimates. Check camera framing and return, hit reactions, damage attribution, and no collider drawings.
3. Open Training Configuration. Adjust Team Level and each deployed character's Skill/Ultimate/Passive tiers up and down. Compare active/off-field behavior after Apply. Close with Cancel, Escape, or controller cancel and confirm practice resumes.
4. Use the left Config button to reveal Training Config, Reset DMG and Stats. Verify slide-in/slide-right ease-out transitions and rapid toggling. Click Stats to show/hide the windows; recording continues while hidden. Click/drag visible stats panels. UI interactions must not trigger attacks, hidden drawer content must not intercept combat clicks, and the configuration modal must remain unobstructed.
5. Reset Damage; then leave via Pause -> MainMenu and reenter. Verify the previous practice overrides/sample are gone and normal saves still load.
6. Enter an ordinary combat room and boss room. Check AI, spawn/hit recovery and existing camera behavior.

Primary platform: Windows PC, keyboard/mouse. Automated tests cover API/event flow; hands-on combo feel, controller ergonomics, boundary framing and other resolutions still require human playtest.
