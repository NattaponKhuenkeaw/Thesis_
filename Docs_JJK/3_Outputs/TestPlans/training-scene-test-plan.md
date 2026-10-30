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
| Temporary shared level | Increase/decrease within existing cap; underlying stored level is unchanged |
| Potential overrides | Per-character tracks, caller data copied, reductions invalidate unlock caches |
| Override ownership | Other owners cannot update or remove the session's values |
| Session cleanup | Restore raw meta state, including data excluded from the save schema |
| Configuration Apply/Cancel | Apply refreshes live stats and clears sample; Cancel discards draft; input/time released |
| Stats separation | Stats recording active with collider drawings off; HUD starts hidden; Stats toggles windows without clearing the sample |
| Dummy recovery | Opted-out or disabled brain cannot resume; ordinary permitted brain can resume |
| Real damage pipeline | Dummy accepts resolved damage; health and attributed DPS sample update |
| Camera selection/return | Training camera live before hits, authored size 10, resumes after ultimate zoom |
| Saved progression | Slot meta, boss checkpoints, headers, occupied flags and active slot unchanged |
| Interrupted scene load | Overrides removed and Training-created persistent managers destroyed |
| Existing regression tests | Shared level, enemy lifecycle, combat registration and camera animation remain functional |

## Human Playtest

1. Launch MainMenu; use mouse and keyboard/controller navigation to enter Training. Check the button is visible and settings/quit still work.
2. Move/jump to both room edges, hit/launch/grab the dummy and perform swaps/skills/ultimates. Check camera framing and return, hit reactions, damage attribution, and no collider drawings.
3. Open Training Configuration. Adjust Team Level and each deployed character's Skill/Ultimate/Passive tiers up and down. Compare active/off-field behavior after Apply. Close with Cancel, Escape, or controller cancel and confirm practice resumes.
4. Click Stats to open/close the window group; damage must continue recording while hidden. Click/drag visible stats panels and their toolbar. These UI interactions must not trigger attacks. Check readability and verify the configuration modal is unobstructed.
5. Reset Damage; then leave via Pause -> MainMenu and reenter. Verify the previous practice overrides/sample are gone and normal saves still load.
6. Enter an ordinary combat room and boss room. Check AI, spawn/hit recovery and existing camera behavior.

Primary platform: Windows PC, keyboard/mouse. Automated tests cover API/event flow; hands-on combo feel, controller ergonomics, boundary framing and other resolutions still require human playtest.
