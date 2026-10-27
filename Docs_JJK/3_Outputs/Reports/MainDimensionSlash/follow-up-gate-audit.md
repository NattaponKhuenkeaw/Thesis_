---
slug: main-dimension-slash
status: draft
source: manual
gdd_tags: [combat, characters, platform-input]
owner: architect-agent
human_checkpoint: required
next_agent: architect-agent
blocked_by: []
---

# Passive Lv4 / Five Dimension Cuts / Skill Follow-Up Audit

Inspected 2026-10-27 after the user's design approval and new requirement: **Passive track level4; perform five existing `main.dash.dimension_cut` actions; open a timed Skill-command opportunity for the distinct Dimension Slash.** Architecture evidence and recommendation only. No runtime scripts, action assets or saved progression changed. The architecture checkpoint is still required.

## Recommended smallest route

Author Dimension Slash as a **separate `SkillAction` in `ActionResolverTable_main.skillActions`**, with its own stable ID, root and finite sequence. It is a standalone player action; it need not use the C# `StandaloneAction` type/list. Require Passive>=4 and a unique timed ready tag. Give this action higher priority than ordinary Dark Chain while eligible, authored cooldown0, no AE cost and no ordinary-skill cooldown consumption. The spent one-use opportunity is the gate. Keep Dark Chain and the cached-cut action unchanged.

All count/threshold/window behavior fits existing Potential conditional data **except exclusion of the free QTE intro**, which reuses exactly the cut action ID. Add a narrow, opt-in action-start provenance predicate so this new rule can exclude QTE starts while existing passives preserve their current behavior. Do not broaden Skill/Ultimate subtype acceptance for this feature.

The following proposed IDs are namespaced architecture suggestions, not installed content:

- Action: `main.skill.dimension_slash`.
- Count: `main.dimension_slash.cut_count`.
- Ready tag: `main.dimension_slash.ready`.

Window length is a starting-value/content choice for the plan; it was not specified by the user. Five and Passive Lv4 are requirements, not tuning guesses.

## Why SkillAction fits existing routing

| Evidence | Implication |
| --- | --- |
| `Assets/Project/Scripts/Character/ComboNode.cs:124`: SkillAction derives CombatAction and adds cooldown. StandaloneAction derives directly from CombatAction (`:300`). | A separate SkillAction is already an independent action tree and supports the existing Skill state. |
| `Assets/Project/Scripts/Combat/ActionResolver.cs:105–137`: Skill input searches skillActions and selects highest effective priority among eligible actions. Standalone search occurs earlier (`:80`, `:425`). | Put the new independent action in skillActions; no resolver algorithm change required. Do not create a Skill-trigger StandaloneAction. |
| `Assets/Project/Scripts/Player/PlayerStateMachine.cs:705–744`: CanStartSkillAction requires SkillAction; CanStartUltimateAction requires UltimateAction. `PlayerSkillState.cs:107–150` also rejects failed SkillAction casts. | Merely setting StandaloneAction.triggerCategory=Skill can match resolution and then fail execution. Changing inheritance or loosening these checks is unnecessary here. |
| `Assets/Project/Scripts/Systems/PlayerCharacter.cs:452`: slot cooldown blocks a SkillAction only when its authored cooldown>0. `CombatPlayerState.cs:267`: starting a cooldown also requires cooldown>0. | An explicitly zero-cooldown follow-up can execute while Dark Chain cools down and starts no replacement regular-skill cooldown. Existing Dark Chain cooldown keeps running. |
| `Assets/Project/Scripts/Combat/CharacterPassiveController.cs:681`, `:2521`: active actionModifiers with a positive required timed tag are exposed as availability windows. | Add an identity-matching PassiveActionModifier in the Lv4 node with required ready tag, rather than relying only on resolver conditions. This feeds the existing Skill button window. |
| `Assets/Project/Scripts/Player/UI/AbilityUIManager.cs:627`: GetFollowUpWindowState resolves eligible action then reads its timed tag window. | Current UI window display can represent the new Skill opportunity. Verify its label/icon styling separately; no new UI owner is required for the gate. |
| `Assets/Project/Scripts/Player/PlayerStateMachine.cs:633`: active Skill window has a dedicated routing branch; `PlayerSkillState.cs:160` clears its availability tag after starting. | Reuse the current follow-up opportunity flow and consume the tag once on successful start. Do not reset Dark Chain cooldown to make the follow-up available. |

Condition data should use `PotentialLevelActionCondition(track=Passive, comparison>=, level=4)` and `RuntimeTagActionCondition(tagType=Custom, tag=ready, shouldExist=true)`. `IsActionUnlocked` defaults true (`CharacterPassiveController.cs:657`), so a future Potential's unlocked modifier alone is not a sufficient pre-unlock gate.

