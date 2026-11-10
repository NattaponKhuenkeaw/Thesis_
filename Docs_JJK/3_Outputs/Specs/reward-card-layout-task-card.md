---
slug: reward-card-layout
status: approved
source: manual
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

The supplied card references set the visual direction for the `/fix-bug` workflow. Artifact and Memory node cards share a title, header divider, framed icon, second divider, and details area. Artifact stats use compact rows with labels on the left and colored values on the right above the passive. The Artifact Preview Panel keeps its authored Stats container beside the icon and scrolls the passive in its own area. Major Memory nodes show a small centered rank at the bottom. Generic nodes display the effect without a rank footer. Artifact stars use the authored middle-centered `RarityPanel`. Memory character portraits keep their separate layout.

Players should be able to compare the reward values and effects before selecting a card. The card lift and confirmation flow from the GDD remain the interaction feedback. The shared prefab supplies the art and spacing, and room visits retain reward ownership under ADR-021.

Check mixed-rarity Artifact cards, long Artifact and Memory details, the three-choice and generic Memory steps, and the transition back to the portrait chooser. Inspect readability at 1920×1080 and 1024×768. The user supplied the layout direction and retains the final visual review.
