---
slug: teambuilder-inventory-layout
status: approved
gdd_tags: [ui, items]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# TeamBuilder inventory validation

Automated Editor checks: `Tools/Neo-JJK/UI/Validate TeamBuilder Inventory Layout` uses an isolated prefab preview scene and a temporary inventory. It checks three columns with clipped vertical scrolling, asset sources, initial-hidden previews, preview-before-assignment, current-character disabling, swap/equip routing, category/destination/reopen resets, combat editing lock, three artifact slots, active indicator, complete slot visibility at 16:9 and 4:3, white overlay input behavior, unchanged content scale, and inventory slide/fade poses.

Regression fixtures: `Tools/Neo-JJK/UI/Validate TeamBuilder Feedback Regression` runs `ShadowMeterTests` and `AbilityUIFeedbackTests` in the existing isolated-scene runner.

Manual gameplay review:
1. Open TeamBuilder in a Safe Zone. Use Change Character on each slot. Clicking the portrait must retain drag/inspect behavior.
2. With enough owned items, scroll past the first three-card row using the wheel and scrollbar. Select different cards rapidly; only the current selection stays highlighted.
3. Preview current/other characters and artifacts. Confirm using Assign/Swap/Equip; check team stats and artifact identity.
4. Close using the inventory X and click outside. Check the inventory slides/fades left and slots return to center. Reopen during closing; it must smoothly reverse without a stuck input blocker.
5. At paused time scale, verify transitions and white overlay feedback still finish. Actual cards, portraits, labels and their scale must stay steady.
6. Check Favourite mode, search and every sort option. Enter Combat Zone; inventory and editing buttons must disappear.
7. Review frames, portrait fit and preview readability at 16:9 and 4:3. Visual feel approval remains a human checkpoint.

Automated pose/render checks do not substitute for the rapid-reversal and timing review in Play Mode.
