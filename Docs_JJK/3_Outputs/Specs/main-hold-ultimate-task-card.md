---
slug: main-hold-ultimate
status: needs-human
source: manual
gdd_tags: [combat]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main hold Ultimate

Source: Apricha's 2026-10-29 request to move all current Execution Breakdown data from QTE into a second, hold-input Ultimate, and author Dimension Slash as Ultimate to prevent Ult input interrupting it. Workflow: `/refactor`, including the explicitly requested input behavior change. The request authorizes this scope within the existing resolver, input buffer and Ultimate state.

Goal: preserve authored action content while making tap/hold Ultimate selection data driven.

| Responsibility | Before | After |
| --- | --- | --- |
| Execution Breakdown ownership | Lightning Plunge QTE follow-up | Ultimate action referencing the same existing node |
| Dimension Slash ownership | Skill action | Ultimate action, retaining stable action ID and ready-tag conditions |
| Ultimate input | Immediate press | Immediate press for characters without hold actions; tap release or one hold dispatch for characters with hold actions |
| Buffered input | Ultimate timestamp | Ultimate timestamp plus tap/hold snapshot |
| Ultimate interruption | Ready window can bypass phase gate | Current Ultimate retains its phase gate even during a ready window; Judgment Cut disables Ultimate cancels in its action settings |

Assumption: tap retains Jailbreak Punisher, or Judgment Cut during its existing five-second ready window; hold selects Execution Breakdown. Execution Breakdown inherits its former QTE action damage budget (5), source stat, Both input/Ground output and zero cost, with no new progression gate. No balance redesign is requested. The legacy stable ID `main.skill.dimension_slash` is deliberately retained; membership in `ultimateActions` now determines its Ultimate category.

Starting value: hold threshold 0.35 unscaled seconds, editable per Ultimate in ActionResolver. Hitstop/slow motion does not stretch the physical input gesture; pause/UI input locks cancel it. Manual check: ten taps should select the tap action and ten deliberate holds should select Execution Breakdown exactly once, including buffered inputs. Lower threshold if intended holds feel slow; raise it if intended taps select hold. Tap selection occurs on release and checks the ready window at that time.

Acceptance: all existing Execution Breakdown managed-reference records, animation, hitboxes, motion, events and audio remain byte-for-byte; Lightning Plunge becomes terminal; ready-tag consumption/modifiers use Ultimate; Ult input cannot interrupt Dimension Slash startup/active; no Unity scripts, authoring tools, compiler commands or tests are executed.

Manual review: tap/hold from idle and legal attack cancels, release around threshold, hold through completion (no repeat), release after switch/pause/UI lock/focus loss (no stale cast), ready-window consumption and expiration, repeated Ult input during Judgment Cut, ground/air restrictions, and unchanged Execution Breakdown presentation/damage. Existing action clocks, costs, damage response, exit and cancel policy remain owned by Ultimate state. Existing visual/audio tracks supply feedback; the authored action names and input selector expose the distinction to the owner.

Runtime validation remains for the owner under the standing no-script restriction. Static source, reference and diff review only.

Static result: all 139 saved ActionResolver managed-reference records remain present. Only QTE root record `8000281000000000008` changes, removing its default-next link. No positive RID references are unresolved. All other managed records, including Execution Breakdown's complete node/animation/hit/motion/VFX/audio/event data, are unchanged from the pre-task Git baseline. No animation files were edited. Existing test source was adapted for Ultimate routing and terminal QTE, and its fixture no longer calls asset authoring; it was not executed.
