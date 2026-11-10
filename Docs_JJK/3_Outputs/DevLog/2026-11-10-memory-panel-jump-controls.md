---
slug: memory-panel-jump-controls
status: approved
gdd_tags:
  - ui
  - progression
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Memory panel jump controls

The inspector and Training share fixed **Go to Top** and **Go to Bottom** buttons above the Memory map. Each button stops scrolling, jumps to the edge of the current page, and focuses the destination node. Switching to Previous Upgrades hides the buttons and restores the authored viewport.

An isolated Unity check used Page II of the authored inspector to verify both scroll positions, destination focus, and the viewport reset. The existing inspector regression also covers both buttons and page switching.