## Counting and window data sequence

Recommended interpretation for architecture approval: **count accepted, paid manual cut starts, once per cut execution**, regardless of hit or whiff; exclude free QTE intro reuse. A later interruption does not refund the count. This differs from counting completed cuts. The fifth accepted cut can expose Skill while the fifth cut is still executing; existing legal interruption rules decide when the player can use it.

`CombatPlayerState.ExecuteCombatStep` commits a counter cost before ExecuteStep (`:383–418`). ExecuteStep creates its CombatExecutionContext (`Assets/Project/Scripts/Combat/PlayerCombat.cs:222`) and then notifies OnActionStarted (`:299`). The current existing DimensionCut root is one step with no automatic continuation (`ActionResolverTable_main.asset:2255–2287`). Therefore this callback currently occurs once per accepted cut. It is per STEP in general, so retain a regression if that action later becomes a multi-step tree.

Add these ordered conditionalStateEffects to the existing **`main.passive.lv4`** node, preserving all its current capacity/inside-Void-Area effects:

| Order | Trigger/filter | Required state | Mutation |
| --- | --- | --- | --- |
| 1 | OnActionStarted, category Normal, exact `main.dash.dimension_cut`, exclude QTE intro | ready tag absent | Add1 to distinct cut_count |
| 2 | Same manual cut-start filter | ready absent; cut_count>=5 | Add ready tag with approved positive duration; reset cut_count0 |
| 3 | OnActionStarted, category Skill, exact new Dimension Slash ID | Ready tag normally present at resolution | Remove ready tag; existing Skill state's ClearActionAvailabilityWindow remains an idempotent safety cleanup |

A separate direct counterEffects list is not needed. Ordered conditional entries make the ready-absent guard apply to counting as well as arming, so extra cuts cannot refresh the window or bank another five while one opportunity is ready.

Existing sequence evidence:

- `Assets/Project/Scripts/Character/CharacterProgressionDefinition.cs:759`: conditional effect supports filter, requiredTags, requiredCounters, nested tag and counter mutations.
- `Assets/Project/Scripts/Combat/CharacterPassiveController.cs:1253–1257`: direct effects precede conditional effects per node. Keep these two threshold rules together in the Lv4 conditional list, not across nodes whose order would become a hidden dependency.
- `:2160–2193`: conditional entries execute in list order, each checking the current shared runtime state; tag mutations run before counter mutations within a matching entry. The second entry therefore sees the first entry's increment, and an arm both sets the tag and resets the count.
- `:2797`: Add/Reset counter mutations already exist. New count ID has no Shadow Meter gain multiplier, so the existing Lv4 2x `main.shadow` gain does not turn one cut into two counts.
- `Assets/Project/Scripts/Combat/CombatRuntimeState.cs:108`: setting an existing timed tag refreshes its expiry. The ready-absent guard is required to prevent incidental refreshes.

When the ready window expires unused, the five-count cycle has already reset; no action is cast and no ordinary Skill is consumed. New cuts begin the next cycle. While ready, extra cuts are ignored for this new count. These are recommended explicit semantics for review, not claims that the user already chose them.

## Narrow code gap: QTE provenance at action start

Main currently declares `main.dash.dimension_cut` as both QTE outgoing trigger and incoming intro reference (`Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset:1427`). `PlayerQteIntroState` executes that existing step without its manual cost (`Assets/Project/Scripts/Player/States/PlayerQteIntroState.cs:3`, `:15`, `:37`). It still calls the normal ExecuteStep path and OnActionStarted.

`CombatExecutionContext.isQteIntro` already provides the needed fact (`Assets/Project/Scripts/Combat/CombatExecutionContext.cs:7–19`), set before the callback at `PlayerCombat.cs:222`. However PassiveEventContext lacks execution/QTE provenance (`CharacterProgressionDefinition.cs:1328`), and PassiveTriggerFilter has no such predicate (`:119`). Thus exact actionId/category/stepName conditions cannot distinguish the free intro from a paid cut. Pure asset data cannot reliably meet the recommended manual-only count rule today.

Small proposed change: extend the action-start notification/context with explicit QTE-intro provenance from CurrentExecution, and add an opt-in filter such as `excludeQteIntroStarts` defaultfalse, documented as an action-start predicate. Pass it only from the execution that raised the notification; do not infer projectile provenance from whichever action happens to be current later. Existing filters/passives retain default behavior. The two new cut-count rules opt in. The exact field/API naming belongs to the architecture plan, not this audit.

Avoid globally suppressing all QTE passive callbacks: it would change healing, meter and other kit behavior outside this task. Avoid counting OnHitDealt: multiple hitboxes/enemies and misses would change the user's five-actions requirement. Avoid counting OnActionEnded: ResetCombo and cancellation both emit it (`PlayerCombat.cs:681`, `:731`), and it is not a natural-completion event.

