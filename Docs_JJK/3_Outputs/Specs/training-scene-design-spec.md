---
slug: training-scene
status: approved
source: manual
gdd_tags:
  - scene-roles
  - enemies
  - ui
  - characters
  - progression
owner: game-design-agent
human_checkpoint: approved
next_agent: architect-agent
blocked_by: []
---

# Training Scene Design Proposal

## Player Experience

Select **Training** from MainMenu to practice the character/team against the existing dummy. An in-game Training panel lets the player compare level and potential configurations. The wider Training camera shows the practice space. Hits retain the game's normal reactions, impact visuals, audio, and damage attribution. Enemy Status, Team Damage and DPS displays adapt the existing debug HUD, with collider drawings off. The dummy does not chase or attack. Return through the existing pause menu.

Confirmed user preference: **in-game Training panel**, rather than Inspector-only configuration.

Design approval: user replied **LGTM** on 2026-10-30, then clarified **Hide visuals; keep damage and DPS**. Collider/hitbox drawing is excluded from player-facing Training; normal hit detection, damage and statistics remain active. This approval covers the proposed Test_Playground destination and experience. Architecture approval remains a separate checkpoint.

Approved follow-up during implementation: user requested a **Stats** button to toggle the debug windows. The windows start hidden; damage collection continues while hidden. The button shows/hides the stats group, retaining the individual panel toggles when shown.

Follow-up on 2026-10-30: user requested TeamBuilder/Inspector access, all catalog characters and artifacts, and simultaneous editing/combat access in Training. Training now grants the UI editing permission without changing its CombatZone room state. Practice catalog availability and loadouts remain session-only. This supersedes the earlier statement excluding a catalog/roster expansion; see `training-loadout-access-task-card.md`.

Presentation correction on 2026-10-30: reuse Player_UI's existing TeamBuilder/Inspector buttons and remove the duplicate TrainingCanvas controls. Only Config stays on the left; it toggles a vertical Training Config / Reset DMG / Stats drawer. Open from a right offset and close to the right using cubic ease-out for movement and opacity. The drawer starts hidden and animates in unscaled time. See `training-tools-drawer-task-card.md`.

Approved destination: **Test_Playground**. Evidence: the inspected active Unity scene is Test_Playground, its RoomData display name is Combat Training, and the scene contains Enemy_Dummy and CinemachineCamera_Training. A separately named Training scene has not been found.

## Interaction Flow

```text
MainMenu -> Training -> Test_Playground -> practice -> Pause -> MainMenu
                                     -> Training panel -> Apply / Cancel -> practice
                                     -> temporary action camera -> Training camera
```

- Training becomes the ordinary gameplay camera for this scene, including before the first hit.
- Temporary authored action/ultimate camera effects retain their established behavior and return to Training.
- Training's base view uses the existing authored lens size; room initialization and enemy combat-presence changes must not replace it with static/exploration framing.
- The dummy takes damage and reacts normally but never starts autonomous AI.
- Entry uses the existing team-loading behavior. This slice adds no character selection/unlock system.
- Training must not clear saves, publish save headers, grant run rewards, or overwrite committed progression. Architecture must check the existing entry/exit paths against this requirement.

## In-Game Configuration

Proposed panel contents:

```text
Training Configuration
  Team Level                         [ existing valid level range ]
  Character                          [ deployed character selector ]
    Skill Potential                  [ tier ]
    Ultimate Potential               [ tier ]
    Passive Potential                [ tier ]
  [ Apply ] [ Cancel ]

Combat Statistics
  [ Enemy Status ] [ Team Damage ] [ DPS ] [ Reset Damage ]
```

