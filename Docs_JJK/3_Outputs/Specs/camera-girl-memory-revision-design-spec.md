# Camera Girl kit: confirmed design

Source: Apricha's kit request and follow-up answers in this chat. Unchanged inputs, action timings and audiovisual behavior remain authored in the current project. Implementation details are in the architecture plan.

Base Capture Da Soul grants one randomly selected buff to teammates except Camera Girl for10s: CR+17% or CD+33%.

| Rank | Skill | Ultimate | Passive |
| --- | --- | --- | --- |
| 1 | Capture grants both CR17/CD33 for10s, teammates except self. | Send Da Btch opens15s Shadow Assist:50% ally-hit trigger, one active clone, off-field support, existing base damage. | Attack3/Capture applies Captured10s: DEF-40%. |
| 2 | Crit buffs become CR31/CD64. | Shadow damage grants3AE, once per shadow attack. | Captured also RES-40%; incoming DOT/Additional+40%. |
| 3 | Crit buffs include self; selfDEF+40%15s. | Assist duration becomes30s total. | Attack3/Capture hitting an already Captured target opens10s Mastery EndField follow-up. |
| 4 | Capture hits have40% Stun chance for2.5s. | AllyATK+500% ownDEF, capped900,20s. | Mastery grants5AE and converts its damage delivery to Additional. |
| 5 | Capture summons Len Focus10s, nonstacking. | Assist chance100%, damage budget650% ownDEF. | Own Captured-target hit reduces SkillCD0.5s and adds70%ownDEF Additional; shared1.5s internal cooldown. |
| 6 | Skill/SkillEX adds350%ownDEF Additional; Captured targets trigger1000%ownDEF Additional splash. | SelfADI+100%20s. | Own Captured-target hits add3%ATK/DEF for3s, max30%. |

Len Focus uses the user-approved starting behavior: one target per second, cycling through living scene enemies, dealing100% Camera Girl DEF. Its ally aura is team-wide: SPD+1 and dash cooldown halved. The icon follows the active character while the timer and source stats remain Camera Girl's. Recast refreshes one effect.

Mastery reuses the authored normal combo step playing cg_attack6, including timing, both hitboxes, motion, audio, events and the owning action formula. It uses the existing Skill input and consumes its follow-up tag. Captured's initial application does not satisfy the already-marked follow-up condition.

Human feel review: check lens/mark readability, follow-up response, stun feedback and Len Focus cadence. The splash starts with the shared explosion default radius of3 units; tune this only if playtest shows unclear or excessive reach. No additional balance values were inferred for Shadow Attack's base damage.
