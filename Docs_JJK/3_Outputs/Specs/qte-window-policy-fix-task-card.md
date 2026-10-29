---
slug: qte-window-policy-fix
status: needs-human
source: manual
gdd_tags: [combat, combat-scope, characters, platform-input]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# QTE / Dimension Slash window and swap policy correction

Source: Apricha's 2026-10-28 report that QTE briefly appears/disappears, CameraGirl Skill fails to prepare the QTE, and both QTE and Dimension Slash should have five-second windows. Explicit direction: QTE shares the ordinary Team Swap Policy. Workflow: `/fix-bug`.

Root causes: QteTeamSwapController subscribed Clear directly to OnTeamChanged, which TeamLoader also publishes during health/stat/runtime-buff refreshes. CameraGirl's buffs and ordinary damage/healing can therefore erase an accepted-hit window. The portrait pulse additionally depended on momentary placement/switch eligibility, and TeamSwitchHUD rebuilt portraits on all those stat notifications. QTE had its own hit-confirm/Active-phase exception alongside the ordinary FSM policy. Main's Dimension Slash ready tag and authoring utility still specified 1.5 seconds.

Implemented: preserve QTE through health/stat refresh and same-character notifications; invalidate on actual roster/source/field-session changes, death, scene change, disable, expiry or successful swap. Capture roster only at window acquisition. Keep the readiness pulse/timer visible while the opportunity exists; existing unavailable visuals still communicate the shared switch restriction. Reuse CanSwitchCharacter for index routing, TeamLoader commitment and QTE preparation. Keep portraits when slot bindings match the roster. Set the saved Dimension Slash tag, authoring utility and player descriptions to five seconds. Both windows use scaled combat time and remain configurable through existing authored fields.

Acceptance: CameraGirl Skill/EX accepted primary hits each prepare Main for five combat seconds; buffs, damage and healing do not consume/restart that deadline. Ordinary and QTE swaps agree across Startup/Active/Recovery/Cancel, auto-chain, airborne, hitstun, input locks and shared cooldown. A blocked switch retains readiness. Actual roster changes, source death and successful switching still clear it. Dimension Slash remains available for five seconds after its qualifying fifth paid cut, unless consumed.

Validation: runtime and Editor assemblies compiled using Unity's current response files with isolated outputs under Temp. Regression coverage updated for shared policy, health/stat preservation, both CameraGirl Skill ids through accepted damage, readiness during cooldown and the five-second Dimension Slash duration. These Unity cases are compiled but not executed; live Editor access remains unavailable. Manual acceptance is in the QTE test plan.
