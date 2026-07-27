# DevLog: 2026-07-24 - Deterministic Player Movement Refactor

## Goal

Replace competing movement/physics tuning with a small deterministic motor, preserve explicit ground/air combat ownership, and prevent falling attack spam from starting a ground action before contact.

## Implemented

- Replaced accelerated horizontal movement with direct profile-speed assignment.
- Replaced jump impulse and Rigidbody gravity with an exact launch velocity, explicit rising deceleration, state-selected falling acceleration, and one terminal speed.
- Reduced `MovementProfile` to direct movement outcomes, resources, and 60 Hz state timing.
- Replaced predictive near-ground action selection with valid collision-contact grounding.
- Made combat actions suspend base vertical movement for their full authored duration; InputGate and MotionData remain the action movement authorities.
- Preserved an action's captured input context during authored lift and moved deliberate ground-to-air behavior to the action's explicit output context.
- Routed Falling-state attacks through air action selection until actual landing.
- Captured `Both` action ownership from the cast context and enforced Ground/Air context for standalone actions.
- Initial pass made real landing interrupt an active air action; the 2026-07-27 amendment below supersedes this with deferred output reconciliation.
- Converted attack MotionData X/Y displacement to deterministic per-frame velocity instead of a force impulse.
- Removed the per-phase airborne gravity policy, grounding authority overrides, obsolete serialized movement fields, and the obsolete dash gravity-reset API. `GroundC` remains as the single authored feet/snap reference.
- Migrated the Fast, Sample, and Slow movement profile assets.
- Superseded corrective pass: direct fixed `fallSpeed` removed the acceleration conflict but felt linear and lunar in human E2E.
- Corrective E2E pass: JumpStartup remains contact-grounded and changes to airborne exactly when launch velocity is assigned.
- Corrective E2E pass: dash uses one FSM frame clock, restores the original deterministic burst curve, rejects dash self-cancels, clears the initiating buffer, and starts its full cooldown on state exit.
- Human feel pass: jump-arc descent now accelerates aggressively while edge/drop descent begins at zero and accelerates gently; both approach one terminal speed.
- Human feel pass: the fall session is latched at jump launch or support loss and survives air attacks/dashes so the correct descent resumes afterward.
- Human feel pass: dash now holds full speed through early active frames and decelerates only near the end instead of using either constant velocity or immediate quadratic decay.
- Initial human feel pass restored Ground Dash walking on the first Recovery frame; the 2026-07-27 amendment below supersedes that handoff with the authored Cancel frame.
- Reference-video pass: edge loss and drop-through enter Falling in the same fixed step; coyote frames grant jump permission without preserving a ground state.
- Reference-video pass: one-way landing requires downward top-surface crossing and snaps the authored `GroundC` position to the platform top before publishing Ground/Landing.
- Reference-video pass: drop-through ignores only the current supporting platform, begins neutral fall with no jump impulse, and permits a lower platform to catch the player.
- Falling and Jump/Air locomotion states own air-attack routing; Landing buffers attack until its snapped ground context is committed.
- Authored attack MotionData is the sole horizontal motion owner while active instead of adding free locomotion speed on top.
- Removed the runtime-created `MovementProfile` fallback. A missing assignment now reports an explicit error and safely disables movement; Kula was migrated to the existing Sample profile.
- Removed `PlayerSpeedFallbackFeedback`; it was an auto-attached placeholder streak generator rather than movement logic or authored VFX.
- Consolidated `PlayerJumpStartupState`, `PlayerJumpRisingState`, and `PlayerAirborneState` into one `PlayerJumpState`; retained the old enum slots only for serialized-data compatibility.
- Made double jump restart the active jump session after clearing vertical and fall momentum, so the second launch has full deterministic authority.
- Added one FSM-owned Grounded/Airborne environment context updated only by jump launch, support loss, and landing; input/action resolution no longer infers this context from contact independently.
- Expanded `PlayerFallingState` to resolve airborne Skill and Ultimate conditions as well as normal attacks.
- Ground-started actions continue across platform edges; the 2026-07-27 amendment locks their ordinary conditions to the captured input context while preserving explicit physical-contact triggers.
- Corrected Dash Recovery authority: vertical gravity resumes immediately, but held walk input cannot raise horizontal speed after the authored dash deceleration and extend dash distance.
- Narrowed the movement input API from `Vector2` to scalar X. Removed unused `RouteDownInput`, `SetDownInput`, and per-state Down forwarding; W/S now reach only authored directional conditions and the explicit Down+Jump drop-through check.
- Fixed air-attack-to-air-dash animation override: Dash now suppresses falling/locomotion animation evaluation for its full FSM lifetime, and airborne restoration ignores requests while Dash owns the animation.
- Fixed intermittent combo-step animation reuse: each executed step now force-restarts its resolved Animator state, including same-state chains.
- Restricted manual normalized-time synchronization to looping Active phases and cached the exact action state/layer, preventing a pre-evaluation read of the previous Animator state from replaying the old step.
- Made non-zero-cooldown ground/air Skill variants share one logical ability-slot availability gate. The resolver now rejects a grounded variant while its airborne sibling is cooling down (and vice versa), while intentional zero-cooldown conditional variants remain available.

