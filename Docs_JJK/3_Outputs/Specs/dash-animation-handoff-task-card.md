---
slug: dash-animation-handoff
status: needs-human
source: manual
gdd_tags:
  - platform-input
  - combat-scope
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Fix: Unconditional Dash Animation Handoff

- Request: user reported Goenitz's long Dash clip being cut and explicitly asked to fix the unconditional handoff.
- Workflow: `/fix-bug`; existing ADR-003 motor/FSM ownership is retained.
- Root cause: Dash Recovery/Cancel forcibly plays locomotion, and natural gameplay completion also requests Grounded/Fall before a longer clip ends.
- Intended result: controls/gravity return at Recovery; neutral input allows the currently playing Dash clip to finish, even beyond gameplay Dash completion. New movement/actions may immediately replace it. No extra movement lock, dash resource, or cooldown delay.
- Scope: `PlayerDashState`, `PlayerAnimator`, focused native regression checks, current movement documentation.
- Acceptance: real Goenitz Animator retains Dash through neutral ground/air Recovery and natural state exit; movement interrupts it immediately; clip completion returns locomotion; existing movement/cooldown checks pass.
- Human checkpoint: bug fix authorized by the current user request; subjective in-game review remains after verification. No new timing values, architectural authority, or dependencies.
- Result: unconditional Recovery/Cancel clip replacement removed. PlayerAnimator preserves only the currently unfinished neutral Dash state, including Grounded/Fall restoration requested at gameplay exit; movement or clip completion releases presentation. Motor, phase timing, and cooldown code are unchanged by this fix.
- Verification: real Goenitz neutral ground/air cases reproduced two failures before the fix. All 12 native checks pass after it, with no C# compiler errors or scoped whitespace issues. Human rendered traversal review remains pending.
