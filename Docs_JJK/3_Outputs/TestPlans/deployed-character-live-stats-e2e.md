---
slug: deployed-character-live-stats
status: passed
source: manual
gdd_tags:
  - combat
  - characters
  - progression
owner: qa-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Deployed Character Live Stats E2E

## Automated E2E

`TeamRuntimeStatFeedbackTests.DeployedSource_E2E_UsesLiveOffFieldDamageAndSnapshotsAppliedBuff`

The single Unity E2E scenario:

1. Deploys source character A and ally B.
2. Applies an A-scaled buff to B at 25 ATK.
3. Captures a real projectile context from A.
4. Switches the active character to B.
5. Buffs off-field A from 25 to 100 live ATK.
6. Runs the projectile through `ProjectileRuntime`, its hitbox module,
   `HitBoxDamage`, and a real enemy target.
7. Verifies A's own 2x outgoing modifier yields 200 damage.
8. Verifies B remains at 35 ATK because the existing buff retained its
   application snapshot.

## Result

Passed in Unity EditMode on 2026-07-29:

- Passed: 1
- Failed: 0
- Skipped: 0
- Duration: 0.130 seconds

No unit tests were added.

## Artifact Ownership Regression

Validated in Unity EditMode on 2026-07-29 with a temporary E2E probe (removed
after execution):

1. Source A had 100 base ATK and its own +50% ATK artifact.
2. Character B had 100 base ATK and its own +200% ATK artifact.
3. The deployed runtimes resolved A at 150 ATK and B at 300 ATK.
4. After switching from A to B, A's off-field projectile damage context still
   resolved 150 ATK rather than borrowing B's 300 ATK.
5. A's deployed runtime retained A's artifact reference.

Result: PASS. The repository's existing deleted test files and deleted Unity
MCP test runner files were not restored.
