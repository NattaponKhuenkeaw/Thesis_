# 2026-10-31 — Shared timed-opportunity feedback and standby-slot exchange

Apricha approved the design and architecture and requested that standby cooldown
presentation use the existing InactiveCanvasGroup alpha instead of the old panel.

Implemented TimedOpportunityGraphic as a small procedural UI mesh: stationary
core, feathered glow and slower expanding wave shared by timed abilities and QTE.
Converted only the dedicated ability follow-up Image components into the shared
graphics, retaining the Icon as the sprite owner and ordinary cooldown/AE displays.
Outlines bypass sprite masks so their expansion remains visible.

TeamSwitchHUD now shows inactive destinations only and reuses the standby slot
across swaps. CharacterSwitchSlot exchanges portrait-only content and keeps the
new destination bound immediately. Stat-event ordering, rapid swaps, silent
rebind, actual roster indices and reduced motion are covered.

The authored CanvasGroup remains active. Its latest intake alpha 0.34 is now an
editable inactiveAlpha setting; the live group returns to 1 when ready without
losing that tuning value. Updated GDD ui/combat wording and the test/review artifacts.

Unity compilation and final console check passed. Focused EditMode suite:
60 passed, 0 failed, 0 skipped. Inspected ordinary Main, timed Skill and QTE canvas
captures in Reports/TimedOpportunityUI. Existing current Main ground-combo art has
an unresolved Sprite subasset; verified the intended HUD fallback without editing
the character data. Full combat feel sign-off remains a manual playtest.

No gameplay rules, timings, costs, input routes, project configuration or external
team state changed. Existing dirty work was preserved. No commit, push or merge.
