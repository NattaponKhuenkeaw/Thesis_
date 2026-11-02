---
slug: main-skill-potential
status: needs-human
source: manual
gdd_tags: [combat, characters]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# DevLog: 2026-11-02 — Main Skill Potential

## Goal and changes

Applied Apricha's requested Base/Lv1–4 Skill revision: remove base vulnerability; 20%/17s and +5 AE Reward at Lv1; gentle field Magnetism at Lv2; replace vulnerability with 40%/17s at Lv3; allies gain Main's ATK / 8 percentage points of DM and ADI for 17s at Lv4. Retired old extra hits, action multiplier and crit buff; updated panel descriptions. GDD references: `@tag:combat`, `@tag:characters`; formulas reuse ADR-002 and ADR-005.

Magnetism is an optional projectile speed exposed in Inspector and Projectile Editor, independent of damage cadence/quota. Requests use the existing terrain sweep and respect lifecycle, armor, immunity, hit freeze, grab ownership and projectile expiry/pooling. Starting speed: 1.5 world units/second, horizontal within the existing field.

Fixed same-recipe stat buff reapplication retaining stale source snapshots in inventory and RuntimeEffectReceiver. Regression: 80 ATK gives +10, refreshed 164 ATK gives +20.5 DM/ADI. Added explicit editor authoring command to update the loaded assets when external YAML import retained stale objects.

## Validation

- Unity compiled with no console errors.
- Focused isolated suite: 68 passed, 0 failed.
- Broader suite: 124 passed, 11 meter/cache/target mismatches remain, documented in the task card. These also occurred in the initial validation during this session and concern unrelated existing local edits.
- Changed tracked scripts pass `git diff --check`.

## Game feel and next step

Human combat/readability review remains pending using `../TestPlans/main-skill-potential-test-plan.md`. Preserve the user's unrelated dirty assets/scripts. Work is on `codex/main-skill-potential`; no commit, merge or publishing was performed.
