---
slug: "artifact-random-roll-inventory-regression"
status: implementation-validation
source: "artifact-random-roll-inventory-task-card"
date: 2026-08-17
---

# Artifact Random Roll Inventory Regression

## Automated EditMode Coverage

- [x] Exact 1/2/3/4-line thresholds implement 50% / 15% / 20% / 15% outcomes.
- [x] Generated rolls remain inside every configured inclusive range.
- [x] Amount rolls use the approved 35% / 30% / 20% / 10% / 5% quality-band thresholds.
- [x] Same-stat lines can occur on one Artifact.
- [x] Same-definition acquisitions keep distinct instance IDs.
- [x] Flat and two-decimal percentage rolls aggregate from clean base stats.
- [x] Imported rolls have unavailable quality.
- [x] All nine authored Artifact assets aggregate identically before and after legacy import.
- [x] Query filtering and quality sorting retain duplicate instances.
- [x] Deployed snapshots do not track mutable inventory roll lists.
- [x] Equipped instances cannot be removed while unequipped sibling copies can.
- [x] Final damage rounds once after decimal calculations.

## Static Validation

- [x] Runtime assembly compiles with Unity's Roslyn response file.
- [x] Editor/test assembly compiles with Unity's Roslyn response file.
- [x] New scripts include Unity `.meta` files.
- [x] ADR-009 and GDD Artifact/Soul Shop/progression rules are updated.

## Unity Editor Validation

- [x] Run `Tools > Project > Artifacts > Dry-Run Legacy Migration Report` and confirm nine definitions.
- [x] Run all EditMode tests and capture the Unity Test Runner result.
- [ ] Acquire two copies of one Artifact and verify independent rolls, IDs, New state, and numeric quality.
- [ ] Enter Favourite Mode; verify click toggles Favourite and drag/preview are suppressed.
- [ ] Verify search, line-count/stat/favourite/equipped/new filters and every sort mode.
- [ ] Equip, move, Deploy, close/reopen Team Builder, reload the run save, and verify instance identity persists.
- [ ] Verify runtime stats equal preview/Inspector values for flat and percentage HP/ATK/DEF plus CM/CR/CD/AER/DM/DI/ADI.
- [ ] Sell one unequipped duplicate by instance; verify equipped and sibling copies remain.
- [ ] Verify Artifact duplicates never create fragments and Character duplicates still do.
- [ ] Confirm no Artifact rarity/star/transcendence labels remain in the active UI.
- [ ] Run representative combat and Training Debug checks; confirm integer damage and no per-hit GC allocation regression.

## Validation Evidence

- Unity 6000.3.4f1 isolated EditMode run: **50/50 passed**, zero failures, zero skipped.
- Final test result: `C:/Users/Apricha/AppData/Local/Temp/Thesis_ArtifactV3_UnityValidation/artifact-v3-results-final2.xml`.
- Dry-run report: **9 Artifact definitions**, all legacy stat values imported exactly, zero owned-item references requiring manual cleanup, zero retired Artifact fragment stacks, Character Potential unchanged.
- The remaining unchecked items require an interactive visual/PlayMode pass in the project Editor.
