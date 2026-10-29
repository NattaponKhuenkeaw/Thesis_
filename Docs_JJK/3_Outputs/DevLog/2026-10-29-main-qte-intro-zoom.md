# DevLog: 2026-10-29 — Restore Main's Shared QTE Intro Zoom

## Goal and Root Cause

The user reported that Main's Lightning Plunge lacks CameraGirl's intro camera motion. PlayIntroHandoff still excluded LightningPlunge from shared horizontal centering and the intro zoom pulse, although the separate plunge camera controller had been removed.

## Change

- Removed the entry-mode exclusion so both QTE modes use the existing intro camera settings and centering.
- Kept the plunge-specific vertical lock, forced lens and position-return blend removed.
- Preserved existing slowdown, damage, movement, VFX and unrelated working-tree edits.
- Workflow: `/fix-bug`; task: `../Specs/camera-fall-damping-removal-task-card.md`; GDD: `@tag:combat` / `@tag:combat-scope`, QTE handoff presentation.

## Verification

Assembly-CSharp compilation succeeded (exit 0) using Unity's existing editor response file and temporary assembly outputs. Scoped diff checks passed; the removed plunge camera controller remains absent. Manual prepared-swap/preview comparison remains for the user; no automated tests added or run.
