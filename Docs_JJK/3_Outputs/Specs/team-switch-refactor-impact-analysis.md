# Refactor Impact Analysis: Team Switch & State UI Reaction

## Goal
Simplify the Team Switch, State, and UI synchronization pipelines by removing over-engineering (redundant Update polling, complex text scaling coroutines, duplicate dictionaries/cleanups, and non-core responsibilities).

## Class Responsibility Table (Before -> After)

| Class | Before Responsibility | After Responsibility |
|---|---|---|
| `TeamLoader` | Manages party array, equipped artifacts, runtime stats, active HP/AE dictionaries, switch cooldown, binds player health, calculates ultimate cost with multiple overloads, spawns background executors. | Manages party array, equipped artifacts, active HP/AE dictionaries, and switch cooldown. Delegates background executor spawning. Streamlined dictionary cleaning. |
| `TeamSwitchHUD` | Rebuilds and syncs active slots. Polls string-based team signature and active index every frame in `Update`. | Rebuilds and syncs active slots entirely via event subscription (`OnTeamChanged` and `OnCurrentCharacterChanged`). `Update` only handles cooldown visuals. |
| `TeamSlotUI` | Displays character slots in TeamBuilder. Manages drag/drop, right-click swaps, wiggles locks, pulses padlock, and runs 3 concurrent coroutines to slide and tick ATK/DEF/HP stat texts. | Displays character slots in TeamBuilder. Instantly updates ATK/DEF/HP stat labels without complex ticking loops. Shakes and wiggles locks via the `UIFeedbackDriver` directly. |
| `PlayerCharacter` | Serves as player facade. Initializes stats/movement/animation, handles damage/healing, manages hurtbox and speed debug drawer injection in `Awake`. | Serves as player facade. Cached component and stats synchronization. Cleaned up redundant `ApplyCharacterData` overloads. |

## Impact & Risk Assessment

1. **High Risk: Swapping HP/AE Persistence**
   - *Risk*: Swapped-out characters losing their current HP or AE state.
   - *Mitigation*: Ensure `TeamLoader`'s event bindings `HandlePlayerHealthChanged` and `HandlePlayerCharacterChanged` remain fully intact and correctly update the persistent dictionaries.

2. **Medium Risk: Background Ultimate Simulation**
   - *Risk*: Switching during an Ultimate causes errors if `BackgroundUltimateExecutor` isn't initialized correctly.
   - *Mitigation*: The static factory `BackgroundUltimateExecutor.Spawn` will be exact in replicating the configuration logic previously in `TeamLoader.cs`.

3. **Low Risk: UI Lock / Feedback Shakes**
   - *Risk*: Lock overlay or wiggle effects missing in Combat mode.
   - *Mitigation*: We preserve `UIFeedbackDriver` calls which are already present and functional on slots.
