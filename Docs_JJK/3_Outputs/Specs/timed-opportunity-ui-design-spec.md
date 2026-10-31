---
slug: timed-opportunity-ui
status: approved
source: manual
gdd_tags: [ui, combat, visual-audio, platform-input]
owner: Codex
human_checkpoint: required
next_agent: architect-agent
blocked_by: []
---

# Timed action and QTE visual proposal

Approved design, now implemented and verified. See `timed-opportunity-ui-review.md`
for implementation details, test results and the remaining manual feel checkpoint.

## Player experience

An eligible temporary action keeps its authored action icon fully visible. A bold,
glowing square outline hugs the icon; two staggered soft outlines slowly expand
and fade outside it. QTE uses exactly this visual language around the standby portrait.
There is no opaque highlight over the art and no follow-up countdown fill.

The reference clip shows a bright persistent ring and repeated outward glow on the
standby portrait (about 1–6 seconds), followed by portrait replacement around 6.4s.
Use its layering and exchange idea, adapted to the user's square-outline request;
the proposed white/cyan tint, slower pulse and exact transition direction are tuning
choices, not measured reproductions. Extracted reference frames live in
`../Reports/TimedOpportunityUI/`.
External lookup: https://honkaiimpact3.fandom.com/wiki/QTE describes radiating circles
from an eligible off-field character icon. The supplied clip remains the visual reference.

## Interaction flow

Existing gameplay opens an eligible window
  -> Existing resolver selects the matching temporary action (or QTE targets standby)
  -> Its icon/portrait gains the shared outline effect
  -> Execute the action, consume/expire the window, or lose eligibility
  -> Clear the effect; restore the ordinary icon and readiness presentation.

Successful swap
  -> Old standby portrait slides left and fades out
  -> The swapped-out character's portrait slides in from the right and fades in
  -> One standby slot settles at its original position.

Gameplay swaps immediately. Presentation never delays the swap or changes input
eligibility. A rejected swap does not exchange portraits. Reopening/rebinding the
HUD establishes its current state without an unsolicited entry animation.

## Ability icon and overlay behavior

- NA, Skill and Ultimate keep the existing resolver/input-route mapping. A
  Skill-routed Ultimate-type action remains on Skill.
- Remove the dedicated FollowUpOverlay images and their remaining-time fill
  behavior. Apply the temporary sprite directly to the existing Icon Image.
- Keep that icon fully drawn. Do not transfer the draining follow-up fill onto it.
  This interprets "just make ring effect around the UI" as an outline-only time-window cue.
- Closing a window restores the normal resolver-selected icon for the present
  character/context, then the cached fallback if nothing resolves. Do not blindly
  restore a sprite cached from an earlier character or Ground/Air state.
- Ordinary cooldown fill/countdown, AE and dimming continue to reflect actual
  usability. No active opportunity glow when resource/context gates make it unusable.
- Dash keeps its ordinary movement-owned readiness/cooldown; it gains no invented
  timed action window.

## Two-character team display

Create/display one standby slot when two characters are deployed. Never create an
active-character slot just to hide it. Reuse the standby slot across swaps.
The represented teammate's HP, AE, cooldown, alive/unavailable state, metadata icon
and the fixed `1` swap hotkey remain authoritative throughout the exchange.
The old portrait may exist briefly as visual transition content, not as an active
slot or interactive target. Disable old opportunity effects as the swap commits.
Hide the team-swap slot if there is no standby character. Keep a dead standby visible
with the existing unavailable treatment and no QTE glow.

The request scopes the combat presentation to two characters. It does not authorize
changing Team Builder capacity. If more characters remain deployable, retain one
visible slot per inactive teammate rather than silently removing usable destinations.

## Feedback and juice starting values

All new values below are **starting values**, subject to the approval/playtest below.

| Event / element | Proposed starting value | Audio |
| --- | --- | --- |
| Available outline | Brighter white/cyan core, soft edge alpha gradient; narrow art-edge spill and no filled center | Retain existing ready feedback |
| Expanding outline | Two staggered waves; 1.0 to 1.85 scale over 1.35 real seconds each, easing outward; alpha 0.85 to 0; repeat only while eligible | No repeating sound |
| Persistent core | 3.5 UI-unit bright core with 14-unit outward feather, 6-unit padding; inward feather capped to 2.5% of the shorter icon dimension | None added |
| Slot exit | Slide left by 0.6 slot width and fade to zero over 0.12 real seconds | Retain existing swap audio |
| Slot entry | Start 0.6 slot width right; ease to rest and full alpha over 0.18 real seconds | No duplicate sound |
| Reduced motion | Static core outline; instant portrait exchange | Existing audio unchanged |