## 2026-07-27 Action Output and Dash Feel Amendment

- Added `CombatActionOutputContext` (`MatchInput`, `Ground`, `Air`) beside the existing Ground/Air/Both input context and exposed both directions in the action editor.
- Captured action input context once at selection. Mid-action support loss/landing no longer reclassifies ordinary conditions or invalidates resolver routes; explicit `Require Just Landed` conditions still observe physical contact.
- Made `ActionPhase.Cancel` an emitted runtime phase with its own interrupt categories, input gate, phase hook, and input handler.
- Committed the current action's output before natural locomotion exit, cross-category cancel, same-state Skill/Ultimate replacement, Attack cancel, Jump/Dash routing, and character-switch cleanup.
- Restricted the post-switch ground snap to committed Ground context so Team Switch cannot overwrite an authored Air output.
- Ground output now snaps to valid ground and clears landing lock before the incoming route. Air output first resolves Air and, when already supported, later replays one `Landing -> Idle/Move` transition.
- Removed the remaining event-driven landing interruption: physical contact during combat no longer resets the air combo or plays landing feedback over the action animation. Air-output reconciliation replays that landing only after action authority ends.
- Suspended base vertical locomotion during every active combat action, not only air actions or frames with non-zero MotionData Y. Authored MotionData remains the sole action-motion authority.
- Reworked Dash feel with starting values of `1.18x` initial Active speed, ease-out settlement to `1.00x` over the first `22%` of Active, and a smooth Recovery brake to `0.08x` immediately before Cancel.
- Kept Ground Dash movement/facing and dash animation authoritative through Recovery. Normal locomotion input and Run/Idle animation now return together on the authored Cancel frame.
- Applied three compile-compatibility casts in `HitBoxDamage`, `CharacterGridInventoryUI`, and `TeamSlotUI` so the full cached Unity assemblies could be verified.

## Verification

- Unity's cached Assembly-CSharp and Assembly-CSharp-Editor Roslyn responses compiled the changed source set with zero errors.
- Project-wide reference searches found no remaining references to removed movement profile fields, grounding authority, or airborne gravity policy.
- `git diff --check` reported no whitespace errors.
- No automated/script test was added or run, per the user's explicit instruction.
- Gameplay acceptance is intentionally assigned to the human E2E plan:
  `Docs_JJK/3_Outputs/TestPlans/deterministic-player-movement-human-e2e.md`.

## Starting Values

- Fast: move 10.5, jump 20, rise deceleration 55
- Sample: move 10.5, jump 24, rise deceleration 65
- Slow: move 6.5, jump 20, rise deceleration 55
- All profiles: jump-fall acceleration 95, neutral-fall acceleration 40, terminal fall speed 28
- Default resources/timing: two jumps, one air dash, seven coyote frames, three jump-startup frames, two landing-recovery frames
- Sample retains four jump-startup frames and zero landing-recovery frames from the latest human tuning pass.

These values are starting points for the human feel pass, not final balance approval.

Dash curve values are also starting points. Perform ten keyboard and ten controller dashes using neutral, forward, and opposite held input. The pass condition is a readable initial burst, continuous locked Recovery braking, and control/locomotion animation returning on the exact Cancel frame without a second surge or hitch. Adjust peak by `0.04`, brake end by `0.03`, or settle progress by `0.05` in the direction indicated by the observed failure.

## Human Checkpoint

Architecture is approved. Playtest feel, keyboard/controller E2E, and PR/merge approval remain open.
