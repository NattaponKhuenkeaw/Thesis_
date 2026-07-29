# 2026-07-29 — Deployed Character Live Stats

Implemented ADR-005 so active and off-field deployed characters share one
runtime stat authority.

- Added runtime-only deployed-character records owned by `TeamLoader`.
- Delayed projectile and Additional Damage formulas now read their recorded
  source's live effective stats at damage resolution.
- Removed-source damage falls back to its captured spawn/application snapshot.
- Buff, debuff, heal, and shield formulas use source snapshots captured when
  applied.
- Off-field hits evaluate the source character's Potential action and outgoing
  damage modifiers without using the active character's rebound passive
  controller.
- Fixed artifact ownership after character switching: deployed runtimes now
  retain their own artifact snapshots, stat rebuilds resolve artifact slots
  from character identity, and artifact passive snapshots use the same
  source-owned runtime record.
- Added and passed one integrated E2E test; no unit tests were added.
- Passed an additional temporary E2E regression for distinct per-character
  artifact ATK bonuses across an active-character switch; the probe was removed
  afterward.
