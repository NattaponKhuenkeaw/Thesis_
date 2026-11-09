---
slug: camera-girl-memory-revision
status: needs-human
source: manual
gdd_tags: [combat, characters, progression]
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---
# Camera Girl authored kit revision

Workflow: /implement-feature. Direct user authorization supplies design numbers and permits necessary reusable system improvements. Preserve existing action inputs, attack timings, audiovisual assets and unrelated Main work. No runtime-generated configuration or character-specific serialized settings block.

Implement the supplied base skill and six levels in each track through existing potential nodes, recipes, coordinated action executor, damage processor and saved projectile/action assets. Base Capture randomly grants one 17 CR / 33 CD teammate-only buff for 10s; skill ranks deterministically upgrade the supplied scopes/values, self DEF, stun, Len Focus and DEF-scaled additional/splash damage. Ultimate ranks retain off-field assist with one active clone, move 3 AE to rank2, duration override30s at rank3, capped DEF-derived ally ATK at rank4, 100%/650% DEF at rank5 and self ADI at rank6. Passive Captured lasts10s, DEF-40 at rank1 and DEF/RES-40 plus DOT/Additional incoming+40 at rank2; existing marked-target hits gate Mastery at rank3; rank4 adds5 AE and Additional delivery; rank5 source cooldown/70%DEF proc shares1.5s cooldown; rank6 self ATK/DEF stacks3% for3s, max10 stacks.

Confirmed: Len Focus starting values are 1 target/sec, round-robin across living scene enemies and a team-wide ally dash/SPD aura; Ultimate3 overrides to 30s total; incoming DOT/Additional vulnerability is +40%. Base Shadow Attack retains existing damage until rank5. Mastery reuses the existing normal combo step that plays cg_attack6, including both hitboxes, motion, audio, events and its owning action's damage formula. No provisional Capture damage formula is substituted. Captured splash starts with the shared explosion definition's 3-unit radius; this reach remains a playtest tuning value.

Architecture: reuse RuntimeEffect inventory lifetime/source ownership; extend generic recipe filters for action sets and enemy effect identity, generic additional-damage splash, stat-contribution cap, reaction applications, movement multiplier and periodic target selection. Use existing per-character cooldown keys and source-stat authority for off-field reactions. Author assets only in editor. Mastery is a saved SkillAction gated by10s tag. No new player FSM states or input mapping.

Implementation also fixes shared limits and evaluation: six potential ranks are reachable; independently authored recipes sharing one formula no longer supersede each other; target-owned damage-input modifiers participate in unified mitigation; the highest eligible coordinated-action policy owns its probability roll. An explicit coordinated damage override is the total step budget distributed across its hit weights, so Lv5 delivers 650% DEF without the old 0.65 share reducing it again. The explicit 3 AE assist reward replaces ordinary AE rolls and requires confirmed damage. Len Focus's aura depends on its source summon and the shared recipe presentation follows the active ally while source ownership stays with Camera Girl.

Experience: Clarity uses existing buff/mark visuals plus a small lens indicator; Response preserves existing cancel/input paths and reuses attack6; Satisfaction/Fit preserve source action feedback. Starting Len Focus cadence must be checked against room readability; adjust cadence if attacks overlap unreadably. Cast refreshes rather than stacks. Follow-ups consume their window; buffs expire on their own timers. Verify swaps, empty/dead targets, mark expiry, nonrecursive additional damage, 900ATK cap, ten-stack cap, AE once per assist and lower-rank preservation. Human playtest owns feel/visual readability after implementation.

Validation: `Tools/Project/Combat/Validate Camera Girl Memory Revision` reports 62 passed, 0 failed, 0 skipped in `Temp/camera-girl-memory-tests.xml`. Unity compiled with no console errors. This includes shared damage-type and Main authored-effect/swap/HUD regressions. Native PlayMode was not invoked to preserve the user's unsaved Test_Playground scene. Implementation is ready for the human feel/visual check; no merge or publication is requested by this task.

Len Focus follow-up under `/fix-bug`. The saved strike referenced the correct Ultimate clips but skipped its spawn animation and returned to the pool immediately on hit. The strike now plays the full spawn clip, spends one hit during that phase and holds the existing final frame for 0.1 seconds before expiry. The isolated Unity run passed 64 tests, including the real strike animator and hit path.
