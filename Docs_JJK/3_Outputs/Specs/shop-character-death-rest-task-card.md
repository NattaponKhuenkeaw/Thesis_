---
slug: shop-character-death-rest
status: needs-human
source: manual
gdd_tags: [core-loop, characters, progression]
owner: agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Character ownership, death, and Rest

## Player-facing goal and source
User request, 2026-11-10: characters are purchased once while owned. Death retains run ownership, disables deployment and active switching, and Rest revives dead owned characters at 30% Max HP. A Character shop list becomes an Artifact offer when all shop-eligible characters are owned.

## Route and scope
Workflow: `/refactor`, with the behavioral changes explicitly requested above. Preserve the existing inventory, deployed-team, stat, acquisition, save and presentation authorities; no new architectural pattern or dependency. The user's request authorizes this scope. Merge/publishing and subjective playtest approval remain human checkpoints.

Affected: CharacterInventoryState, ShopManager/ShopItemListUI, TeamLoader, Team Builder character availability, existing regression tests. Preserve unrelated camera and scene changes. Full run-loss/lobby reset boundaries and character selling remain existing behavior.

## Acceptance criteria
- Death keeps the owned CharacterItem and RunCharacterState, at zero HP with a persisted death marker; removes deployment and releases equipment as before.
- Generic healing and purchasing cannot bypass death; Rest revives every owned dead character at exactly 30% Max HP, without auto-equipping them.
- Living deployed teammates retain the authored additive Rest heal and cap at Max HP.
- Owned characters, including dead and benched characters, never roll or purchase again regardless of progression or the legacy owned-filter flag.
- Exhausted Character offer lists use the normal Artifact roll, price, quantity and display paths, including rerolls.
- Regression coverage includes Main/ally, full wipe guards, save restoration, stale offers and Rest redeployment.

## State and experience checks
Alive -> Dead: lethal defeat; no purchase cost, zero HP/AE, clear transient effects, remove deployment. Dead -> Alive: explicit Rest only during an active run; 30% Max HP, same ownership, manually deployable again. Full wipe keeps the existing terminal run flow; Rest cannot bypass it. Rest's existing single-claim, input lock, readout and audio remain in force.

Clarity: inventory labels dead characters and the Rest result mentions revival. Motivation: character purchase survives individual defeat. Response: dead characters cannot enter CharSlots. Satisfaction/Fit: reuse existing death/switch and Rest visual/audio feedback. Numeric revival value comes directly from the user; no tuning proposal.

## Validation
Implemented and verified: 68/68 Unity EditMode tests passed in an isolated copy; scoped diff check passed. Manual gameplay/visual review remains; see `../TestPlans/shop-character-death-rest-test-plan.md`.

Automated checks plus manual smoke scenarios: buy last eligible character, reroll to Artifact; kill Main/ally, inspect owned dead card and blocked deployment; Rest once, verify 30% HP and re-equip; full wipe still routes to Lobby; retry stale duplicate purchases without currency/fragment changes. Stress repeated defeat/Rest and rerolls; observe whether dead vs living state and revival are understandable.
