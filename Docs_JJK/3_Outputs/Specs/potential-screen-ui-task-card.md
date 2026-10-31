---
slug: potential-screen-ui
status: ready-for-review
source: manual
gdd_tags: [ui, progression]
owner: codex
human_checkpoint: satisfied-by-user-prompt
next_agent: human
blocked_by: []
---

# Potential Screen UI

## Approved scope
Refactor Potential presentation using `Assets/Project/UI` and character Potential icons. Preserve the earlier scrollable node-board layout and the right-side upgrade preview. Remove the character/profile section. Level I starts at the bottom, then II, III, IV upward. Blank background; no node-state legend or fragment currency display. Existing sequential unlock and fragment spending rules remain unchanged.

Workflow: `/refactor`. GDD: `@tag:ui` Character Potential Panel and `@tag:progression` Permanent Potential Upgrade. User prompt and follow-up authorize the visual scope; no architecture or dependencies change.

## Responsibility / impact
| Responsibility | Before | After |
| --- | --- | --- |
| State and upgrade eligibility | CharacterPotentialPanel / CharacterProgressionState | Same authorities |
| Node rendering | Flat tinted images | Authored frames, track icons, state labels, selection outline |
| Node layout | Existing authored board | Existing groups and positions, ordered upward, scrollable |
| Upgrade preview | Text and confirm | Right-side asset frame, icon, wrapped scrollable description, close/cancel |

Files: Potential panel/node/window scripts, TeamCanvas Potential subtree, scoped editor authoring/capture helper and focused tests. Preserve pre-existing uncommitted work; no save, balance, scene, build, dependency, input configuration, merge or publishing changes.

## Acceptance / verification
- [x] Twelve nodes retain track identity, use existing artwork and state markers.
- [x] No character/profile section, fragment display, state legend, or background illustration.
- [x] Level I at bottom; subsequent levels rise upward.
- [x] Board retains ScrollRect, viewport and draggable content.
- [x] Right-side preview preserves description and upgrade eligibility; close/cancel clear selection.
- [x] Five focused EditMode UI regression checks pass.
- [x] Unity compiles without errors; render previews at 16:9 and 4:3.
- [ ] Human visual/game-feel review.

Design filter: clarity through track headings, levels and state labels; response through existing selection/upgrade and scroll controls; satisfaction through existing upgrade feedback; fit through project pixel artwork; motivation through permanent upgrades. No balance values change. Layout sizes are starting values inspected in both aspect ratios.

Broader regression note: an existing CameraGirlPotentialTests skill-data expectation fails (expected 33, authored value 43). That unrelated data/test was not changed. Focused UI results and screenshots: `Docs_JJK/3_Outputs/Reports/PotentialScreen/`.
