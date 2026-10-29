# 2026-10-29 — QTE readiness and ordinary swap fallback

## Bugs Found

A ready QTE with no usable target or safe entry blocked an otherwise legal ordinary swap,
also showing the unavailable portrait cover. Restored the approved fallback in both FSM
eligibility and TeamLoader commitment. Valid QTE still takes priority; fallback respects
ordinary cooldown/state rules. Rejected requests retain readiness; accepted swaps clear it.

Added target-loss, blocked-entry/cooldown, portrait-click and hitstun regression checks.
Runtime and Editor compilation passed with isolated outputs; diff checks passed.
Unity tests and live playtest remain pending. See the task card and updated QTE test plan.
