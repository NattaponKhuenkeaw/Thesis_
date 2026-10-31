# 2026-10-31 — Timed-opportunity HUD proposal

Apricha requested shared slower glowing procedural outlines for QTE and action
time windows, removal of ability follow-up fill overlays, and a two-character
standby-only combat slot with portrait exchange motion.

Inspected current AbilityUIManager, CharacterSwitchSlot and TeamSwitchHUD; the
approved action-window-icons baseline; relevant GDD ui/combat text; project routing
and implement-feature contracts. Reviewed the supplied eight-second Honkai clip
via sampled frames and a portrait-detail sequence around its swap. Looked up the
Honkai QTE reference as requested.

Created timed-opportunity-ui task card and design spec with concrete starting
values and validation scenarios. Supplied a schematic motion preview for review.
Runtime scripts, prefab contents and gameplay behavior were not changed.
Apricha subsequently approved the design with "LGTM" on 2026-10-31. Recorded
approval in the design/task artifacts and prepared timed-opportunity-ui-arch-plan.md.
The plan keeps gameplay ownership intact, introduces one canvas-compatible
procedural Graphic and reuses inactive slot views with portrait-only animation.
Architecture approval is now pending per the separate workflow checkpoint.
Existing working tree changes were preserved. No compile/tests claimed for a
design-only session. Reference frame extraction succeeded with local OpenCV.
