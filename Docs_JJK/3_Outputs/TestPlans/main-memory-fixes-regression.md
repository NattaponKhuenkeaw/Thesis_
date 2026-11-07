# Main Memory fixes regression

Automated: `Tools/Project/Combat/Validate Main Memory Fixes`.
2026-11-07 result: **79 passed, 0 failed**, `Temp/main-memory-fixes-tests.xml`.
Latest corrections and magnetism/Normal Attack coverage are in `projectile-magnetism-reaction-fix-regression.md`. Includes Skill rank boundaries, summoned template/reference settings, configurable finisher damage and rank gating, Magnetism, enemy position reactions and combat damage types. Unity Console reported no compilation errors; existing legacy Artifact obsolete warnings remain.

Human playtest:

- Latest user correction: Dark Chain does not stun at any rank. Compare Skill Lv1/Lv2/Lv3: no pull, gentle pull, stronger pull. Enemy immunity/death retains existing ownership.
- Open Main → Kit Definition → Potential Nodes. Skill Lv4 exposes Dark Chain Summon. Passive Lv5 exposes Ultimate Finisher Damage: ATK ratio 0, source MaxHP, source ratio 0.2, Darkness.
- At Skill Lv4, cast with enemies on the floor and on raised platforms. Summoned chains should use the surface below the selected enemy. Verify four strikes at 2.5-second intervals within the 12-second summon window. Check visuals/readability against existing SFX and hit feedback.
- Edit summon settings before Play and verify template, interval, duration and damage. Cast repeatedly, swap Main off-field, kill the current target, and clear the run; target reacquisition and pool cleanup should retain existing behavior.
- At Passive Lv4, neither Ultimate adds the HP finisher damage. At Lv5, only each Ultimate's final melee hit adds it. Change Source Stat Ratio before Play to verify the authored amount. Compare targets with different Max HP to confirm scaling uses Main's HP.
