using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// =========================================================
// ActionResolverTable — Per-character ScriptableObject.
// Contains combo trees for each context and standalone
// contextual actions. Replaces hard-coded combo routing.
// =========================================================

[CreateAssetMenu(fileName = "ActionResolverTable_", menuName = "Game/Combat/Action Resolver Table")]
public class ActionResolverTable : ScriptableObject
{
    [Header("Combat Actions")]
    public List<ComboAction> comboActions = new List<ComboAction>();
    public List<SkillAction> skillActions = new List<SkillAction>();
    public List<UltimateAction> ultimateActions = new List<UltimateAction>();

    [Header("Standalone Contextual Actions (checked before combo trees)")]
    [Tooltip("One-shot actions like Plunge, post-skill specials, counter-attacks.")]
    public StandaloneAction[] standaloneActions;

    [Header("Fallback")]
    [Tooltip("If no combat actions match, execute this linear step instead of ignoring the input (prevents combo breakage if trees are misconfigured).")]
    public AttackStepData defaultLinearStep;
}
