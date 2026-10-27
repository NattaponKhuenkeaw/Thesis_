---
slug: ability-ui-zone-feedback
status: approved
source: manual
gdd_tags: [ui, combat]
owner: Codex
human_checkpoint: E2E feel review by requester
next_agent: implementation-agent
blocked_by: []
---

# Ability UI and zone feedback

Source: request in this chat, 2026-10-26. User authorizes implementation and script tests; user owns E2E.

Workflow: `/implement-feature` because the requested UI work includes new behavior and a bug fix, rather than a behavior-preserving refactor.

GDD: `GDD_Neo-JJK.md` UI section restricts navigation/inspection to safe zones. Dash/NA feedback and hiding combat HUD in safe zones are specified directly by this request.

## Approved behavior and implementation boundary

- Dash uses existing cooldown overlay, countdown, dimming and ready-rush feedback, reading movement's fixed-frame timer. It does not introduce a second gameplay cooldown.
- NA bounces on a committed normal-attack step, excluding automatic follow-up steps and rejected/buffered inputs.
- Panel exit, initial binding and character refresh establish readiness silently; subsequent cooldown-to-ready transitions retain feedback.
- SafeZone hides PlayerHP_UI and the Cool ability display; CombatZone hides Navigation. Existing ZoneStateProvider remains authoritative.
- Keep the HUD/controller root active so zone subscriptions continue working.
- Reuse current feedback scale/duration without tuning combat timings, resources or input gates.
- Preserve the user's existing prefab/scene changes; wire the existing Player_UI slots only.

## Responsibility / impact

| Class | Before | After |
| --- | --- | --- |
| PlayerMovement | Owns fixed-frame dash timer privately | Also exposes remaining/duration for presentation |
| PlayerCombat | Commits attack steps | Also requests NA feedback for manual normal steps |
| AbilityUIManager | Skill/Ult plus redundant presentation timers | Skill/Ult/Dash driven by gameplay state; quiet binding; NA pulse |
| PlayerHUD | Zone-aware navigation | Also toggles HP and combat ability roots |

No new architecture or dependency. No build/CI/secret changes. Human feel review is the requested E2E pass.

## Acceptance / E2E checklist

- Dash countdown begins at dash end, matches its real lockout, pauses with gameplay and flashes ready once.
- NA bounces for initial attacks and manually committed combo steps, not blocked inputs or auto-chain continuations.
- Closing Team/Inspector panels repeatedly while skill is already ready produces no ready bounce.
- Actual skill/ultimate cooldown completion still produces one ready rush/pulse; energy and follow-up overlays still work.
- Character swap and re-enabling HUD while ready are quiet; active cooldowns display current remaining values.
- SafeZone: HP and Cool hidden, Navigation visible. CombatZone: inverse. Transition both ways.
- E2E remains pending with the requester; script verification is recorded in the DevLog.
