---
slug: memory-preview-refresh
status: ready-for-review
source: manual
gdd_tags:
  - progression
  - ui
owner: implementation-agent
human_checkpoint: playtest
blocked_by: []
---

# Memory Preview Refresh

## Bug

When Upgrade Preview is open, clicking a different node on the Memory map should update the existing preview to that node. Previously, the outside-click handler treated the Memory map nodes as outside the preview and closed the window during node selection.

## Fix

Recognize active major and generic Memory nodes as interactive map targets while a Memory preview is open. Clicking one leaves the preview open so its existing selection callback can replace the displayed node details. Clicking elsewhere still dismisses the preview.

## Verification

- Static source review confirms the outside-click handler checks active Memory map nodes before dismissing the preview.
- Play Mode verification remains for the Memory feature playtest checkpoint.
