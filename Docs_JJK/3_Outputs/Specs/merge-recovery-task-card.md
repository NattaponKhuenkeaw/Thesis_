---
slug: merge-recovery
status: approved
source: manual
gdd_tags: []
owner: implementation-agent
human_checkpoint: not-required
next_agent: implementation-agent
blocked_by: []
---

# Restore the Unity project after the failed stash apply

## Problem

Unity 6000.3.4f1 reports C# merge conflict markers and a missing ArtifactRewardPicker prefab when opening Test_Playground on core_character.

## Scope

Recover the authored snapshot behind the failed stash apply. Restore affected scripts, serialized assets, and original Unity metadata together. Preserve the damaged commit and the recovered source as separate Git references so the repair can be reviewed.

## Acceptance criteria

- Remove conflict markers from the project and compile the game and editor assemblies.
- Restore the reward picker source with its original GUID and the object IDs used by Test_Playground.
- Restore prefab and script dependencies present in the source snapshot.
- Keep package and project settings unchanged.

## Routing

Workflow: `/fix-bug`. Repair branch: `codex/fix/merge-recovery`. This restores existing authored behavior and does not require a GDD design decision.
