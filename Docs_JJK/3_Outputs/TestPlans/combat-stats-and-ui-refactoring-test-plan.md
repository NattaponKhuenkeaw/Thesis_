# Test Plan: Combat Stats & UI Refactoring

## 1. Test Plan Summary
| Test Area | Priority | Type | Platform |
|-----------|----------|------|----------|
| Damage Math (Outgoing) | P0 | Manual / Automated | PC |
| Damage Math (Incoming) | P0 | Manual / Automated | PC |
| UI Mode Switch | P0 | Manual | PC |
| Inspect Screen | P1 | Manual | PC |

## 2. Functional Tests
```
TEST: Player Critical Hit
GIVEN: Player attacks an enemy
WHEN: The attack rolls a critical hit based on CharacterStats.critRate
THEN: The damage should be multiplied by CharacterStats.critDamage and HitReactionData.isCriticalHit should be true
PRIORITY: P0

TEST: Enemy Defense Mitigation
GIVEN: An enemy with DEF > 0
WHEN: The player attacks the enemy
THEN: The enemy's received damage should be properly mitigated according to CombatFormula (Damage * (100 / (100 + DEF)))
PRIORITY: P0

TEST: UI Combat Zone Mode
GIVEN: The game is in the CombatZone
WHEN: The player opens the TeamBuilderUI
THEN: Time should freeze, audio should duck, the grid panel should be hidden, and dragging characters should be locked
PRIORITY: P0

TEST: Inspect Screen Open
GIVEN: The player is in the CombatZone
WHEN: The player clicks on a Team Slot
THEN: The Inspect Screen should appear showing the correct stats (e.g. integer SPD) and the character should not be made active
PRIORITY: P1
```

## 3. Edge Case Tests
- [ ] What happens if a character has 100% Damage Resistance? (Should clamp or reduce to 1 damage minimum)
- [ ] What happens if the player rapidly clicks the Team Slot to open the Inspect Screen?
- [ ] What happens if the Team Builder UI is closed while the Inspect Screen is still open? (Should close the Inspect Screen gracefully)
- [ ] What happens if the `CharacterStats` or `CombatFormula` encounter negative values for base damage or multipliers?

## 4. Performance Tests
- [ ] No GC allocations during damage calculation in `CombatFormula`?
- [ ] Memory usage within budget when instantiating the Inspect Screen UI elements?

## 5. Platform-Specific Tests
| Test | Primary Platform | Secondary Platform |
|------|-----------------|-------------------|
| Open Inspect Screen | Click with Mouse on PC | Tap with finger on Mobile |

## 6. Regression Checklist
_Systems that could break due to this change:_
- [ ] `PlayerCombat` — Verify that hitstop and knockback still function correctly since damage logic changed.
- [ ] `PlayerMovement` — Verify that `speedOverride` correctly drives `PlayerMovement.moveSpeed` and doesn't break jumping.
- [ ] `EnemyController` — Verify that enemies still die when HP reaches 0 and don't enter an invalid state.
