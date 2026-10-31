---
slug: training-stats-appearance
status: draft
source: manual
gdd_tags:
  - ui
  - scene-roles
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Restore Training Debug Appearance

## Report

User screenshot, 2026-10-31: Training Stats became cramped and visually degraded after the Canvas conversion.

## Cause

The replacement renderer scaled a 1920×1080 reference Canvas into small Game views and replaced the prior styled rows/bars with plain text and manual monospacing. This altered the established debug-window appearance while fixing layer ordering.

## Fix

- Keep the independent Canvas and its sorting relative to Config.
- Restore pixel-sized 13px bold cyan headings, 11px labels/values, 10px toolbar/status text, and centered 28px yellow DPS.
- Use the original dark panel, label, separator and active/inactive toolbar colors.
- Restore HP/stun progress bars, reaction-state colors, per-character damage-share bars, aligned numeric columns and sample time.
- Resize/reposition the default panels to fit the current Game view while preserving manually dragged positions.
- Keep Stats visibility independent from Config and loadout windows.

## Validation

Unity compilation has no errors. The current Test_Playground editor scene has unsaved changes, so the scene-switching Training flow was not run and no after-change screenshot is claimed. The existing Training flow remains available for the next clean-scene run. The fix restores the established presentation while retaining Canvas layering and independent visibility.
