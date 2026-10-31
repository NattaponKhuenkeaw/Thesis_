---
slug: main-meter-max-cancel
status: draft
source: manual
gdd_tags: [combat, characters, visual-audio]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Main combat changes — implemented design for review

The user's 2026-10-29 goal supplies the mechanics; live feel remains unapproved.
See the task card for scope and values. This revision supersedes the older broad passive
meter gains and Ultimate-category Dimension Slash window.

Meter gains use an ordered CharacterObject list, matching stable action id (or Ground/Air's
existing name) and optional step/category. First match wins; unlisted actions award zero.
Execution Breakdown is 5 per accepted ordinary hit. Starting values are 1 for Dimension Cut,
Dimension Slash and Jailbreak Punisher; ordinary Ground/Air attacks retain 2, Attack3 4,
Attack6 6 and Dark Chain 8. The existing capacity and Void Area positive-gain multiplier apply.
Whiffs, blocked damage and secondary damage without ordinary-hit proc permission do not earn it.

Committing Main's QTE intro grants `main.qte.dimension_cut.ready` and adds one to
`main.shadow.cached_cuts`. Ordinary fallback and failed preparation grant neither.
The next manually committed Dimension Cut spends one cached cut and removes the marker;
it contributes through the existing five-cut progression. The reward does not itself count
as a Dimension Cut. Existing action timing/cancel boundaries still apply.

The five-second Judgment Cut/Dimension Slash offer belongs to Skill, including its resolver
entry, availability/consumption filters and descriptions. AE and Ultimate hold selection
remain attached to Jailbreak Punisher/Execution Breakdown.

QTE targeting prefers the original struck enemy when it supports safe placement; otherwise
try another valid in-range target with safe placement. If none works, preserve legal ordinary
fallback. Do not weaken wall/floor/path checks or teleport to an out-of-range enemy.

MAX CANCEL permits a different Ultimate during Recovery, before its ordinary Cancel boundary.
Startup and Active stay committed. Existing authored Ultimate-interrupt opt-out remains valid.
Destination conditions, resources, AE and cooldown must pass; exclude only the outgoing
action's cooldown from shared-slot rejection. Failed requests retain the current execution.
Accepted replacement restarts Startup and displays MAX CANCEL once. Same-action resets are excluded.

The callout uses bold italic gold text over a thin red streak, inspired by the supplied image,
without copying its artwork. Starting presentation: 0.85 seconds unscaled, paused with gameplay,
46px text at 1280x720 reference resolution, brief scale settle and fade. Test both facings,
small/large displays and each Ultimate overlay. Pass when readable without obscuring the target;
if unclear, adjust placement/size before adding effects. The destination retains its authored audio.

Playtests: novice can distinguish ready QTE from a successful ordinary fallback; stress simultaneous
tap/hold/cancel inputs; skilled players can chain distinct Ultimates during recovery; rejected
inputs cannot erase an execution or create a free resource; observers can read MAX CANCEL and
the incoming character's QTE. None of these live acceptance checks has been completed yet.
