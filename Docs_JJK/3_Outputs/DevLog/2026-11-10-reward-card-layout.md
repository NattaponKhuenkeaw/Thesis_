---
slug: reward-card-layout
status: approved
gdd_tags:
  - ui
  - items
  - progression
owner: implementation-agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Reward card layout

The Artifact picker used to insert the passive before the stat rows and look for the rarity stars directly under the card. The Memory picker also repositioned the labels independently. Both reward types now use the prefab's title, dividers and framed icon, with wrapping details in a shared scroll area. Artifact values and stat names use the same label, and the nested `RarityPanel` centers the stars. Memory shows rank or track progress before the effect. Short effects sit toward the bottom to match the supplied reference.

The Unity preview checks covered mixed-rarity Artifacts at 1920×1080 and 1024×768, plus both major and centered generic Memory nodes at 1920×1080. The isolated editor checks exercised long Artifact and Memory descriptions and checked that the portrait chooser still uses character icons. All six Artifact UI tests passed. The inventory suite passed 43 of 44 tests. `AllNineAuthoredArtifactsMigrateWithoutChangingStats` still searches the removed `Artifacts/Phase1` folder and found zero assets.

## Follow-up layout correction

The compact stat row prefab is back on Artifact rewards with names on the left and colored values on the right. The Artifact Preview Panel keeps its Stats container beside the icon and gives the passive label its own viewport beneath the rows. Major Memory nodes place a smaller rank label at the bottom center. Generic Memory nodes show the effect directly below the divider.

All six Artifact UI tests passed, including the five-row preview layout check. Unity captures verified the compact rows and both Memory layouts.
