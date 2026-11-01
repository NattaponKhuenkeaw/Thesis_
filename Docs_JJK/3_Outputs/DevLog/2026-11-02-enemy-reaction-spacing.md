# Enemy Reaction Spacing

## Investigation

The existing position path uses `HitFrameData.useSnapToAnchor` and `snapOffset` to target the active hitbox center plus an authored offset during impact freeze. Reaction movement after that phase can still be derived from `horizontalSpeed` and active frames.

An initial cap on spacing travel was withdrawn after the requester clarified the intended model: combo reactions should be deterministic positions authored for each hit, not speed-derived displacement. No implementation remains from that cap.

## Blocker

Confirm how ordinary combo hits without an authored snap target should obtain their target position, and whether the target should be reached/held for the full reaction.
