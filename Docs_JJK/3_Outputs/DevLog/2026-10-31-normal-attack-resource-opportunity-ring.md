# Normal Attack resource opportunity ring

`AbilityUIManager` now keeps the shared NA ring visible when the resolver selects
an available resource-backed standalone Normal action. This covers Main's cached
Dimension Cut after Dash; the existing resolver remains responsible for its
resource, context, target and action-condition checks. Timed-tag opportunities use
the existing path.

Added the manual regression step to the timed-opportunity UI test plan. Unity
compilation and runtime tests were not run for this change.
