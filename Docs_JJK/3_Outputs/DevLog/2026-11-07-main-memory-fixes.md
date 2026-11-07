# 2026-11-07 — Main Memory fixes

Workflow: `/fix-bug`; task: `../Specs/main-memory-fixes-task-card.md`.

Root causes: Dark Chain's stun existed only in the hard-coded Lv3 runtime variant. Summon tuning/reference and the Passive Lv5 20% Max HP finisher were hard-coded outside the kit asset.

Skill Lv2 now authors Projectile Stun Seconds (0.5), consumed by Lv2 and higher. Skill Lv4 authors Dark Chain Summon settings, with Ground Projection as the reference point. Passive Lv5 authors Ultimate Finisher Damage through the existing AdditionalDamageEffectDefinition schema; final-hit/rank restrictions are preserved. Per-cast summon effects inherit the current cast's vulnerability and stun rather than retaining the first cached cast's effects. No input, cost or cancellation changes.

Unity compiled with no errors. Focused menu `Tools/Project/Combat/Validate Main Memory Fixes`: 67 passed, 0 failed. Existing obsolete Artifact warnings remain. Human combat feel/readability checks are in `../TestPlans/main-memory-fixes-regression.md`. Unrelated local work was preserved; no commit, merge, publishing or configuration changes.
