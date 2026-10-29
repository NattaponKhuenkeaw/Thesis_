# 2026-10-29 — Hitbox armor breaking and shield guard

## Bugs Found

Shield blocking returned before the existing Breaks Armor flag could be processed.
Armor-breaking primary hits now permanently remove shield blocking for that enemy instance
and end its active guard/strike, while leaving future attacks available.
Regression coverage added to `EnemyFlightGuardTests`; Unity execution/playtest pending.
See `../Specs/hitbox-break-armor-shield-guard-task-card.md` for scope and verification.
