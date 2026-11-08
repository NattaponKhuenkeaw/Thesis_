# Artifact reward card layout

## Scrollable details follow-up

Each card now keeps its header/icon fixed and scrolls passive text plus stat rows inside a RectMask2D viewport. Passive text grows to its preferred height rather than shrinking into a fixed box. Wheel, drag and the overflow scrollbar control only that card's details. Card navigation retains left/right neighbors; Down enters the scrollbar and Up returns to the card. Every new draw stops inertia and resets details to the top.

Validation: all **8 targeted ArtifactUiPolishTests passed**, including long text overflowing the viewport, wheel scrolling without a claim, unchanged fixed icon placement, and reset/reuse without duplicate scroll areas. Existing two-click selection/retry tests also passed. Updated 4:3 capture inspected. This is editor regression evidence; live controller and drag feel remain available for human review.

[Scrollable card layout at 4:3](reward-card-scroll-4x3.png) · [UI test results](reward-card-scroll-tests.xml)

Reward cards now instantiate the same `TeamInventoryStatRow` / `StatItemUI` used in Artifact Preview. Fixed passive bonuses and visible rolled lines share its label/value columns, stat formatting and width fitting. Five rows fit inside the authored 153-unit stat area. Lore remains hidden.

Each icon has a rarity-colored frame. Selecting raises the card and scales to 1.08; claiming scales to 1.18 while sliding upward, then fades out. These are starting values for visual review; reduce scale if neighboring cards or the header overlap. Motion retains unscaled time and existing two-click commit protection.

Validation: Unity compiled the changes. Artifact inventory fixture: 43 passed, 1 failed. The failure is the legacy migration fixture expecting nine assets in the removed `Artifacts/Phase1` folder; reward picker confirmation passed. The visual capture helper was updated to use the current SSR asset path and filter world objects from its camera. Rendered card stats and icon frames were inspected. Other UI from loaded scenes may still appear outside the cards in captures.

[Reward cards](reward-card-layout.png)
