# DevLog: 2026-07-24 - Deterministic Player Movement Refactor

## Goal

Replace competing movement/physics tuning with a small deterministic motor, preserve explicit ground/air combat ownership, and prevent falling attack spam from starting a ground action before contact.

## Implemented

- Replaced accelerated horizontal movement with direct profile-speed assignment.
- Replaced jump impulse and Rigidbody gravity with an exact launch velocity, explicit rising deceleration, state-selected falling acceleration, and one terminal speed.
- Reduced `MovementProfile` to direct movement outcomes, resources, and 60 Hz state timing.
- Replaced predictive near-ground action selection with valid collision-contact grounding.
- Made combat actions suspend base vertical movement for their full authored duration; InputGate and MotionData remain the action movement authorities.
- Preserved each executing step's captured input context during authored lift and moved deliberate ground-to-air behavior to the action's explicit output context.
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
- Initial human feel pass restored Ground Dash walking on the first Recovery frame; a later Cancel-frame experiment was rejected and this first-Recovery handoff is current again.
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
- Ground-started steps continue across platform edges; ordinary conditions stay locked while that step runs, while explicit physical-contact triggers remain observable.
- Corrected Dash Recovery authority: Air Recovery keeps horizontal locomotion locked; neutral A/D holds gravity at zero, while A/D releases normal falling without adding walk speed.
- Narrowed the movement input API from `Vector2` to scalar X. Removed unused `RouteDownInput`, `SetDownInput`, and per-state Down forwarding; W/S now reach only authored directional conditions and the explicit Down+Jump drop-through check.
- Fixed air-attack-to-air-dash animation override: Dash now suppresses falling/locomotion animation evaluation for its full FSM lifetime, and airborne restoration ignores requests while Dash owns the animation.
- Fixed intermittent combo-step animation reuse: each executed step now force-restarts its resolved Animator state, including same-state chains.
- Restricted manual normalized-time synchronization to looping Active phases and cached the exact action state/layer, preventing a pre-evaluation read of the previous Animator state from replaying the old step.
- Made non-zero-cooldown ground/air Skill variants share one logical ability-slot availability gate. The resolver now rejects a grounded variant while its airborne sibling is cooling down (and vice versa), while intentional zero-cooldown conditional variants remain available.

## 2026-07-27 Action Output Amendment and Superseded Dash Experiment

- Added `CombatActionOutputContext` (`MatchInput`, `Ground`, `Air`) beside the existing Ground/Air/Both input context and exposed both directions in the action editor.
- Captured input context per executing step. Mid-step support loss/landing no longer reclassifies ordinary conditions or invalidates the running step; explicit `Require Just Landed` conditions still observe physical contact.
- Made `ActionPhase.Cancel` an emitted runtime phase with its own interrupt categories, input gate, phase hook, and input handler.
- Committed the current action's output before natural locomotion exit, cross-category cancel, same-state Skill/Ultimate replacement, Attack cancel, Jump/Dash routing, and character-switch cleanup.
- Restricted the post-switch ground snap to committed Ground context so Team Switch cannot overwrite an authored Air output.
- Ground output now snaps to valid ground and clears landing lock before the incoming route. Air output first resolves Air and, when already supported, later replays one `Landing -> Idle/Move` transition.
- Removed the remaining event-driven landing interruption: physical contact during combat no longer resets the air combo or plays landing feedback over the action animation. Air-output reconciliation replays that landing only after action authority ends.
- Suspended base vertical locomotion during every active combat action, not only air actions or frames with non-zero MotionData Y. Authored MotionData remains the sole action-motion authority.
- Superseded experiment: Dash used a `1.18x` initial Active burst, settled over `22%`, and added a Recovery brake toward `0.08x`.
- Superseded experiment: Ground Dash movement/facing and animation stayed locked until the authored Cancel frame. Human feedback found the added tail slow and unresponsive.
- Applied three compile-compatibility casts in `HitBoxDamage`, `CharacterGridInventoryUI`, and `TeamSlotUI` so the full cached Unity assemblies could be verified.

## 2026-07-27 Dash Recovery Reversion

- Restored the prior Dash Active curve: `1.00x` through `55%` of Active, then quadratic deceleration to `0.15x` at the final Active frame. Recovery no longer owns a second brake.
- Restored Main's prior Dash data: `5/12/12` Startup/Active/Recovery frames, Cancel at frame `25`, and speed `25`.
- Restored Ground Dash movement/facing and Run/Idle animation on the first Recovery frame; the late Cancel phase remains open so control cannot relock.
- Air Dash Recovery remains horizontally locked. Neutral A/D holds vertical gravity at zero; any A/D input releases normal falling without injecting walk speed.

