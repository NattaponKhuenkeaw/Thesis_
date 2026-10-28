---
slug: fullscreen-execution-slash
status: needs-human
source: manual
gdd_tags: [visual-audio, combat]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Clean fullscreen execution slash

Replaced Execution Breakdown's fullscreen mask with a deterministic single ribbon using the existing Dimension Slash material and shader. Removed the mask-specific shader/material and rendering/sampling branch. The event now requests the existing background white flash; its distortion tracks the actual ribbon. Authored a short twelve-frame effect and a much stronger, bounded, editable shockwave. No grain, noise or dissolve treatment is used.

Preserved the user's trigger frame, actor animations, hitbox data, damage, action timing, camera events and sound entries. Updated editor controls, serialized-name compatibility and future authoring defaults without executing those utilities. Static reference/diff review and scoped whitespace checks only; no Unity scripts or tests run. [Task card and manual review](../Specs/fullscreen-execution-slash-task-card.md).
