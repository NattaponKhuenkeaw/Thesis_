---
slug: goenitz-blood-frenzy-kit
status: approved
source: manual
gdd_tags:
  - combat
  - characters
owner: architect-agent
human_checkpoint: not-required
next_agent: implementation-agent
blocked_by: []
---

# ADR-020: Source-Scoped Character Kit Runtime

| Field | Value |
|---|---|
| Status | Accepted; implementation in progress |
| Date | 2026-11-03 |
| Author | Codex, architect role |
| GDD | `@tag:combat`, `@tag:characters` |

## Context

Goenitz's approved kit uses existing AE as mode duration while off-field, shares mode benefits, blocks ordinary AE gains, and rewards kills regardless of action category. The active CharacterPassiveController changes character on swap; per-step execution context also changes across multi-step Ultimates. Implementing those rules solely in that controller would misattribute source effects and lose pending mode eligibility.

Frenzy-linked abilities/evasion, the independent 15-second Blizzard buff, and permanent-until-hit ATK stacks have deliberately different lifetimes. Shared assets cannot own this mutable state.

## Proposed Decision

- Add an opt-in runtime-definition reference on character kit data, a small TeamLoader-owned runtime host, and one plain C# runtime per configured source character. Goenitz's custom runtime handles its authored trigger policy without replacing the existing Potential engine.
- Keep AE in the current run-character record and mutate it through TeamLoader. Add typed change reasons so ordinary positive gains can be blocked during a mode while negative spends/drains and explicit kill extensions remain valid.
- Carry a transient accepted-cast token across steps/projectiles/background execution, separate from the existing per-step/field-session context. Attach one-use remembered Frenzy eligibility to that token.
- Observe confirmed positive primary damage before ordinary AE generation, and consume source-aware normalized damage events for live-mode kill accounting. Preserve provenance and deduplicate by enemy life identity.
- Reuse target-owned Runtime Effect Recipes: source-linked Frenzy package, separately timed single-stack Blizzard, and a Forever/remove-on-damage ATK-stack recipe capped at 10. Mode cleanup removes only source-linked mode effects.
- Keep actor damage identity distinct from buff-source identity. Additional Bleed uses the attacking actor's current ATK, the shared damage processor and an explicit native-DOT proc flag. Evasion becomes a focused recipe contribution rather than a new general buildable stat.
- Extend hold Skill and persistent-tag consumption authoring with backward-compatible defaults; opt Goenitz projectiles into seconds-based cadence without globally reauthoring the roster.

## Alternatives Considered

| Alternative | Benefit | Reason not selected |
|---|---|---|
| Add all behavior to active CharacterPassiveController | Few initial types | Loses source callbacks after swap; mixes off-field team lifecycle into an active actor component. |
| Use Artifact passive runtime to execute the character kit | Existing normalized event handling | Gives character upgrades Artifact instance identity/ownership, contrary to ADR-010 boundaries. |
| Duplicate AE into a tag timer/counter or recipe duration | Easy countdown display | Two quantities drift after costs/extensions and violate AE-as-duration semantics. |
| Put Blizzard and kill stacks under the Frenzy container | Simple apply/remove | Incorrectly removes independent/persistent buffs when Frenzy ends. |
| New framework for all character passives | Broad future flexibility | Unnecessary migration risk; this change is an opt-in runtime for source-scoped custom kit behavior. |

## Consequences

### Positive

- Off-field rules use the correct source and existing stat/AE authorities.
- Mode, timed buffs and permanent-until-hit effects have independently verifiable lifetimes.
- No mutable combat state on SOs, per-ally drain coroutines or extra AE gauge.
- Other characters keep their current input, passive and projectile behavior unless explicitly opted in.

### Trade-offs

- Adds explicit cast/source/lifetime plumbing and a narrow shared combat observation point.
- Requires focused regressions for ordering, subscription cleanup and pooled enemy life identity.
- UI needs a mode snapshot for duration rather than interpreting a Forever recipe as an independent timer.

### Migration

1. Add runtime contracts with no configured characters; verify no roster behavior changes.
2. Add cast identity, AE reasons and confirmed-hit ordering; verify source/QTE/hold regressions.
3. Configure Goenitz and remove superseded legacy trigger rows atomically within the asset migration.
4. Author new actions/projectiles/recipes, verify level gates, then add state-driven presentation.
5. Preserve current editor-authored asset changes. No dependency/build/CI edits are part of this decision.

## Related

- ADR-001 AE authority; ADR-002 recipes; ADR-004 target-owned channels; ADR-005 deployed stats; ADR-008 damage delivery; ADR-010 event/Artifact boundaries; ADR-019 kit information ownership.
- `../Specs/goenitz-blood-frenzy-kit-design-spec.md`
- `../Specs/goenitz-blood-frenzy-kit-arch-plan.md`
