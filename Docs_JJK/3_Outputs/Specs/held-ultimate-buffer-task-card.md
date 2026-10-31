---
slug: held-ultimate-buffer
status: approved
source: manual
gdd_tags: [combat, platform-input]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Held Ultimate input buffering

Workflow: `/fix-bug`. Direct user request, 2026-10-29: HOLD input should listen to the input buffer because hold Ultimate MAX CANCEL is difficult.

GDD references: `@tag:platform-input` short action buffering and `@tag:combat` state-authorized cancel boundaries. This request authorizes keeping a charged physical hold queued beyond the ordinary tap timeout. Existing hold thresholds and the 0.15-second released-input buffer are unchanged.

Root cause: the handler dispatches the hold once on reaching its threshold, then stops dispatching. The buffer expires 0.15 seconds later even if the button is still down, so a hold charged before Recovery silently disappears.

## Acceptance

- Charge a hold during Startup/Active; keep it pending while physically held and consume at the first legal cancel boundary.
- Never bypass commitment phases, authored opt-out, destination cooldown or resource costs.
- Consume once per physical press. Another buffered action clearing the queue must not recreate the hold.
- Releasing a charged queued hold starts the existing short buffer; a short press still selects tap.
- Focus loss, disabled input, pause/input lock, invalid character or action map cancels the queued gesture.
- Preserve Skill/Ultimate input and Action Type separation, costs, presentation and authored assets.

## Player experience and verification

Response: allow preparing a hold during commitment instead of forcing threshold timing inside Recovery. Clarity: tap/hold keeps its existing selection rule. Satisfaction/fit/motivation: use the existing action, audio and MAX CANCEL feedback; no new feedback or balance values.

State entry remains the existing policy/availability gates; consumption starts the destination action and cleans up the source. Natural completion, other consumed actions and invalidated input clear the existing queue. Zero AE, destination cooldown and source opt-out remain rejection cases.

Automated regression: real Input System key state through gesture update, then actual buffered FSM consumption; expired timestamp while still held, release expiry, short tap, invalidation, one-shot consumption and existing MAX CANCEL rejection tests. Keep the user's dirty scene intact using the existing isolated validation runner.

Manual checkpoint: hold R through the source Ultimate's Active phase; it must MAX CANCEL at legal Recovery with the existing callout. Release early, spam other actions, pause/unpause, change characters and repeat with insufficient AE. Confirm a new physical press is needed after the queued input is consumed.

Native validation: **140 passed, 0 failed**, 2026-10-29 23:02 Bangkok (`Temp/main-meter-qte-max-cancel-tests.xml`). Eight added keyboard/gesture/FSM cases pass, alongside all 132 prior focused cases. Edit-mode tests explicitly enable input processing with a temporary feature flag and restore its original value in teardown; no project input configuration is persisted. Runtime and Editor compilation succeeded; no errors in the fresh console read. Live combat feel remains the human checkpoint.
