---
slug: memory-only-upgrade-ui
status: approved
gdd_tags:
  - ui
  - progression
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Memory-only upgrade UI

Removed the Previous Upgrades entry points from the character inspector and Training Config. Training now uses Memory and Global Level controls, with the old rank-slider container hidden in existing scenes. The Training draft still carries the captured fragment ranks for compatibility.

Both Unity assemblies compiled. The inspector and Training regression cases now check that the old view has no entry point.