- **Level follows the existing shared Run Global Level rule**, not the legacy individual Character Level panel. Label it Team Level so its scope is visible. Use the existing configured cap; do not invent a second training growth formula.
- **Potential is per selected deployed character**, with the existing Skill, Ultimate and Passive tracks. Allow increasing and decreasing tiers, including zero, within the character's defined track bounds and existing maximum. No fragment/currency costs in Training.
- Opening configuration suspends practice with scoped gameplay input/time ownership. UI navigation remains responsive. Inputs over the panel must not trigger attacks.
- Changes remain a draft until Apply. Apply commits the selected Training settings together, refreshes live stats and potential/passive effects, preserves team-wide damage history, and resumes practice. Cancel discards the draft and resumes practice. Reset DMG is the explicit sample reset action.
- Configuration must start from the session's loaded setup and remain temporary. Exiting, reloading, or interrupting Training discards overrides. Do not edit CharacterObject assets or invoke permanent potential upgrade/reset/save actions.
- A separate Reset Damage action clears the existing statistics sample without changing configuration, HP, cooldowns, or Aura Energy.
- Existing team selection and loadout behavior stays as documented in the earlier proposal; this adds configuration for deployed characters, not a new unlock or roster system.

## Adapt Debug Statistics for Training

- Reuse the existing damage attribution, DPS calculation, enemy status and per-character contribution data, including delayed/off-field damage attribution.
- Training enables statistics collection independently of HUD visibility and collider visualization. The HUD starts hidden and is opened/closed with the Stats button.
- Training starts with collider visualization **off** and does not provide a player-facing collider switch in this slice.
- Preserve real hitboxes, hurtboxes, physics and damage detection. "Without the Collider enable" is interpreted as hiding debug drawings; disabling the colliders would prevent practice hits.
- Developer debug mode retains its collider visualization capability. Hiding one stats panel must not stop data collection or activate collider drawings.
- Reuse current sample timeout and damage math; no DPS formula/timing changes are proposed.

## Current Implementation Findings

- CameraManager manages Explore, Combat, Static, and Ultimate references; it does not reference Training.
- A Training camera exists both in CameraSetUp.prefab and as a scene object. Implementation must inspect and bind the intended scene camera explicitly to avoid selecting the wrong duplicate.
- Both inspected Training camera definitions have lens OrthographicSize **10**, but GroupFraming OrthoSizeRange **6.8–8**. Merely raising priority does not resolve the conflicting framing settings.
- RoomData_Test_Playground currently has `useStaticCamera: 1`; Training selection must account for this room preference.
- EnemyController requires Rigidbody2D, not BehaviorGraphAgent.
- EnemyBehaviorBrain requires BehaviorGraphAgent, EnemyController, and EnemyLocomotion. Its `enableAgentWhenNoBossDriver` field currently permits AI on normal enemies.
- EnemyController.OnActiveLifecycleEntered calls SetBehaviorAgentActive(true). Hit/grab/traversal recovery also requests AI resumption, so the component checkbox alone is temporary.
- Enemy_Dummy is an EnemyCore variant. Its serialized overrides remove the inherited BehaviorGraphAgent, while the inherited EnemyBehaviorBrain still requires that component and is configured to permit AI.
- Test_Playground is already enabled in EditorBuildSettings. Reusing it requires no new build-scene registration.
- TrainingDebugManager.IsActive currently gates both stats/HUD and collider drawing. EnemyController.RecordHit and TrainingDebugHUD use it, as do ColliderDebugDrawer and HitBoxController. Reusing that one flag unchanged would draw colliders when the HUD is enabled.
- CharacterPotentialPanel calls permanent progression upgrade actions. PlayerMetaProgression also exposes potential mutation/reset paths, some of which save progression. Training configuration cannot blindly reuse those UI actions.
- CharacterLevelPanel explicitly declares itself legacy. The accepted Run Global Level ADR makes PlayerRunInventory the shared level authority and TeamLoader the deployed stat authority.

These are inspected facts, not completed changes. The implementation strategy remains subject to the architecture checkpoint.

## Feedback and Tuning

| Trigger | Visual | Audio | Timing |
| --- | --- | --- | --- |
| Select Training | Existing menu selection/click feedback and loading flow | Existing menu feedback where available | Existing menu behavior |
| Hit dummy | Existing hit reactions, hit VFX, and current combat HUD | Existing authored impact SFX | Existing combat timings |
| Finish temporary camera effect | Wider Training view resumes | Existing action audio | Existing authored camera return |
| Apply configuration | Selected levels/tiers and refreshed stats; accumulated damage history stays visible after closing the overlay | Reuse existing menu confirmation feedback | One application before gameplay resumes |