## 2026-07-27 Combat Step-Boundary Context Routing

- Added one Ground/Air sample at legal next-step boundaries without allowing landing/support loss to interrupt the running step.
- When context is unchanged, `ActionResolver` continues the current tree. When context changed, it resolves the same attack category against current conditions and executes the selected action's exact root step.
- Context-change roots bypass root-route shortcuts and passive starting-depth overrides. If no matching Ground/Air action exists, combat ends into the physical locomotion state.
- Re-rooting may select the same `Both` action; output capture is therefore refreshed for every executing step, not only when the action object changes.
- Final `MatchInput` completion preserves the context captured when the executing step began. Genuine edge-loss re-routing remains owned by legal next-step resolution; explicit Ground/Air output remains authoritative for completion and accepted cancels.

## 2026-07-28 Cause-Aware Edge Routing and Auto-Chain Fix

- Corrected step-boundary classification so positive-Y authored MotionData is not mistaken for falling from a platform edge. `PlayerMovement` records whether contact was lost from genuine unsupported movement, authored vertical action motion, jump, or drop-through; the resolver algorithm remains unchanged.
- Ground actions lifted by small or large authored Y displacement remain Ground-owned across step boundaries. Genuine horizontal support loss still re-resolves the matching Air action root.
- Fixed `autoChainToNext` when an authored Cancel window begins on the first Recovery frame. Combat states now attempt structural auto-chain before consuming Cancel-phase player input.
- Goenitz's follow-up Ultimate authoring was valid: `Exit.defaultNext` points to `Recover`. `Exit` used `recoveryFrames = 1` and `cancelWindowStartFrame = 234`, so frame 234 resolved as Cancel and the Recovery-only auto-chain hook never ran. The Cancel-boundary auto-chain fix restores `Exit -> Recover` without editing Goenitz's asset.
- Fixed physically airborne `MatchInput`/Ground-output Jump cancels after authored upward MotionData or spent air-jump resources. Jump pre-validation recognizes the pending snap/reset; the correction refreshes resources without publishing synthetic `JustLanded`; and the accepted Jump launches in the same transition with no Landing/Falling or second startup hold. Ordinary jumps keep their authored startup.
- Fixed Ground-captured `Both`/`MatchInput` actions whose MotionData travels Ground -> Air -> Ground. Physical re-contact remains observable to action conditions, but while the combat state owns the frame it cannot emit landing recovery/animation; Ground output also clears same-tick `JustLanded` before handing control to Idle/Move or the accepted next action.
- Restored jump-start commitment weight without changing authored timing values: ground and double-jump startup now buffer direction while holding horizontal motor output at zero, then restore response on the deterministic launch frame. The special physically airborne Ground-output Jump cancel remains immediate.
- Fixed Rugal's remaining `rugal.skill` landing visual. Its FSM already exited directly to Grounded, but `TriggerGrounded` allowed a queued Grounded transition to leave the current `Landed` clip playing; locomotion now forces Grounded immediately once it owns animation.
- Fixed the underlying Rugal `rugal.skill` Fall/Land session. The first failure came from trusting a cached pre-MotionData grounded sample instead of locating the actual floor. A second execution-order race remained: when the state machine snapped after contacts were generated, `PlayerMovement.FixedUpdate` could run later in the same step and overwrite Ground with the old airborne contact set. Ground output now stops MotionData, ray-resolves and silently snaps to the floor, clears landing lag, and owns the authored Ground result for the remainder of that fixed step; normal edge/fall detection resumes on the next step.
- Made Team Switch consistently Ground-only and independent of the outgoing Rigidbody pose. A Ground-context action remains eligible while authored MotionData temporarily lifts the body; after a successful switch, the incoming character always uses the authoritative silent floor snap and routes directly to Idle/Move. Genuine Air-context actions cannot switch, and no Fall/Landing session is inserted.
- Removed the remaining real Team Switch landing session. Team Switch runs from `Update`, so its silent snap occurred between physics simulations; the old same-`fixedTime` guard expired before the next `FixedUpdate`, which then consumed pre-snap airborne contacts and created genuine Fall/Landing recovery. An out-of-physics Ground correction now owns the upcoming fixed-step contact sample, after which normal physics contacts resume.

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

Dash uses restored project values rather than new starting knobs. Perform ten keyboard and ten controller ground/air dashes using neutral, forward, and opposite A/D. Pass when the prior Active curve is preserved, Ground Recovery restores movement/animation immediately without a late relock, neutral Air Recovery holds gravity, and A/D releases normal falling without horizontal walk injection.

## Human Checkpoint

Architecture is approved. Playtest feel, keyboard/controller E2E, and PR/merge approval remain open.
