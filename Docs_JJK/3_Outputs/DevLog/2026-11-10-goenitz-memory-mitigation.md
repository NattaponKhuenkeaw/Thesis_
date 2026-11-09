# Goenitz Memory Mitigation

Goenitz now uses the existing Memory progression path for all six Skill, Ultimate, and Passive ranks. The upgraded tornado assets carry their own crit-splash recipes. The existing AE gauge and kit runtime still own Blood Frenzy and its shared lifetime. The old authoring menu now guards the Memory kit against a legacy migration overwrite.

Unity passed 21 focused Goenitz checks and 64 shared Edit Mode regressions. The remaining playtest is in the [task card](../Specs/goenitz-memory-mitigation-task-card.md).

The follow-up reuses Rei's yellow splash prefab on both tornados. Normal Yonokaze can splash once per accepted cast. EX Yonokaze uses a one-second source cooldown across active projectiles. Blood Frenzy now starts and deals its entry burst as soon as the held Ultimate is accepted. All 21 focused checks passed again. The shared run passed 63 of 64 after a separate workspace change to generic Memory authoring. Rei's generic Memory test expected 1,030 HP and received 1,000.

EX Yonokaze's crit explosion now uses the existing red Blood Explosion prefab. Normal Yonokaze uses Rei's yellow visual.
