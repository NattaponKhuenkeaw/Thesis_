---
slug: teambuilder-inventory-layout
status: needs-human
source: manual
gdd_tags: [ui, items]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# TeamBuilder inventory layout

## Goal and authorized scope
User request on 2026-10-29: open the Character inventory through a **Change Character** button. Both inventory categories follow the supplied dark/yellow references: scrollable grid above, item preview below. Previews are inactive until the player selects an item.

User clarification: decorative UI art must come exclusively from `Assets/Project/UI/`, including its `MainMenu/` assets. Preserve existing text-font and gameplay character/artifact content references; add no decorative sprites from other asset folders.

Workflow: `/implement-feature` because the requested UI refactor changes selection behavior. GDD: `@tag: ui`, Team Builder & Team Lookup Flow, and `@tag:items`. Existing inventory/draft/deployment authorities remain in place (ADR-009). No dependency, build, scene, or save-schema changes.

## Acceptance
- Each editable team slot has a Change Character button; clicking the portrait does not open character selection.
- Both inventories have three cards per row (user's final correction), vertical scrolling for additional rows, and an inline preview below.
- Inventory opening/closing uses an unscaled slide and fade; team slots slide right into a reserved area and return on exit without scaling.
- Shared pulse and hover feedback animate a white overlay with ease-out expansion/fade; actual content retains its scale. TeamBuilder placement/stat feedback uses this effect.
- Opening, closing, changing category or destination clears selection and hides previews.
- Clicking a character or artifact previews it without immediately changing the team; explicit Assign/Equip confirms the selection.
- Current/in-team/equipped states remain visible; selecting the current character cannot duplicate it.
- Artifact Favourite, search and sorting remain available; combat editing remains locked.
- Existing team drag/swap/removal and inspector entry remain intact.

## Design and implementation boundary
The user's request and images authorize the layout and interaction change above. Existing panel/controller responsibilities remain: TeamBuilderUI owns destination/draft routing; each grid owns its selected card; preview components display the selected data. UI-only prefab authoring is isolated to TeamCanvas and dedicated inventory-card prefabs. Final visual feel remains for human review; no merge or team publishing is authorized.

## Experience checks
Clarity: visible destination and selected card, disabled Current Character action. Response: one click previews; explicit action applies. Motivation: preview supports loadout choice. Satisfaction/Fit: existing portraits, dark panels and yellow focus feedback. No new gameplay numbers, effects or audio.

## Validation
Automated checks cover authored layout/references, default-hidden previews, select-before-assign and reset on reopen/category/destination changes. Visual checks cover the two categories and a smaller viewport. Manual checks include wheel scroll, rapid selections, assigned-character swap, Favourite mode, closing and reopening, and combat lock.

Implementation complete: 66 isolated layout/selection/feedback assertions and 76 Shadow Meter/ability-feedback regression tests pass. Final rendered previews cover 16:9 and 4:3 with no inventory/slot overlap. Human review remains for Play Mode transition timing and rapid reversal feel.

Starting values: .22 s unscaled slide/fade, 24-unit panel gutter and three equal card widths. Check that opening/closing and rapid reversal settle without overlap or scale drift at 16:9 and 4:3. Increase the gutter if frames touch; reduce duration if the transition delays selection. White overlay expansion uses the Shadow Meter quadratic ease-out and never changes the content transform.
