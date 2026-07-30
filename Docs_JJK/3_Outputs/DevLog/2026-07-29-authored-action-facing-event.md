# 2026-07-29 — Authored Action Facing Event

Implemented a frame-authored facing event for combat actions.

- Added `SetFacingDirection` to the existing Action Event system.
- Kept facing application owned by `PlayerMovement.SnapFacingDirection`.
- Added `Opposite`, `Left`, and `Right` authoring modes.
- Updated the Animation Editor Action Event property card.
- Added an Opposite event to Rugal's `Grab FollowupEX` at frame 1, before its frame-2 dash motion.
- Added EditMode coverage for direction resolution.

The live Unity Editor was in Play Mode during implementation, so the remaining checkpoint is an in-editor compile/test and visual EX-route playtest.
