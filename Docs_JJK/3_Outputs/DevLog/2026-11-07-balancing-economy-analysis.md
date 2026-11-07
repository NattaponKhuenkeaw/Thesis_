# 2026-11-07 — Balancing and economy analysis

Analyzed current enemy curves, owner-supplied zero-defense training-dummy DPS, Level-room cadence, room quotas, route rewards, and artifact rarity/prices under the owner's no-selling economy constraint.

- Saved the evidence, calculations, assumptions, and provisional playtest targets in [balancing-economy-analysis-design-spec.md](../Specs/balancing-economy-analysis-design-spec.md).
- Identified exponential normal HP versus linear boss HP, slow Level-event progression, team power spread, unequal chamber workload for equal rewards, and the risk of Hard rewards providing neither a usable upgrade nor direct Soul.
- Kept the proposed 36-node Potential system as future scope. Its persistence, per-character scope, and reward cadence remain undecided.
- Used the current working-tree data, including existing user edits; changed no gameplay files. No Unity playtests or automated tests were run.
- Intended run duration and biome-to-character-level benchmark mapping remain unknown. Proposed numerical targets are starting values for human playtesting, not approved tuning.
- Incorporated owner clarification: room/boss duration observations cover biomes 1–10, further biomes were not played because progression and instant boss kills were unenjoyable, and dummy DPS tests lasted approximately 30 seconds. Reframed later-biome numbers as projections and separated opening burst from window-averaged DPS. Added a first-ten-biome pacing scenario without assuming this is the intended run endpoint.
- Removed resale calculations following the owner's plan to remove artifact/fragment selling and its UI. Economy analysis now uses direct Soul income and actual usable upgrades separately; no gameplay removal was performed as part of this analysis.
- Recorded the clarified roughly 1,200 DPS Lv.30/full-SSR/zero-Potential benchmark, the 10k–20k max-investment goal, permanent Potential, and owner control of character DPS tuning.
- Saved [the recovery proposal](../Specs/balance-economy-recovery-design-spec.md): a provisional ten-biome chapter arc, faster guaranteed leveling, separate Normal/Nightmare reference bands, authored boss health milestones, slower normal health growth, whole-room quotas, guaranteed Soul, staged drafts, and shop price budgets. All new numbers are starting values with validation and adjustment rules; no gameplay implementation or playtests were performed.
