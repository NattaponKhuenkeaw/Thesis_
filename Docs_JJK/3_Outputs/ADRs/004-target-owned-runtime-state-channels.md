---
slug: target-owned-runtime-state-channels
status: approved
source: manual
gdd_tags:
  - combat
  - characters
  - progression
  - ui
owner: codex
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# ADR-004: Target-Owned Runtime State Channels

| Field | Value |
| --- | --- |
| Status | **Accepted** |
| Date | 2026-07-28 |
| Author | Codex |
| GDD Section | `@tag:combat`, `@tag:characters`, `@tag:progression`, `@tag:ui` |

## Context

ADR-002 intentionally made recipe identity part of runtime-effect matching so two uses of a shared
formula do not merge accidentally. That rule is correct for ordinary buffs and debuffs, but it
splits state such as shields whose gameplay ownership belongs to the target rather than a recipe.
Separately, structural `Always`-effect reconciliation currently runs from ordinary value-change
events, amplifying notifications and stat rebuilds as more Potential nodes unlock.

## Decision

Add `RuntimeEffectStateChannel`, defaulting to `None`, to `RuntimeEffectDefinition`.
`ShieldRuntimeEffectDefinition` declares `Shield`. Runtime containers first resolve an existing
instance by target and non-`None` channel; only non-channel effects use the existing
definition/recipe/stack-key compatibility rule.

Channel reapplication keeps one instance and one stack, adds shield value through the incoming
definition/recipe context, rebinds source, snapshot, recipe, definition, key, duration, and
presentation, clears forced expiry, and restarts the incoming timer. Runtime identity and
compatibility helpers live in `RuntimeEffectRuntimeUtility` and are shared by the run inventory and
local receiver.

Structural `Always` bindings are reconciled from team, inventory, initialization, and Potential
changes only. Reconciliation uses reference-based binding keys and a desired set, mutates only
missing/obsolete bindings, coalesces notifications, and performs one final stat refresh.

Potential truth is cached by `PlayerMetaProgression`, which raises `OnPotentialChanged` after
mutations and load/reset operations. Controllers cache derived timed-window candidates. Runtime
tag expiry remains query-driven, and interval effects use their next-due timestamp as the gate for
formula context construction.

## Alternatives Considered

| Option | Pros | Cons |
| --- | --- | --- |
| Require one manual shield `stackKey` everywhere | No runtime API change. | Fragile authoring convention; recipe/value changes can silently split truth again. |
| Special-case `ShieldRuntimeEffectDefinition` in both containers | Small local diff. | Duplicates ownership rules and lets inventory/receiver behavior drift. |
| Target-owned state channel plus shared identity utility | Reusable invariant, preserves recipe identity elsewhere, testable in both containers. | Adds one explicit runtime concept and requires instance rebinding. |
| Move shields outside runtime effects entirely | Strong single ownership. | Duplicates duration/presentation/lifecycle machinery and breaks recipe reuse. |

## Consequences

### Positive

- Shield amount, expiry, absorption, VFX, and HUD presentation share one authoritative instance.
- Authors no longer need coordinated shield stack keys.
- Non-shield effects preserve ADR-002 recipe isolation.
- Potential growth no longer multiplies structural work from ordinary runtime value changes.
- Shared helpers reduce duplicated matching logic.

### Negative / Trade-offs

- A later target-owned mechanic must deliberately opt into a channel and define its reapply rule.
- Rebinding means the current shield presentation/source reflects the most recent application.
- Performance acceptance still requires an editor/profile capture; static code changes alone are
  not evidence of the 60 FPS budget.

### Migration

- Existing shield assets need no YAML migration; their stack keys become irrelevant to ownership.
- Existing runtime-effect application overloads remain source compatible.
- Existing callers may use channel lookup for authoritative state but do not pass shield IDs.
- No build setting, package, dependency, or balance migration is required.

## Related

- Extends ADR-002: `Docs_JJK/3_Outputs/ADRs/002-runtime-effect-recipes.md`
- Task card:
  `Docs_JJK/3_Outputs/Specs/centralized-shield-potential-runtime-performance-task-card.md`
- Regression plan:
  `Docs_JJK/3_Outputs/TestPlans/centralized-shield-potential-runtime-performance-regression.md`

