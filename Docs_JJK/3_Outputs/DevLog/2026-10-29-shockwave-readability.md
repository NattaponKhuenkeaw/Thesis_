# 2026-10-29 — Shockwave readability

User requested larger and longer Shockwave feedback across every VFX using it. See `../Specs/shockwave-readability-task-card.md` for scope, starting values, and visual acceptance criteria.

Updated ArtifactAreaVisual and all four affected prefabs with 1.5x decorative wave size, 2x expansion/fade duration, and stronger alpha. Preserved hit geometry and the existing attack ring timing. One-shot cleanup now accounts for the separate wave tail. Added focused EditMode regression coverage and updated the prior blood-burst lifetime assertion.

Scoped `git diff --check` passes. Unity execution and in-game visual review remain unverified. No CI, dependency, build, or gameplay configuration changed.

Owner follow-up: increased Shockwave to 2.5x its original size (150% larger), retaining the extended duration. Enlarged Dimension Slash judgment and final-cut ribbons by 50% in width and height through overallScale 1.3 -> 1.95 in saved action data and authoring defaults. Updated relevant regression expectations; visual review and Unity execution remain pending.
