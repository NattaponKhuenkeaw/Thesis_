---
slug: authored-action-facing-event
status: approved
source: manual
gdd_tags:
  - combat
  - tech-stack
owner: architect-agent
human_checkpoint: completed
next_agent: implementation-agent
blocked_by: []
---

# Authored Action Facing Event — Architecture Plan

## Decision

Extend the existing `ActionEventFrameData` system. Do not add facing logic to combo routing or MotionData.

## Data Model

```csharp
public enum ActionFacingMode
{
    Opposite,
    Left,
    Right
}

[Serializable]
public class FacingDirectionEventSettings
{
    public ActionFacingMode mode = ActionFacingMode.Opposite;
}
```

Add `SetFacingDirection` to `ActionEventType` and add a serialized `facingDirectionSettings` payload to `ActionEventEntry`.

## Data Flow

```text
ActionEventFrameData trigger frame
→ CombatPlayerState.ExecuteActionEvent
→ resolve Opposite/Left/Right
→ PlayerMovement.SnapFacingDirection(-1 or +1)
→ subsequent MotionData/hitboxes/VFX read the updated facing
```

## Responsibilities

| Class | Responsibility |
| --- | --- |
| `ActionEventFrameData` | Stores authored facing event and mode. |
| `CombatPlayerState` | Executes the event exactly once at its frame. |
| `PlayerMovement` | Remains the authority that applies character facing. |
| Rugal `ActionResolverTable` | Authors the Opposite event at the re-grab/second-dash boundary. |

## Affected Files

- `Assets/Project/Scripts/Character/ActionEventFrameData.cs`
- `Assets/Project/Scripts/Player/States/CombatPlayerState.cs`
- `Assets/Project/Scripts/Editor/UI/PropertyCardBuilder.cs`
- `Assets/Project/Character/Hero/test_02_rugal/ActionResolverTable_Char_2_Rugal.asset`
- EditMode tests for event execution and input independence

## Constraints

- No direct transform-scale mutation outside `PlayerMovement`.
- No change to captured action input/output context.
- No input sampling when the event executes.
- No changes to currently edited Rugal asset data until the human checkpoint is approved.

## ADR Impact

No new ADR is required. This extends the existing frame-event pattern and follows ADR-003’s decision that `PlayerMovement` owns facing and authored combat steps own their execution timeline.
