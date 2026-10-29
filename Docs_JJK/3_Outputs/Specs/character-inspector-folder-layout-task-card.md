---
slug: character-inspector-folder-layout
status: approved
source: manual
gdd_tags: [ui, progression]
owner: implementation-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Character Inspector identity card / folder layout

## Authorized request
2026-10-29: implement the supplied Character Inspector reference with existing game assets, an identity-card portrait and folder/drawer-style information layout. Retain the earlier instruction that decorative sprites come only from `Assets/Project/UI`. Character portraits/ability icons remain content from their existing character definitions; retain existing font assets. User approves this UI implementation; visual feel review remains after rendering.

Workflow: `/implement-feature`. GDD `@tag: ui` Character Inspector and `@tag:progression` Potential define the existing data and navigation. Reuse current InspectScreenUI/sub-panel/RunInventory/MetaProgression authorities and ADR-005/007/009; no architecture, dependency, build, save-schema or balance changes.

## Design and acceptance
- Left vertical selectable character rail with a yellow selected frame; identity card with a large portrait, real name/role and existing city background.
- INFO and SKILLS folder tabs above a framed document area; active tab is yellow and readable.
- INFO displays the shared run level, primary/secondary stats, current Potential total with twelve segments and Upgrade Potential. Values come from current systems, not the mockup numbers.
- Equipment remains reachable from INFO and retains the selected character's artifact information. Potential retains its existing full-screen workflow and upgrade validation.
- Selecting characters updates portrait, statistics, skills and Potential consistently; selected rail entry stays highlighted, rail does not reset or duplicate entries.
- Folder changes have a short unscaled drawer slide/fade; close/back and Safe Zone locking remain functional. No actual content scale animations.
- New layout is authored in TeamCanvas, with dedicated rail/skill templates. Existing Potential panel and TeamBuilder inventory remain intact.
- Render Info, Skills, Equipment, empty state, alternate character and 4:3; verify selected state, references, all stat bindings and Potential route.

Starting values: .18 s folder transition, 20-unit drawer offset, twelve equal Potential indicators (existing three tracks x four levels). Test rapid tab/character switching at paused time: final panel visible, no alpha/position drift, no scale changes. Shorten the duration if the tabs feel delayed; widen text areas or scroll if descriptions clip.

## Experience checks
Clarity: identity on left, labelled stat rows and active folder on right. Response: immediate data selection with optional presentation motion. Motivation: existing build/Potential decisions. Satisfaction/Fit: existing dark/pixel city theme and white overlay feedback. No new gameplay state or economy.
