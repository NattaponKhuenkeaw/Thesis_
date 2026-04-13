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

    // Legacy fields mapped for migration
    [HideInInspector] public ComboTree groundComboTree;
    [HideInInspector] public ComboTree airComboTree;
    [HideInInspector] public ComboTree groundSkillTree;
    [HideInInspector] public ComboTree airSkillTree;
    [HideInInspector] public ComboTree groundUltimateTree;
    [HideInInspector] public ComboTree airUltimateTree;

    // =========================================================
    // Migration Helpers
    // =========================================================

    [ContextMenu("Migrate Legacy Trees")]
    public void MigrateFromLegacyTrees()
    {
        TryMigrateTree(groundComboTree, "Ground Attack", AttackCategory.Normal, CombatActionContext.Ground);
        TryMigrateTree(airComboTree, "Air Attack", AttackCategory.Normal, CombatActionContext.Air);
        TryMigrateTree(groundSkillTree, "Ground Skill", AttackCategory.Skill, CombatActionContext.Ground);
        TryMigrateTree(airSkillTree, "Air Skill", AttackCategory.Skill, CombatActionContext.Air);
        TryMigrateTree(groundUltimateTree, "Ground Ultimate", AttackCategory.Ultimate, CombatActionContext.Ground);
        TryMigrateTree(airUltimateTree, "Air Ultimate", AttackCategory.Ultimate, CombatActionContext.Air);

        groundComboTree = null;
        airComboTree = null;
        groundSkillTree = null;
        airSkillTree = null;
        groundUltimateTree = null;
        airUltimateTree = null;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void TryMigrateTree(ComboTree tree, string name, AttackCategory cat, CombatActionContext ctx)
    {
        if (tree != null && tree.rootNode != null)
        {
            int priority = cat == AttackCategory.Normal ? 0 : 10;
            
            switch (cat)
            {
                case AttackCategory.Normal:
                    comboActions.Add(new ComboAction { actionName = name, context = ctx, priority = priority, comboTree = tree });
                    break;
                case AttackCategory.Skill:
                    skillActions.Add(new SkillAction { actionName = name, context = ctx, priority = priority, comboTree = tree });
                    break;
                case AttackCategory.Ultimate:
                    ultimateActions.Add(new UltimateAction { actionName = name, context = ctx, priority = priority, comboTree = tree });
                    break;
            }
        }
    }

    private static System.Collections.Generic.List<ComboNode> BuildChainFromSteps(System.Collections.Generic.IEnumerable<AttackStepData> steps, AttackCategory category = AttackCategory.Normal)
    {
        var result = new System.Collections.Generic.List<ComboNode>();
        ComboNode prev = null;

        foreach (var step in steps)
        {
            if (step == null) continue;

            var node = new ComboNode { stepData = step };
            if (prev != null) prev.defaultNext = node;
            
            result.Add(node);
            prev = node;
        }
        return result;
    }

    public static void MigrateGrabRoutesManual(ComboNode node)
    {
        // Note: GrabHitbox.branchOnSuccess has been removed, but this helper 
        // structure is kept if future manual structural migrations are needed.
    }
}
