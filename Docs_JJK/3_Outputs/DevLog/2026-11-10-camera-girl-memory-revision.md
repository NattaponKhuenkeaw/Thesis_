# Camera Girl authored kit revision

Implemented the supplied base Capture behavior and six Skill/Ultimate/Passive upgrades through saved assets and the existing potential, RuntimeEffect, action and coordinated-assist systems. User clarifications set Ultimate3 to30s total, Len Focus to one scene-wide round-robin target per second with a team-wide aura, and Mastery to the authored cg_attack6 action step.

Shared improvements support source-owned reactions, source-keyed cooldown reduction, recipe identity replacement, capped source-stat buffs, dependent movement auras, delivery conversion, marked-target filters and target mitigation input. The lens presentation follows the active ally while its combat effect remains owned by Camera Girl. No runtime-generated configuration or character-specific serialized kit block was introduced.

Review found two assist issues: lower unlocked ranks could reroll a failed higher-rank chance, and a650% override could be reduced by the old hitbox share to422.5%. The selected policy now owns one roll, and a positive damage override is a total step budget. Explicit3AE rewards suppress ordinary hit AE and require confirmed damage. Earlier-rank damage is preserved.

Validation:62 passed,0 failed,0 skipped in the isolated EditMode runner, including actual ShadowActionExecutor hit damage, shared damage types and Main swap/HUD regressions. Unity compiled with zero console errors. Native PlayMode and human combat feel review remain unrun; scope is recorded in the test plan. Changes remain in the current checkout alongside prior Main work. Test_Playground, unrelated training reports and build/dependency/CI settings were not edited by this revision. No merge or team publication was performed.

## Rei Memory routing fix

The earlier kit update authored Rei's nodes while the Memory system still selected combat ranks, descriptions and base stat bonuses through a Main-only character check. Main and Rei now opt into the shared Memory path through their kit assets. The inspector and Training hide Previous Upgrades for both. The expanded EditMode suite passed 63 cases and the Unity console reported zero errors. The inspector PlayMode regression remains unrun.

## Len Focus strike animation

Len Focus skipped the multi-frame `cg_ult-projectile` clip and expired as soon as it spent its hit. The authored strike now plays the clip while its one-hit collider is active. The final frame holds for 0.1 seconds before the projectile returns to the pool. The Unity EditMode suite passed 64 cases, including actual sprite changes after the hit, the final loop and timed release.

## Captured splash timing

Skill Lv.6 checked for Captured after the source passive applied it. The explosion recipe now requires Captured before the hit, so the first hit marks and the next qualifying hit can splash. The regression covers first application, an already marked target, and a target whose mark was removed. Unity runtime and editor assemblies compiled successfully.
