# Projectile magnetism and Main Normal Attack regression

Automated menu: `Tools/Project/Combat/Validate Main Memory Fixes`.

2026-11-07 final result: **79 passed, 0 failed**, `Temp/main-memory-fixes-tests.xml`. Unity compilation succeeded. Changed-script whitespace checks passed.

Reproduction: before the fixes, Main Skill rank 4/6 failed the grounded/air Normal Attack availability tests. A pull test with an empty damage-tracking set also failed. Existing position-reaction physics tests passed, so no change to EnemyHitReactionHandler's movement motor was needed. User clarification identified Goenitz's intended targets as outside the damage/grab collider.

Coverage: actual Physics2D simulation for inward pull on either side, Main flinch and Goenitz knockup, independent detection for dynamic and kinematic bodies, outside-hitbox pull without damage/grab enrollment, out-of-range exclusion, hit cooldown/quota, terrain/immunity, and expiry/pool reset. Main resolver tests cover grounded/air entry and three stages at Passive Memory 0 versus stage 4 at Passive Memory 1+, with stage 5 retaining its captured-enemy trigger.

Manual PC checks:

- Main: Skill Lv2 and higher group enemies toward Dark Chain from both sides, with no stun. Confirm no rightward shove when the old stun would expire. Damage hitstop still applies normally.
- Goenitz: place a normal enemy just outside each side of the tornado's damage hitbox. It should move inward and only get hit/captured inside. A target beyond the extra reach stays still. Compare all four Ultimate projectile variants.
- Starting reach is 2 world units per side beyond the routine collider; speed retains the existing authored value. Tune ProjectileData → Magnetism Range in the default Inspector, or Magnetism Extra Reach in the action editor. Reduce reach for unintended group captures; increase for nearby intended targets that remain unreachable. Check visual readability and existing audio/hit feedback.
- Main: at Skill 4 with Passive Memory 0, LMB starts the three-stage ground chain and the air chain. Passive Memory 1 adds stage 4 and its captured-enemy stage 5 follow-up. Check cached Dimension Cut still takes priority when its resource is available, then Normal Attack resumes when it is spent.
- Check armored/immune enemies, grabs owned by another action, walls, expiration, repeated casts, swaps, pause and run teardown preserve existing ownership and cleanup.
