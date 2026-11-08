---
slug: artifact-ui-polish
status: needs-human
source: manual
gdd_tags: [ui, items, progression]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Artifact UI polish

## 2026-11-07 slot and ownership follow-up
Scrollable reward details: keep the icon/name/rarity fixed, place passive and stat rows in a clipped vertical ScrollRect, size passive text naturally without shrinking, reveal an overflow scrollbar, preserve two-click confirmation and neighboring-card navigation, and reset scrolling on each new draw. Wheel/drag/scrollbar interactions must not claim a reward. Targeted regression covers long text, actual scrolling without a claim, and reuse/reset without duplicate scroll areas.
Reward-card follow-up: use the same StatItemUI prefab, fixed bonus inclusion, visibility filtering, formatting and bounded columns as Artifact Preview; show a rarity-colored icon frame; enlarge the chosen card while sliding upward on pickup. Starting scales: selection 1.08, pickup 1.18. Review maximum five stat lines at 16:9 and 4:3, and check enlarged cards do not touch the header. Existing two-click confirmation and hidden Lore remain in effect. This uses the existing `/fix-bug` workflow because the requested UI changes alter behavior and presentation.
Additional correction: remove the legacy sprite selection frame, keeping the light-yellow outer glow. Empty character slots hide equipment, stat bounds and Change Character; clicking the empty slot invokes the existing Change Character flow. Filling a slot restores its content, while combat locking still blocks edits.
Authorized by the user's direct request; workflow remains `/fix-bug`.
- A brighter destination border gently pulses using unscaled time and never intercepts input.
- Clicking an occupied slot opens/previews its equipped copy. Repeated clicks and switching slots keep the artifact inventory open, retaining cells, scroll and query state.
- Equipped inventory cards retain rarity color at full opacity and show their character owner portrait at the top right. Favourite badges move below the portrait to avoid overlap; missing portraits retain the Equipped text fallback.
- Starting values: 1.2-second pulse period, 6% border expansion, 44-unit owner badge. Human check: immediately identify the selected slot and owner at 1080p and 4:3 without obscuring artifact art. Reduce expansion if adjacent slots overlap; increase badge size only if the owner is unreadable.
- Extend the existing prefab validation with occupied-slot preview/reopen, repeat clicks, stable cell/click/scroll state, owner portrait/input safety, preserved rarity tint, and pulse phase checks. Preview scenes keep the user's current scene intact.

## Player-facing goal and source
Implement the user's 2026-11-06 UI corrections and requested two-click artifact reward selection. The prompt authorizes implementation; final visual/game-feel review remains human-owned.

## Router decision
Workflow: `/fix-bug`. Use the existing UI controllers, prefab references, reward seed, rarity weights, and roll generator. No new architecture or dependency is needed. Context: `project-stack.md`, GDD `@tag: ui`, `@tag:items`, and `@tag:progression`.

## Scope and acceptance
- Repeated discard/cancel/escape cycles leave no invisible modal blocking input.
- Artifact passive/description rich text renders in preview and reward cards.
- Compact preview stat labels and values remain inside the row/panel.
- Rarity sorting works in both directions with deterministic tie-breaking.
- Remove the inventory Favorite toolbar button; preserve copy-level Favorite in the context menu. This is an interpretation of the user's note about the button still existing.
- Equipping leaves inventory open and retains the selected destination; slot selection uses an overlay instead of an inventory slot header.
- Passive Potential details appear on the left; other tracks restore the original placement.
- Navigation Settings opens settings from gameplay.
- Reward cards show rarity-colored backgrounds. First click raises/selects; another card changes selection; second click on the selected card commits once and animates the pickup/exit. Remove the Claim button.
- Draw rarity independently per card using existing biome R/SR weights; no duplicate definitions within one pull; keep SSR policy unchanged.

## Interaction and design checks
Clarity: selected overlay, raised card and an explicit second-click hint. Motivation: compare actual rolled rewards before claiming. Response: change selection freely before confirmation. Satisfaction: pickup motion plus existing confirm audio. Fit: retain existing UI art, rarity colors and reveal behavior.

Reward state: reveal -> choosing -> selected -> committing -> picked/exit. Commit failure returns to selected; pause gates input; disabling stops animation. No inventory mutation on first selection. Repeated confirmation cannot grant multiple copies.

Starting animation values: selection lift 28 UI units over 0.12 seconds, pickup 100 units over 0.2 seconds. Test: first click clearly selects without claiming, second click visibly picks up once at timeScale 0. Reduce lift if it overlaps the header; increase duration only if observers miss confirmation. Human visual approval is pending.

## Validation
Targeted Unity tests for modal lifecycle, mixed rarity draws/seed repeatability, sort order, two-click claim/idempotence, layout bounds, Potential placement and Settings wiring. Inspect rendered UI at 16:9 and 4:3 where the available editor permits. Preserve pre-existing unrelated changes. No merge, publishing, build/dependency/configuration changes.

Implementation and verification are complete; see `../Reports/ArtifactUiPolish/validation.md`. Pending checkpoint: human approval of the final visuals/game feel. The main project still has the unrelated shop-test compilation blocker described in that report.
