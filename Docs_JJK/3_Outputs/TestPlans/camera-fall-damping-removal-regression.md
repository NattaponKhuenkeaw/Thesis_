---
slug: camera-fall-damping-removal
status: draft
source: manual
gdd_tags:
  - identity
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Camera Fall/Landing Regression

Automated/static verification completed on 2026-10-28:

- Unity 6000.3.4f1 Roslyn compilation of Assembly-CSharp using the existing editor response file succeeded (exit 0). Outputs were redirected to a unique temporary directory without replacing Unity's assemblies. Existing obsolete API, unused-field and other unrelated warnings remain.
- Removed fall/landing settings and APIs have no remaining C# / prefab / scene references under Assets.
- Scoped `git diff --check` passes. The camera prefab changes only remove the eight obsolete settings.

Manual scenarios below are **not run**. Use a gameplay scene with the camera prefab and room boundaries.

| Scenario | Expected result |
| --- | --- |
| Jump, reach apex, fall and land | Proxy Y matches player Y; no extra downward lead or upward recovery. Ordinary Cinemachine follow smoothing remains. |
| Long drop and repeated jumps | No velocity-dependent dip, damping switch or landing rebound. |
| Turn and move near both room walls | Horizontal look-ahead and confinement retain their current behavior. |
| Explore/combat transition and enemy tracking | Framing still changes normally; falling/landing does not change group damping. |
| Midair character swap and scene transition | Camera retargets the new body without retained fall-offset state. |
| Main QTE plunge and authored camera actions | Existing action-owned camera lock, descent tracking, return and impact feedback still operate. |

Final subjective camera feel requires the user's playtest.
