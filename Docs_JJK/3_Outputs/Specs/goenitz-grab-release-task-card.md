---
slug: goenitz-grab-release
status: approved
source: manual
gdd_tags: [combat, combat-scope]
owner: implementation-agent
human_checkpoint: not-required
next_agent: qa-agent
blocked_by: []
---

# Goenitz grab release during ultimate recovery

A surviving target launched by a hit marked `Breaks Grab` still counts as held by Goenitz and blocks a prepared team swap in the final Recover step of Yamidokoku and Shin Yaotome.

After an accepted grab-breaking reaction, remove only the released target from the owning grab session. A hit without the flag, a rejected reaction, or a different held target must preserve grab ownership. Verify that the shared QTE gate accepts the prepared swap during grounded recovery once the last held target is released.
