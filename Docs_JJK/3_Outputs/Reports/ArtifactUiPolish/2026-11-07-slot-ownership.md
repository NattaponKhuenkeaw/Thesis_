# Artifact slot and owner feedback — 2026-11-07

Implemented the requested follow-up in the existing artifact UI controllers and both artifact cell prefabs.

- Selected destinations have a solid bright outer border with a gentle unscaled-time pulse. All overlay graphics ignore raycasts.
- Repeated slot clicks keep inventory open. Changing destinations updates only selection/preview; occupied slots display their own copy, empty slots clear the preview. Existing grid cells, query and scroll are retained.
- Equipped cells keep rarity tint and full opacity. A top-right character portrait identifies the owner; tall art is cropped without stretching. Favourite sits below the owner portrait. Missing portraits use the existing Equipped text fallback.
- Both authored cell prefabs contain the owner badge; the authoring utility preserves it when rebuilding the UI.

Validation: Unity compiled successfully. `Tools/Neo-JJK/UI/Validate TeamBuilder Inventory Layout` passed **88 assertions**, including repeat clicks, stable cell/click/scroll state, occupied-slot preview/reopen, owner icon/input safety, transfer/unequip, rarity tint, selected-card glow and pulse phase changes. Rendered captures were inspected at 1280×720 and 1024×768. Preview scenes were used without replacing or saving the user's active scene. The existing fixed-width team cards remain clipped on the right at 4:3.

The pulse is configured for unscaled time; its phase changes were checked programmatically. Final animation feel and controller interaction remain on the human regression checklist.

[16:9 capture](slot-owner-16x9.png) · [4:3 capture](slot-owner-4x3.png) · [Regression checklist](../../TestPlans/artifact-ui-polish-regression.md)
