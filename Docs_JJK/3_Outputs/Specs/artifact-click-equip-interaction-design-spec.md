---
slug: "artifact-click-equip-interaction"
status: approved
source: manual
gdd_tags:
  - "ui"
  - "items"
owner: "game-design-agent"
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# Artifact Click-to-Equip Design Spec

## Interaction Rules

1. In Safe Zone Team Builder, left-clicking a writable Artifact slot makes it the sole destination and opens the Artifact grid.
2. The destination remains visibly highlighted while the player searches, sorts, filters, or enters/exits Favourite Mode.
3. In normal mode, clicking an Artifact card equips/replaces at the destination and closes the inventory.
4. In Favourite Mode, the card click only toggles Favourite; it does not equip or clear the destination.
5. With no destination, a generic Artifact-grid card click retains preview behavior.
6. Right-click and equipped-slot drag rules remain unchanged.

## Interaction State

| State | Entry | Exit / Interrupt |
| --- | --- | --- |
| No destination | Default, close, combat entry, category change, successful equip | Left-click writable Artifact slot |
| Destination selected | Left-click Artifact slot on a character Team slot | Equip, close, combat entry, Character grid selection, equipped-slot drag, or selecting another Artifact slot |
| Favourite Mode | Existing Favourite button | Existing exit; destination selection persists |
| Equipped-slot drag | Begin drag on a filled equipped slot | Valid slot drop swaps; outside drop removes; normal drag cancellation restores the icon |

## Five-Component Check

- **Clarity:** the exact destination slot stays highlighted before resolution.
- **Response:** one click selects a destination and one click equips; closing or changing category cancels safely.
- **Satisfaction:** the slot icon and recalculated Team stats update together through the existing draft feedback.
- **Fit:** matches the established Character inventory interaction.
- **Motivation:** preserves fast comparison through Artifact filters and Favourite Mode.

## Edge Cases

- Empty Character Team slot: Artifact destination selection is rejected.
- Read-only/combat slot: selection and mutation are rejected.
- Already-equipped Artifact: existing relocation/replacement behavior is preserved.
- Same Artifact and destination: resolves through the existing equip routine without duplicate ownership.
- Panel closes externally: highlight and pending destination are cleared.
- Beginning an equipped-slot drag cancels the pending click destination before drag resolution.