Scale/alpha animation uses real time; eligibility and expiration always read the
gameplay-owned timers. No new camera impulse, gameplay slowdown or haptic behavior.
Glow must be visible in the current canvas without requiring render-pipeline,
bloom, build-setting or dependency changes. Procedural UI geometry or an equivalent
canvas-compatible effect is appropriate; implementation ownership is for the next
architecture stage. No particle system is necessary for this version.

## Edge cases and state boundaries

Entry: live gameplay window AND selected action eligible in the current context;
QTE: off-field, alive and currently eligible under the existing QTE policy.
Exit: consumption, timeout, loss of eligibility, rebind, roster change, disable.
Interruption: changes follow gameplay immediately, with no visual tail advertising
an unavailable action. Repeated preparation refreshes the existing window, without
stacking effects or restarting the pulse each frame. New action identity refreshes
sprite and outline together. Rapid swaps cancel/rebase the transition so only the
latest standby destination remains interactive. No resource cost for presentation.

Reduced motion reuses QteTeamSwapController.reduceUiMotion for QTE; the next stage
must determine a consistent shared setting for abilities without introducing an
unrequested settings system. Paused combat must not cause the outline to imply a
window is counting down in real time.

## Experience evaluation and test plan

- Clarity: fully legible artwork plus a shared outline means "temporary opportunity".
- Response: gameplay and click destination change immediately; animation adds no input gate.
- Satisfaction: slow expansion accompanies existing readiness/swap/combat feedback.
- Fit: square geometry follows the requested UI style and leaves combat readable.
- Motivation: spot opportunities during rotation without displaying a redundant active slot.

Micro test: open a timed NA/Skill/Ultimate and QTE opportunity; execute each and
let each expire. Pass if the correct input/portrait glows only while eligible,
ordinary art returns on the next HUD update, and every icon stays legible. If the
effect is too busy, increase pulse duration by 0.2s before reducing core contrast;
if missed, strengthen core contrast before increasing scale or pulse speed.

Stress: swap repeatedly, reject swaps, refresh QTE, change Ground/Air and resource
gates at window boundaries, disable/re-enable HUD, change roster and pause. Pass
if no duplicate active slot, drifting position, stale ring, stale art or incorrect
destination appears; gameplay timers/costs remain unchanged. For motion, pass if
the standby portrait reads clearly after one 0.30s exchange; reduce travel first if
tracking feels difficult. Verify reduced-motion mode without expansion or slides.

New-player/readability: an observer can name the offered input and standby teammate
without explanation. Skill: deliberately consume or skip an opportunity. Abuse:
UI toggles or swaps cannot extend a window or bypass a cost. Existing approved
action-window icon tests are the regression baseline.

## GDD alignment and checkpoint

Fulfills @tag:ui follow-up and team readiness visibility, @tag:combat QTE visibility,
@tag:visual-audio combat legibility, and @tag:platform-input existing input routing.
After design approval, update the GDD's old QTE overlay/bar description as part of
the implementation; do not record this proposal as shipped behavior yet.

**Approval:** Apricha approved the shared outline-only window cue, one standby slot,
and the slower pulse/portrait exchange starting values in chat on 2026-10-31 with
"LGTM", responding to the design and motion preview. Architecture planning may
proceed. Final subjective in-game feel still requires playtesting.

Approved implementation detail from Apricha on 2026-10-31: use the authored
InactiveCanvasGroup for team-slot cooldown dimming instead of the panel overlay.
A separate inactiveAlpha tuning value preserves cooldown opacity while the live
group returns to alpha 1 when ready. The final intake snapshot had alpha 0.34.

## In-game feel correction — 2026-10-31

Apricha found the first version too small and requested exaggerated impact closer
to the supplied reference. Clarified explicitly: slight glow spill over the icon
edge is wanted, while the center stays clear. This authorizes the scoped visual
tuning rework under Handoff_Contracts/README.md, not another architecture change.

The table above records the new starting values. Micro test: both QTE and ability
windows must show a larger outer silhouette at a glance, retain readable artwork,
and keep inward geometry within the narrow rim at several pulse phases. If too
busy, reduce wave opacity before size; if still too small, increase expansion
before increasing the inward feather. Reduced motion remains a static outline.

Glow refinement: separate a cyan halo from the white core, taper its opacity
quickly near the line and gently at the outside, and fade each new wave in to
avoid stacking a second bright frame directly on top of the resting outline.
The same expansion and narrow inward-spill limits apply.
