---
slug: qte-unavailable-swap-fallback
status: approved
source: manual
gdd_tags: [combat, combat-scope, characters, platform-input]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# QTE readiness incorrectly blocks a legal swap

Source: Apricha's 2026-10-29 report that QTE becomes unavailable after triggering even
in a safe swap state; clarified that the black indicator is the character portrait cover.
Workflow: `/fix-bug`. Existing approved behavior is in `qte-team-swap-design-spec.md`,
Legal input and handoff: when target/entry preparation fails, fall back to an ordinary
swap if legal. The newer `qte-window-policy-fix-task-card.md` remains authoritative
for shared state restrictions and valid QTE cooldown bypass.

## Root cause

`CanSwitchCharacterTo` returns QTE preparation failure directly whenever a window exists.
`TeamLoader.SwitchToIndex` also explicitly refuses a ready window whose preparation fails.
Thus a dead/out-of-range target or unsafe entry turns readiness into a restriction even
when ordinary switching is legal. The portrait correctly mirrors that incorrect decision.
This source-level failure is confirmed; which entry condition failed during the reported
play session is not known.

## Scope and acceptance

- Restore ordinary eligibility fallback in the FSM portrait/input gate and TeamLoader commitment.
- Valid prepared entries still execute QTE and retain their existing cooldown bypass.
- Failed preparation respects ordinary cooldown and safe-state rules.
- Rejected input keeps readiness and its deadline; successful ordinary or QTE switching clears it.
- No target/terrain checks are weakened; no character authoring or scene assets change.
- Add regression cases for lost targets, invalid entry, cooldown, unsafe state, and portrait feedback.

## Experience and validation

Combat response/clarity: earning QTE must not take away an otherwise usable swap.
Existing ordinary/QTE transition feedback and authored values stay unchanged.
No new player states or numeric tuning. Enter via an existing valid swap request; on a
failed QTE preparation, enter ordinary swap only if its existing rules pass.
Human playtest remains required; do not claim the original live scenario was reproduced.
Automated regression execution is pending. Static compilation and diff checks will be recorded below.

## Validation result

Runtime and Editor assemblies compile successfully using Unity's current Roslyn response
files, with all outputs isolated under `Temp/QteSwapFallbackCompile`. The Editor build
includes `UNITY_INCLUDE_TESTS` and the new regression cases. `git diff --check` passes.
Unity gameplay/test execution was not performed. Compilation reports existing project
warnings; there are no compilation errors. Review confirms both eligibility and commitment
use the same shared state rule and distinguish valid QTE cooldown bypass from ordinary
fallback cooldown enforcement. Failed requests return before readiness clearing or loading.
