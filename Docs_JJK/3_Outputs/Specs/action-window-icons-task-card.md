---
slug: action-window-icons
status: approved
source: manual
gdd_tags: [ui, combat, platform-input]
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Resolver action icons on cooldown slots

Workflow: `/implement-feature`. Source: Apricha's direct request on 2026-10-30.
Player goal: the NA / Skill / Ult / Dash panel communicates the authored action
currently offered, including temporary action windows.

GDD @tag:ui: "The active HUD shows current HP, Skill cooldown, Ultimate cooldown,
Aura Energy readiness, and any Potential-created follow-up window."
@tag:combat and @tag:platform-input govern context, availability and input routes.
Per-action icons are requested here and are not yet documented as implemented GDD behavior.

Inspect: ComboNode.cs (CombatAction), ActionResolver.cs, PlayerCharacter.cs,
Player/UI/AbilityUIManager.cs, Editor/UI/PropertyCardBuilder.cs, Character/DashData.cs,
Prefab/CombatUI/Player_UI.prefab and the existing AbilityUIFeedbackTests.cs.

Acceptance proposed for approval:
- Author an optional Sprite for each resolver action in the existing action authoring UI.
- NA / Skill / Ult show the matching action's icon according to their existing input route.
- Ordinary icons follow the current resolver selection only when its Ground/Air and
  action conditions match.
- Temporary actions replace the corresponding slot icon during their availability window;
  closing or consuming the window restores the ordinary icon.
- A replacement appears only during its timed window and while the resolver matches
  it for the current Ground/Air and input conditions.
- Team swap portraits use CharacterMetaData.icon, falling back to its portrait.
- Resolver priority, Ground/Air conditions, counters, cooldown and AE remain authoritative.
- Unassigned action art falls back to the slot's original prefab art, including after swaps.
- Dash keeps movement-owned availability/cooldown and gets an optional icon in DashData,
  authored on CharacterObject and displayed in the existing Dash slot.
- No gameplay category, Action Type, AE cost, timer or input changes.
- Cover priority, window expiry, swaps, missing art, cooldown, AE and Ground/Air cases.

Out of scope: producing new artwork, redesigning the HUD and changing the withdrawn
unified-action-catalog proposal. Preserve existing local asset and script edits.

Design and architecture approved by Apricha in chat on 2026-10-30; implemented.
Added optional action icon authoring, resolver-driven NA/Skill/Ult pictures and
window cues, cached prefab fallback and Main's existing Shin art assignments.
NA uses existing prefab image/overlay references. Main's Dash icon is authored in
DashData and rendered through the existing Dash image.
Review: `action-window-icons-review.md`. Verification: `Reports/ActionWindowIcons/`.
Manual input/feel sign-off remains with the requester.

Follow-up requested by Apricha on 2026-10-31: play-mode fallback icons were null on
the live NA/Skill/Ult Image components because their serialized references used a
deleted Sprite asset GUID. Point them and Main's ordinary Ground/Air actions at the
current Basic icon asset. Ordinary ActionResolver icons remain visible when their
current context and conditions match; temporary window cues still use live windows.