## Lifecycle, clocks and QTE coexistence

- **Saved level:** `main.passive.lv4` is Passive track (`track:2`) level4 in current data (`CharacterObject_main.asset:1418–1425`). Its existing Shadow Domain effects must remain. Active nodes use `PlayerMetaProgression.GetPotentialLevel(character, track)` (`CharacterPassiveController.cs:1224`); no new save key is needed for the unlock.
- **Count ownership:** CharacterPassiveController binds TeamLoader's per-character CombatRuntimeState on character assignment (`:93–108`). Ordinary swapping preserves that record. Use this existing lifetime for the new count rather than a scene MonoBehaviour field.
- **No disk persistence:** count and ready tag are combat runtime state. TeamLoader clears all records on DeployLoadout (`TeamLoader.cs:751`) and RefreshRuntimeState (`:892`); defeat removes the affected character's record (`:625`). Do not promise persistence across run reset/loadout changes or every scene transition.
- **Off-field timer:** timed tags use `Time.time` (`CombatRuntimeState.cs:100`, `:126`, `:220`). Hitstop/pause pauses the window; slow motion stretches its real-time duration. Off-field time normally continues, so swapping does not freeze or refresh a ready tag. The record expires on access/tick even when not currently bound.
- **QTE count exclusion is distinct from outgoing QTE eligibility:** do not change existing manual-cut damage triggers. QteTeamSwapController accepts on-field manual primary confirmed hits and rejects QTE intro hits via execution.isQteIntro (`QteTeamSwapController.cs:91–107`). Preserve the existing trigger IDs. Do not automatically add Dimension Slash as a new QTE trigger; that is separate content scope.
- **QTE can legally interrupt a qualifying attack:** the controller's prepared-handoff gate can accept a non-auto-chain confirmed step (`QteTeamSwapController.cs:122–134`). New presentation must clean up on that handoff. Do not assume a new SkillAction's startup/active are always protected from all team transitions merely from its Skill interrupt policy.
- **Readiness consumption:** SkillAction start removes its ready tag, even if Dimension Slash then whiffs or gets legally interrupted. Repeated inputs cannot reopen the opportunity. The normal Skill command returns to Dark Chain resolution after tag consumption/expiry, subject to its existing cooldown.

## Alternatives and tradeoffs

1. **Recommended: existing SkillAction + existing conditional data + narrow QTE-start filter.** Reuses resolver, Skill state, cooldown0 exception, timed-window HUD and current per-character state. Smallest route without changing unrelated action class semantics.
2. **Literal Skill-trigger StandaloneAction support.** Would require widening FSM subtype checks and PlayerSkillState's active/preview/identity handling, then auditing cooldown/availability/UI behavior. Larger surface and unnecessary unless a literal standalone-list representation is a hard authoring requirement.
3. **Main-specific IPassiveEffectHook.** The existing hook can inspect CurrentExecution.isQteIntro and mutate existing count/tags; it avoids a shared filter field but moves otherwise authorable threshold/window behavior into character-specific code. Prefer only if no reusable QTE-start provenance field is acceptable. Never keep count solely inside a reconstructed hook instance because hooks rebuild on Potential/character changes.

## Required regression cases

- Passive3 cannot count or cast; Passive4 preserves all old effects and enables the new rule. Saved level reload behaves correctly.
- Cuts1–4 do not arm; fifth accepted paid cut arms exactly once. Failed input/insufficient cached cut/dead or missing required target do not count. Whiff after accepted start counts; damage against crowds does not multiply it.
- QTE intro using the same actionId does not count; outgoing manual cuts still prepare teammate QTEs on their qualifying hits.
- Ready opportunity beats ordinary Skill in eligible context; executes while Dark Chain is cooling down; does not restart, clear or consume Dark Chain's cooldown; cannot bypass its own ready/Potential gate.
- Existing Skill follow-up display shows remaining time. Expiry restores ordinary Skill display/resolution; pressing Skill at expiration never produces a free Dimension Slash.
- Consuming opportunity once, cancellation, death, swap, reload and repeated Skill input cannot replay it or spend cached cuts. New count ID does not modify main.shadow or main.shadow.cached_cuts.
- Additional cuts during ready neither refresh the timer nor pre-bank a second opportunity. After use/expiry, five new accepted cuts are needed under the proposed reset policy.
- Count survives ordinary swap; ready time continues off-field on scaled time. Defeat/run/loadout reset uses the existing record lifecycle.
- Returning via QTE into Main does not consume/refresh existing ready tag or advance the count; no stale old execution can mutate it after switch.
- At future edits to the cached-cut tree, verify the counter still reflects one accepted action rather than each step.
