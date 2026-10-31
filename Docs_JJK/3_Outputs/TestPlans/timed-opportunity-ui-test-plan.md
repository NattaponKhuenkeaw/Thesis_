---
slug: timed-opportunity-ui
status: approved
gdd_tags: [ui, combat, visual-audio, platform-input]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Timed opportunity HUD verification

Design and architecture approved by Apricha on 2026-10-31. Gameplay rules and
inputs remain unchanged. Automated results and authored-canvas captures are saved
under `../Reports/TimedOpportunityUI/` after running the focused validation menu.

## Automated regression

Run `Tools/Project/UI/Run Timed Opportunity Tests` in Unity EditMode.

- ActionWindowIconTests: Normal/Skill/Ultimate temporary art and shared outline
  appear for the selected eligible action; expiry/consumption restore normal art;
  preview does not consume gameplay tags/counters; resource/context/cooldown gates;
  existing authored Main art and input-route mapping; prefab wiring.
- AbilityUIFeedbackTests and UIFeedbackWiggleTests: silent rebind/re-enable, ordinary
  cooldown/readiness, movement-owned dash and non-drifting rejected feedback.
- QteTeamSwapTests: existing QTE entry/window/cooldown/fallback behavior, shared
  portrait outline; plus one standby slot for two members, reuse across swaps and
  stat refreshes, intermediate stat-event ordering, fixed `1` swap label across
  roster indices and gaps, no standby for a solo roster, preserved inactive alpha and ready
  restoration, rapid portrait swaps and reduced motion.
- Additional AbilityUIFeedbackTests: idempotent shared Graphic, no raycast interception,
  icon-mask independence, static core in reduced motion, frozen pulse on pause,
  empty geometry after clearing. Impact-tuning regression samples several wave
  phases to check large outward expansion and the bounded inward art-edge feather.

## In-game visual/feel acceptance

1. In training, open a Skill follow-up, a timed Normal/Ultimate when authored, and
   QTE on the standby portrait. Confirm the same slow square outline appears on
   each eligible input; art remains fully visible without the old time-window fill.
2. Execute each opportunity and separately let it expire. Normal art and ordinary
   cooldown/AE presentation return; no stale outline appears on the next character.
   With Main, use Dash to cache a Dimension Cut. When the resolver selects that
   resource-backed Normal action, confirm the NA ring appears; after spending the
   cached cut, confirm the ring clears. Resolver context/target restrictions still
   determine whether it is offered.
3. Swap normally and through QTE. One standby slot remains. Old portrait exits left;
   the swapped-out teammate enters from the right. HP/AE/hotkey belong to the new
   standby destination immediately; animation never gates input.
   Verify top-row `1` and numpad `1` both swap from either active character; `2`
   has no team-swap binding.
4. Attempt illegal swaps and rapid alternating swaps. Rejections do not exchange
   portraits; successful exchanges return to the same position with no slot clones.
5. On swap cooldown, InactiveCanvasGroup stays active at the configured inactiveAlpha
   (initially preserved from authored alpha 0.34). On readiness it returns to 1.
   No opaque unavailable panel covers the art or hides the grouped content.
6. Pause, re-enable the HUD, change Ground/Air/resource eligibility and refresh a
   window during its lifetime. No extra window, resource spend or stale effect.
7. Set the existing QTE reduceUiMotion flag: static shared outline and instant
   portrait exchange for both UI types. Restore the user's preferred setting.
8. Observer/readability: identify the temporary input and standby teammate without
   explanation. If busy, increase pulse period before reducing core contrast; if
   unnoticed, raise core contrast before increasing speed. If portrait tracking is
   difficult, shorten slide travel before altering swap rules.

Impact tuning requested after the first playtest: confirm the two staggered waves
look substantially larger than the first version. The slight inward glow should
touch only the rim; the main action symbol/portrait center must stay clear. If the
effect is too busy, lower wave opacity before reducing its visible footprint.

Subjective in-game feel remains a human playtest checkpoint. Automated canvas
captures and state tests do not substitute for that sign-off.
