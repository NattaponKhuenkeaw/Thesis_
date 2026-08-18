# Boss Death Flow — Setup Notes

## Runtime wiring (automatic)

1. `BossPatternController` on death → `BossDeathSequence.BeginFromBoss`
2. `RoomWaveManager` boss clear → prepares offers, skips portals/choice UI
3. Left-click prompt → `SceneTransitionManager.RequestTeamHealOnNextTransition()` + `TransitionToRoom`
   - Uses `Mouse.current.leftButton.wasPressedThisFrame` (Input System), not `UnityEngine.Input`
4. During transition load → `TeamLoader.HealAndReviveEntireTeam()`

No scene object is required for `BossDeathSequence` / prompt UI (auto-created).

## Optional Inspector tuning

Place an empty `BossDeathSequence` under your persistent root (or leave auto-create) and tune:

| Field | Suggested start | Notes |
|-------|-----------------|-------|
| hitStopFrames | 8–12 | Real-time freeze via `GameFreezeManager` |
| slowMoTimeScale | 0.2–0.35 | After hit-stop restores |
| slowMoDuration | 0.4–0.7s | Unscaled wait |
| orbBurstDuration | 0.25–0.35s | Pop-out before homing |
| orbHomingDuration | 0.6–0.9s | Lerp to player (`AnimationCurve` EaseInOut) |
| energyOrbPrefab | optional | Leave empty for fallback yellow orb |

### Energy orb prefab (optional)

1. Create empty GO → add `BossEnergyOrb` + SpriteRenderer (glow / fire sprite)
2. Assign to `BossDeathSequence.energyOrbPrefab`
3. Homing uses **unscaled** time so it still moves during slow residual frames

### Boss room WaveManager

On boss layouts set:

- `useBossDeathFlow = true`
- `unlockSceneExitOnClear = false`

(`RoomLayout_Boss_Executioner` and the layout factory are updated.)

Even without the flag, spawning a `BossPatternController` / `isEliteOrBoss` enemy auto-enters boss clear path.

## Heal timing

Do **not** call team heal in the boss arena. Heal is queued on click and applied after the next room mounts inside `SceneTransitionManager`.
