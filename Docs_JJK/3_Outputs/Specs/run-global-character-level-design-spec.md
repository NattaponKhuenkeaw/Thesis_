---
slug: run-global-character-level
status: approved
source: manual
gdd_tags:
  - characters
  - ui
  - progression
  - scene-roles
owner: game-design-agent
human_checkpoint: required
next_agent: architect-agent
blocked_by: []
---

# Design Spec: Run-Global Character Level

## 1. Player Experience Description

The player enters a calm Level room knowing it is a roster-wide power reward, not another inventory-management screen. Approaching the background display visibly selects its button. Confirming temporarily hands presentation control to the room: normal UI hides, movement locks, the camera frames the display, and the display cycles through 1-3 before revealing the weighted reward. The reveal plays a level-up sound, increases one shared run level, refreshes every character's authored ATK/DEF/Max HP growth, then returns control and UI so the player can inspect the difference.

The prototype slice replaces this presentation with a repeatable admin button that runs the same roll-and-apply domain flow immediately. It intentionally does not pretend the final Level room, camera, UI, animation, or audio exists.

GDD alignment: `@tag:characters`, `@tag:ui`, `@tag:progression`, and `@tag:scene-roles`.

## 2. Interaction Flow

```text
Player enters the Level interaction zone
  -> background button changes from idle to selected
    -> player presses the interaction input
      -> normal UI hides and player input locks
        -> camera frames the background display
          -> display cycles between 1, 2, and 3
            -> weighted result settles
              -> level-up audio and final-number feedback play
                -> Run Global Level applies once and all stats refresh
                  -> camera/UI/input return to gameplay ownership
```

Prototype flow:

```text
Admin OnClick
  -> roll 1/2/3
    -> clamp and apply Run Global Level
      -> emit level-changed event
        -> rebuild deployed runtime stats and refresh readouts
```

## 3. System Rules

- One Run Global Level is authoritative for the complete character roster.
- The run starts at Lv.1, caps at Lv.30, and resets to Lv.1 with run reset/loss.
- The event awards +1 at 65%, +2 at 25%, or +3 at 10%; these values come directly from the project-owner request.
- The roll is resolved before state mutation and applied exactly once.
- Stat growth remains character-authored: the same level can produce different ATK/DEF/Max HP values per character.
- A character need not exist in run inventory state to resolve stats at the shared level.
- Individual EXP, EXP Potions, and the Character Level panel are legacy and have no active design authority.
- Permanent Character Potential and Artifact progression are unchanged.
- The admin prototype may be triggered repeatedly for testing; the final room controller must reject re-entry while its sequence is active.
- The final per-room claim limit is not documented yet. A completed event must never double-apply, but one-claim-per-visit versus another authored rule remains a room-implementation decision.

## 4. Feedback Loops

| Trigger | Visual | Audio | Timing |
| --- | --- | --- | --- |
| Enter interaction zone | Background button changes to selected state | Optional selection cue | Immediate response; exact animation timing pending room feel pass |
| Confirm event | UI hides; camera prioritizes display | Confirmation cue | UI/input ownership changes before randomization begins |
| Randomization | Display cycles 1-3 with readable contrast | Cycling/tension cue | Exact cadence and duration pending human feel approval |
| Final reveal | Winning number holds and level/stat readouts refresh | Dedicated level-up sound | State applies once when the reveal settles |
| Sequence ends | Camera and UI return | Short resolution tail | Input unlocks only after presentation ownership is released |

## 5. Juice Specification

### Visual Feedback

- Selection animation, number cycling, final-number emphasis, and changed-stat emphasis are required for the final room.
- The final number must remain readable without relying on color alone.
- Camera pan/zoom, easing, motion sensitivity alternative, and exact durations remain at the Level-room human game-feel checkpoint.

### Audio Feedback

- Final room requires at least a confirmation cue and a distinct level-up reveal cue.
- Cycling audio, pitch movement, mix ducking, and variants remain an audio-authoring decision.

### Physical Feedback

- Controller haptics are optional and must respect a disable/intensity setting if added.

### Prototype Feedback

- Admin flow emits a traceable log containing rolled gain, applied gain, and resulting global level.
- Runtime level/stat readouts refresh through state events; no placeholder cinematic feedback is required.

## 6. Level Sequence State Machine

