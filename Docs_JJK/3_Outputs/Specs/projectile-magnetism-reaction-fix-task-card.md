---
slug: projectile-magnetism-reaction-fix
status: needs-human
source: manual
gdd_tags: [combat, characters]
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Projectile magnetism during reactions

Source: Apricha's report on 2026-11-07: Main pushes enemies away; Goenitz projectile_ult magnetism has no visible effect. Workflow: `/fix-bug`. Reuse the existing ProjectileData.magnetismSpeed and shared projectile path. Do not introduce a Main-specific pull system or alter authored damage, pull speed, hit timing, immunity or grab ownership.

User corrections: Main's Dark Chain no longer needs stun at any rank. Goenitz's reported targets are beside the tornado, outside its hitbox; targets inside are already captured. Main Normal Attack cannot start although LMB Dimension Cut still works.

Acceptance: remove Main's Memory stun and its descriptions/configuration. Reuse projectile magnetism with separate horizontal reach beyond the damage collider; outside targets can be pulled but cannot take damage or be grabbed until they enter the original hitbox. Preserve position-reaction vertical motion, terrain clipping, impact freeze, immunity, grab ownership and expiry/reset. Remove Main Skill Lv4's blank modifier that disables all Normal actions. Verify base NA stages 1–3 remain available and Passive Memory Lv1 adds only stages 4–5.

Game-design checks: clearly inward movement, unchanged input/cancel/resource rules, existing visuals/audio. Starting value: Goenitz's four Ultimate projectile variants receive 2 world units of extra horizontal reach per side; speed stays authored at 10. Check a target just outside the tornado pulls inward, a target beyond the range stays still, and damage/grab begin only inside. Reduce reach if it captures unrelated groups; increase if nearby intended targets remain unreachable. Human playtest confirms crowd grouping/readability.

## Verification

Implementation complete. Unity compilation succeeded; the focused Memory/projectile suite passed **79 tests, 0 failures** (`Temp/main-memory-fixes-tests.xml`). Main Normal Attack lock was reproduced before removing the blank Skill Lv4 modifier. Physics tests cover inward movement and nearby targets excluded from damage/grab tracking. The existing reaction motor passed simulation checks and was left unchanged. See `../TestPlans/projectile-magnetism-reaction-fix-regression.md` for human combat feel/readability checks, including the captured-enemy requirement for Normal stage 5.
