# 2026-11-08 — Recover the Unity project

## Bugs Found

GitHub Desktop applied stash `c03a7c8e`, based on `d862aef8`, to its older ancestor `d43cbd83`. Commit `e712b8cd` recorded conflict markers in scripts and serialized assets and omitted dependencies from the stash's base. Unity reported CS8300 and could not load the reward picker in Test_Playground.

## Recovery

Restored the original stash snapshot, including the base's prefab and script dependencies and the stash's authored changes. Kept the source reachable as `codex/recovery-source-c03a7c8e`. The repair commit is a descendant of the damaged commit on `codex/fix/merge-recovery`.

## Verification

- Unity rebuilt Assembly-CSharp and Assembly-CSharp-Editor and reimported ArtifactRewardPicker.
- The reward picker retained its GUID and all three object IDs targeted by Test_Playground. No project conflict markers or duplicate asset GUIDs were found.
- Two source GUIDs were already unresolved in the pre-incident commit, in AdminCanvas and the legacy Test scene.