### Entry Conditions

- Current room is the Level Safe Zone.
- Player is inside the interaction zone and the button is selected.
- No Level sequence is active.
- Input/UI/camera ownership can be acquired safely.

### Exit Conditions

- Result has been applied once.
- Reveal presentation has completed.
- Camera, UI, and input ownership have been restored.

### Interruptibility

- Before confirmation: leaving the interaction zone cancels selection only.
- After confirmation: normal player inputs do not cancel the sequence.
- Scene teardown or object destruction invokes cleanup that restores UI/input ownership and cannot apply the result twice.

### Chained Actions

- Idle -> Selected -> Locked/Framing -> Randomizing -> Revealed/Applying -> Restoring -> Completed.

### Resource Cost

- No item, currency, EXP, or potion cost.
- Per-room claim policy is pending the final room implementation.

### Edge Cases

| Condition | Expected Behavior |
| --- | --- |
| Input spam | Only the first valid confirmation starts the sequence; later inputs are ignored while active. |
| Lv.29 rolls +3 | Apply +1 and end at Lv.30; feedback distinguishes rolled gain from applied gain if needed. |
| Already Lv.30 | Apply nothing; final room must communicate the cap instead of showing false stat growth. |
| Character not owned in run | Its next stat resolution still uses the same Global Level. |
| Team member is deployed | Effective stats rebuild immediately after level mutation. |
| Scene unload during sequence | Cleanup restores input/UI ownership; no duplicate application. |
| Save loaded from legacy per-character data | Global level normalizes to at least Lv.1; legacy records do not become authority. |
| Low frame rate | Randomization may render fewer intermediate cycles, but weighted result and one-time application remain deterministic. |

## 7. Five-Component Evaluation

| Component | Assessment | Design Requirement |
| --- | --- | --- |
| Clarity | Strong target | Selected state, visible 1-3 pool, final hold, and resulting global level communicate cause and outcome. |
| Motivation | Strong | One interaction improves the full roster and removes inventory friction. |
| Response | Strong before confirmation | Selection reacts immediately; the non-cancelable presentation begins only after explicit confirmation. |
| Satisfaction | Pending room implementation | Final room requires visual and audio reveal channels; admin logs are sufficient only for prototype verification. |
| Fit | Strong | A safe environmental reward room matches the room-driven rogue-lite loop and Shop presentation language. |

## 8. Risks and Abuse Cases

- Unlimited completed claims would trivialize the run; final room claim policy needs human approval before scene implementation.
- A long lockout could make a routine reward frustrating; camera/randomization timings must be playtested before becoming GDD numbers.
- Per-character UI that still mutates legacy EXP would create two authorities; active entry points must be disabled while code remains for migration.
- Rebuilding stats could accidentally heal or damage the active character if HP policy is implicit.

> [!NOTE]
> **ASSUMPTION:** The prototype preserves the active character's absolute current HP while Max HP grows. **IMPACT:** Level rooms do not act as free healing. **IF WRONG:** Preserve HP percentage or add the Max HP delta instead. **VALIDATE:** Compare all three policies in the first Level-room feel test.

## 9. Playtest Scenarios

1. **New player:** Without explanation, identify that the room affects the whole roster and predict the result before control returns.
2. **Stress:** Spam confirm, leave/re-enter the zone, and attempt transition during randomization; the result applies once and input ownership recovers.
3. **Skill:** Not applicable to the weighted reward; mastery is not an intended outcome dimension.
4. **Abuse:** Attempt repeated claims, reload/transition interruption, Lv.29 +3, and Lv.30 activation.
5. **Readability:** An observer explains the rolled gain, resulting global level, and why stats changed in 8/10 trials.

## 10. Numbers and Tuning

| Value | Status | Source / Validation |
| --- | --- | --- |
| +1 / +2 / +3 | Approved | Project-owner request |
| 65% / 25% / 10% | Approved | Project-owner request; automated boundary tests |
| Lv.1 start / Lv.30 cap | Current GDD direction | Reset, clamp, and legacy-save migration tests |
| Camera/randomize/reveal timing | Pending | Human feel test during Level-room implementation; no starting value is committed in this prototype |

Tuning priority if the final room feels wrong: input ownership/recovery, outcome clarity, reveal satisfaction, camera/audio fit, then reward weights.
