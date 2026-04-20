using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// TriggerWindow — Interrupt triggers based on conditions.
// Defines a window within the Active phase where specific 
// environmental/status events can bypass recovery.
// =========================================================

[Serializable]
public class TriggerWindow
{
    [Tooltip("The frame in the active phase when this trigger becomes active.")]
    public int startFrame;
    [Tooltip("The frame in the active phase when this trigger expires. -1 means it lasts the entire active phase.")]
    public int endFrame = -1;
    
    [SerializeReference] public List<ActionConditionBase> triggerConditions = new List<ActionConditionBase>();
    [SerializeReference] public ComboNode targetNode;
    
    [Tooltip("Higher priority triggers overwrite lower ones if both occur at the same time.")]
    public int priority;

    public bool IsInWindow(int activeFrame)
    {
        if (activeFrame < startFrame) return false;
        if (endFrame != -1 && activeFrame > endFrame) return false;
        return true;
    }
}
