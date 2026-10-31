using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class QteSwapDefinition
{
    public bool enabled;
    [Tooltip("Outgoing actions whose accepted primary hits prepare teammates.")]
    public string[] triggerActionIds = Array.Empty<string>();
    [Tooltip("QTE entry action in the resolver table. Existing ordinary actions can also be referenced by id or name.")]
    public string introActionKey;
    [Tooltip("Empty selects the action root; otherwise selects this exact authored step.")]
    public string introStepName;
    [HideInInspector, Tooltip("Legacy character-owned combo. New QTE trees belong to ActionResolverTable.qteActions.")]
    public ComboTree introComboTree;
    [Header("Entry Presentation")]
    public QteEntryMode entryMode;
    public QtePlungeSettings plunge;
    [Tooltip("Spawn behind the outgoing character on X, mirrored by their facing direction. Then face the enemy.")]
    [FormerlySerializedAs("targetOffset"), Min(0f)] public float entryOffset = 1f;
    public AudioClip readySfx;
    [Header("Committed Intro Reward")]
    [Tooltip("Granted once when this character actually starts its QTE intro, never on readiness or an ordinary swap.")]
    public string entryGrantTag;
    public string entryGrantCounterId;
    [Min(0)] public int entryGrantAmount;
    [Tooltip("Manual action that consumes the entry marker; the counter itself uses normal action resource costs.")]
    public string entryGrantConsumeActionId;
    [NonSerialized] private ActionResolverTable _cachedTable;
    [NonSerialized] private CombatAction _cachedAction;
    [NonSerialized] private AttackStepData _cachedStep;

    public bool MatchesTrigger(string actionId)
    {
        if (!enabled || string.IsNullOrEmpty(actionId) || triggerActionIds == null) return false;
        foreach (string candidate in triggerActionIds)
            if (string.Equals(candidate, actionId, StringComparison.Ordinal)) return true;
        return false;
    }

    public bool TryResolveIntro(ActionResolverTable table, out CombatAction action, out AttackStepData step)
    {
        action = null;
        step = null;
        if (!enabled || table == null || string.IsNullOrEmpty(introActionKey)) return false;
        action = FindAction(table.qteActions);
        if (action != null)
        {
            ComboNode root = action.comboTree?.rootNode;
            step = string.IsNullOrEmpty(introStepName) || root?.stepData?.stepName == introStepName
                ? root?.stepData : FindStep(root, new HashSet<ComboNode>());
            return step != null;
        }
        if (introComboTree?.rootNode?.stepData != null)
        {
            action = FindAction(table.standaloneActions) ?? FindAction(table.comboActions);
            step = action != null ? introComboTree.rootNode.stepData : null;
            return step != null;
        }
        if (_cachedTable == table && _cachedAction != null && _cachedStep != null
            && string.Equals(_cachedAction.ActionKey, introActionKey, StringComparison.Ordinal)
            && (string.IsNullOrEmpty(introStepName) || string.Equals(_cachedStep.stepName, introStepName, StringComparison.Ordinal)))
        {
            action = _cachedAction;
            step = _cachedStep;
            return true;
        }
        action = FindAction(table.standaloneActions) ?? FindAction(table.comboActions);
        if (action == null) return false;
        ComboNode entry = action is StandaloneAction standalone ? standalone.GetEntryNode() : action.comboTree?.rootNode;
        step = string.IsNullOrEmpty(introStepName)
            ? entry?.stepData ?? (action as StandaloneAction)?.GetEntryStep()
            : FindStep(entry, new HashSet<ComboNode>());
        _cachedTable = table;
        _cachedAction = action;
        _cachedStep = step;
        return step != null;
    }

    public ComboTree GetIntroComboTree(ActionResolverTable table)
    {
        CombatAction action = table != null ? FindAction(table.qteActions) : null;
        return action != null ? action.comboTree : introComboTree;
    }

    private CombatAction FindAction<T>(List<T> actions) where T : CombatAction
    {
        if (actions == null) return null;
        foreach (T action in actions)
            if (action != null && string.Equals(action.ActionKey, introActionKey, StringComparison.Ordinal)) return action;
        return null;
    }

    private AttackStepData FindStep(ComboNode node, HashSet<ComboNode> visited)
    {
        if (node == null || !visited.Add(node)) return null;
        if (string.Equals(node.stepData?.stepName, introStepName, StringComparison.Ordinal)) return node.stepData;
        AttackStepData found = FindStep(node.defaultNext, visited) ?? FindStep(node.onGrabSuccessNode, visited);
        if (found != null) return found;
        if (node.routes != null)
            foreach (ComboRoute route in node.routes)
            {
                found = FindStep(route?.targetNode, visited);
                if (found != null) return found;
            }
        return null;
    }
}
