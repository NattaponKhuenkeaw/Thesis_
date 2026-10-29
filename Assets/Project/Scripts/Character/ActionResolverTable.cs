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

    [Header("QTE Entry Actions (prepared team swaps only)")]
    public List<ComboAction> qteActions = new List<ComboAction>();

    [Header("Standalone Contextual Actions (checked before combo trees)")]
    [Tooltip("One-shot actions like Plunge, post-skill specials, counter-attacks.")]
    public List<StandaloneAction> standaloneActions = new List<StandaloneAction>();

}