No new shake, particles, haptics, combat timings, or music are proposed. Training size **10** is the existing authored starting value, not a new balance recommendation. Test movement, jumps, knockback, and ultimates at the scene boundaries. Pass if the intended practice area remains readable and action cameras return to the wider view. If framing is too tight/wide, adjust the Training camera's authored lens/framing together during human playtest.

## Five-Component Evaluation

- Clarity: explicit Training label, passive target, consistent scene camera, clearly shared Team Level and per-character potential tracks.
- Response: retain normal inputs, combos, cancels, damage and reactions for useful practice.
- Satisfaction: retain visual and audio impact feedback.
- Fit: use the existing combat room, character systems and menu presentation.
- Motivation: practice itself is the purpose; no progression rewards are introduced.

## State and Edge Cases

- Menu entry: ignore repeated activation while a load is pending; clean up menu input ownership on scene change.
- Training camera: activate on room initialization; temporary action effects may override it; restore on completion/cancellation; release on scene unload.
- Dummy: spawn and hit-reaction lifecycle continue; all autonomous-AI resume requests respect its disabled policy. Disabling the brain component must not silently fall through to graph activation.
- No AI means no detection-driven camera membership. The camera must still work before and after the first hit.
- Missing camera/destination should produce a clear diagnostic without leaving a permanently locked menu.
- Leaving/reentering Training must not leak camera priorities, input locks, persistent run managers, or training damage totals into normal play.
- Dummy death/respawn and resource refill remain existing behavior; extra reset/immortality controls require a separate request.
- Cancel/close/unload must release configuration input/time ownership, including when opened while another modal owns a lock.
- Apply must invalidate old potential/passive effects when reducing tiers as well as adding new ones when increasing them. Changes must reach off-field deployed records too.
- Pending projectiles or status effects must follow established damage snapshot rules; the panel must not introduce a new damage formula or retroactively rewrite already-delivered damage.
- Training overrides must be discarded even when leaving via a non-menu path. Architecture must define isolation and cleanup before implementation, rather than relying only on a normal return button.

## GDD Alignment and Approval

`@tag:scene-roles` identifies Test_Playground as a combat/training room. `@tag:ui` documents existing training damage attribution. The title entry and passive-dummy policy are additions requested by the user; the GDD does not currently define them.

The follow-up explicitly requests level/potential configuration and adapting debug mode without collider drawings. Existing `@tag:characters`/`@tag:progression` and the accepted level-authority ADR govern the data used by these controls. Training overrides must not become a competing production progression authority.

Design and destination approved in chat on 2026-10-30. Proceed to the architecture plan required by `/implement-feature`. No gameplay code has been changed.

## Playtest Scenarios for Implementation

1. New player: find Training, enter, hit the dummy, and return to MainMenu using mouse and existing menu navigation.
2. Stress: repeatedly activate Training; attack immediately on spawn; release grabs; transition through hit recovery; leave and reenter. AI must remain disabled and one load must occur.
3. Skill: perform normal combos, launches, swaps, and ultimates. Reactions and damage attribution must remain useful for practice.
4. Abuse: enter/exit with and without an active save; compare committed progression and rewards before/after. Training must not change them.
5. Readability: move to room boundaries, jump and knock the dummy away. Verify wider framing, temporary camera return, and no static-camera takeover.
6. Regression: enter an ordinary combat room and boss room; verify existing AI, lifecycle recovery, and camera transitions.
7. Configuration: enter with a save containing nonzero level/potential values and confirm Training begins at Team Level 1 / potential 0; change settings and check effective stats/effects. Existing damage history survives Apply and team swaps; Reset DMG clears it. Cancel discards edits.
8. Statistics separation: Training HUD and damage recording work with no body/hitbox/hurtbox drawings. Switch the developer debug collider flag independently and verify physical hits work in every visualization state.
9. Isolation: enter without a save slot, then with an existing save; change configuration and leave through normal and interrupted paths. Saved levels/potentials/resources and authored assets must remain unchanged.

Smoke-test status: pending implementation; inspection was read only.
