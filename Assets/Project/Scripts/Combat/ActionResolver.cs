using System.Collections.Generic;
using UnityEngine;

// =========================================================
// ActionResolver - Stateful runtime combo tree walker.
// Created per-combo session by PlayerAttackState.
// Tracks current position in the tree and resolves
// the next AttackStepData based on InputContext.
// =========================================================

public class ActionResolver
{
    public enum PlungeSessionPhase
    {
        None,
        Charge,
        Dive
    }

    // =========================================================
    // State
    // =========================================================
    private ComboNode currentNode;
    private ComboTree activeTree;
    private StandaloneAction activeStandalone;
    private int treeDepth;

    // Logging
    private PlayerStateMachine fsm;

    public void SetLogger(PlayerStateMachine stateMachine)
    {
        fsm = stateMachine;
    }

    private void Log(string message, Color color)
    {
        if (fsm != null && fsm.showCombatLogs)
        {
            fsm.Log("Combat", $"Resolver: {message}", color);
        }
    }

    private void Log(string message) => Log(message, new Color(0.7f, 0.7f, 1f));

    // =========================================================
    // Public Read-Only
    // =========================================================
    public int TreeDepth => treeDepth;
    public bool IsActive => currentNode != null || activeStandalone != null;
    public PlungeSessionPhase CurrentPlungePhase { get; private set; }
    public bool IsPlungeChargeActive => CurrentPlungePhase == PlungeSessionPhase.Charge;
    public bool IsPlungeFalling => CurrentPlungePhase == PlungeSessionPhase.Dive;
    public CombatAction ActiveCombatAction { get; private set; }

    /// <summary>
    /// Plunge data from the active standalone action (if any).
    /// </summary>
    public StandaloneAction ActivePlungeAction => activeStandalone != null && activeStandalone.isPlungeAction ? activeStandalone : null;

    // =========================================================
    // Resolve - First Attack Input (no active combo)
    // =========================================================

    /// <summary>
    /// Called when the first attack input arrives (no active combo).
    /// Checks standalone actions first, then enters the appropriate combo tree.
    /// Returns the resolved AttackStepData, or null if nothing matches.
    /// </summary>
    public AttackStepData ResolveInitial(InputContext ctx, ActionResolverTable table, AttackCategory category = AttackCategory.Normal)
    {
        Reset();

        if (table == null) return null;

        // 1. Check standalone actions first (plunge, post-skill specials)
        StandaloneAction standalone = FindMatchingStandalone(ctx, table);
        if (standalone != null)
        {
            Log($"Standalone Match! Priority: {standalone.priority}, Plunge: {standalone.isPlungeAction}", Color.yellow);
            activeStandalone = standalone;
            ActiveCombatAction = standalone;
            activeTree = standalone.comboTree;

            if (standalone.isPlungeAction)
            {
                if (standalone.HasAuthoredPlungeTree())
                {
                    currentNode = standalone.GetEntryNode();
                    CurrentPlungePhase = PlungeSessionPhase.Charge;
                    treeDepth = 1;
                    return currentNode != null ? currentNode.stepData : null;
                }

                // Legacy fallback: preserve the old immediate-dive plunge behavior
                // when the authored data has not been migrated yet.
                CurrentPlungePhase = PlungeSessionPhase.Dive;
                treeDepth = 1;
                return null;
            }

            if (standalone.GetEntryNode() != null)
            {
                currentNode = standalone.GetEntryNode();
                treeDepth = 1;
                return currentNode.stepData;
            }

            currentNode = standalone.followUpNode;
            treeDepth = 1;
            return standalone.stepData;
        }

        // 2. Select the highest priority matching CombatAction
        CombatAction bestAction = null;
        CombatActionContext expectedContext = ctx.isGrounded ? CombatActionContext.Ground : CombatActionContext.Air;

        IEnumerable<CombatAction> searchList = category switch
        {
            AttackCategory.Normal => table.comboActions,
            AttackCategory.Skill => table.skillActions,
            AttackCategory.Ultimate => table.ultimateActions,
            _ => System.Linq.Enumerable.Empty<CombatAction>()
        };

        foreach (var action in searchList)
        {
            bool contextMatches = action.context == CombatActionContext.Both || action.context == expectedContext;
            if (contextMatches && ActionMatches(action, ctx))
            {
                if (bestAction == null
                    || GetEffectivePriority(action, ctx) > GetEffectivePriority(bestAction, ctx))
                {
                    bestAction = action;
                }
            }
        }

        if (bestAction != null)
        {
            Log($"Action Selection: '{bestAction.actionName}' (Category={category}, Priority={bestAction.priority})");
            ActiveCombatAction = bestAction;
            activeTree = bestAction.comboTree;
        }
        else
        {
            Log($"Resolution Failed: No matching {category} action for context {expectedContext}.", Color.red);
            return null;
        }

        if (activeTree?.rootNode == null)
        {
            Log("Active Tree or Root Node is null.", Color.red);
            return null;
        }

        // 3. Check if the root node itself has a matching route for the initial input
        currentNode = activeTree.rootNode;
        ComboNode resolved = ComboRouter.ResolveRoute(currentNode, ctx, false);

        if (resolved != null && resolved != currentNode)
        {
            Log($"Root Route matched! -> '{resolved.stepData?.stepName}'");
            currentNode = resolved;
        }

        treeDepth = 1;
        return currentNode.stepData;
    }

    // =========================================================
    // Resolve - Subsequent Attack Input (mid-combo)
    // =========================================================

    /// <summary>
    /// Called on subsequent attack inputs during an active combo.
    /// Evaluates routes on the current node, falls back to defaultNext.
    /// Returns the next AttackStepData, or null if combo is finished.
    /// </summary>
    public AttackStepData ResolveNext(InputContext ctx)
    {
        if (currentNode == null) return null;

        // Context Validation: Prevent Air combos from continuing on the Ground and vice versa.
        if (ActiveCombatAction != null && ActiveCombatAction.context != CombatActionContext.Both)
        {
            if (ActiveCombatAction.context == CombatActionContext.Ground && !ctx.isGrounded)
            {
                Log($"Context Invalidated: Expected Ground, but is Airborne. Canceling chain.", Color.red);
                return null; // Force chain break, FSM will start a new air action
            }
            if (ActiveCombatAction.context == CombatActionContext.Air && ctx.isGrounded)
            {
                Log($"Context Invalidated: Expected Air, but is Grounded. Canceling chain.", Color.red);
                return null; // Force chain break, FSM will start a new ground action
            }
        }

        string prevNodeName = currentNode.stepData?.stepName ?? "Unknown";

        ComboNode nextNode = ComboRouter.ResolveRoute(currentNode, ctx, allowDefault: false);

        if (nextNode != null)
        {
            Log($"Route Match! [{prevNodeName}] -> '{nextNode.stepData?.stepName}'", Color.green);
            currentNode = nextNode;
        }
        else
        {
            ComboNode defaultNext = currentNode.defaultNext;
            if (defaultNext != null)
            {
                Log($"Default Chain: [{prevNodeName}] -> '{defaultNext.stepData?.stepName}'");
                currentNode = defaultNext;
            }
            else
            {
                if (fsm != null && fsm.showCombatLogs)
                    Log($"Combo End: [{prevNodeName}] has no automatic follow-up.", Color.gray);
                return null;
            }
        }

        if (currentNode == null) return null;

        treeDepth++;
        return currentNode.stepData;
    }

    // =========================================================
    // Grab Success - Dedicated route for successful grabs
    // =========================================================

    public AttackStepData ResolveGrabSuccess(InputContext ctx)
    {
        if (currentNode == null) return null;

        if (currentNode.onGrabSuccessNode != null)
        {
            string prevNodeName = currentNode.stepData?.stepName ?? "Unknown";
            Log($"Grab Success (Legacy) Match! [{prevNodeName}] -> '{currentNode.onGrabSuccessNode.stepData?.stepName}'", Color.green);
            currentNode = currentNode.onGrabSuccessNode;
            treeDepth++;
            return currentNode.stepData;
        }

        if (currentNode.routes != null)
        {
            ComboRoute autoSuccessRoute = null;
            ComboRoute directionalMatchRoute = null;
            ComboRoute fallbackGrabRoute = null;

            for (int i = 0; i < currentNode.routes.Count; i++)
            {
                ComboRoute route = currentNode.routes[i];
                if (route?.targetNode == null || !route.requireGrabbedEnemy) continue;

                if (route.direction == DirectionRequirement.None)
                {
                    if (autoSuccessRoute == null || route.priority > autoSuccessRoute.priority)
                        autoSuccessRoute = route;
                }

                if (ComboRouter.RouteMatches(route, ctx))
                {
                    if (directionalMatchRoute == null || route.priority > directionalMatchRoute.priority)
                        directionalMatchRoute = route;
                }

                if (fallbackGrabRoute == null || route.priority > fallbackGrabRoute.priority)
                    fallbackGrabRoute = route;
            }

            ComboRoute bestMatch = autoSuccessRoute ?? directionalMatchRoute ?? fallbackGrabRoute;

            if (bestMatch != null)
            {
                string prevNodeName = currentNode.stepData?.stepName ?? "Unknown";
                string matchType = bestMatch == autoSuccessRoute ? "AUTO-NONE" :
                    bestMatch == directionalMatchRoute ? "DIRECTIONAL" : "FALLBACK";

                Log($"Grab Route Match ({matchType})! [{prevNodeName}] -> '{bestMatch.targetNode.stepData?.stepName}'", Color.green);
                currentNode = bestMatch.targetNode;
                treeDepth++;
                return currentNode.stepData;
            }
        }

        return null;
    }

    // =========================================================
    // Immediate Routes - Environment-driven interruptions
    // =========================================================

    public AttackStepData ResolveTriggers(StateContext ctx, int activeFrame)
    {
        AttackStepData currentStep = GetCurrentStepData();
        if (currentStep == null || currentStep.triggerWindows == null)
            return null;

        TriggerWindow bestMatch = null;
        for (int i = 0; i < currentStep.triggerWindows.Count; i++)
        {
            var trigger = currentStep.triggerWindows[i];
            if (trigger == null || trigger.targetNode == null) continue;

            bool inWindow = trigger.IsInWindow(activeFrame);
            bool conditionsMet = ActionConditionUtility.AllMet(trigger.triggerConditions, ctx);

            if (!inWindow) continue;

            if (conditionsMet)
            {
                if (bestMatch == null || trigger.priority > bestMatch.priority)
                {
                    bestMatch = trigger;
                }
            }
        }

        if (bestMatch != null)
        {
            foreach (var condition in bestMatch.triggerConditions)
            {
                condition?.OnTriggered(ctx);
            }

            if (bestMatch.targetNode.stepData == GetCurrentStepData())
            {
                return null;
            }

            Log($"Trigger Window Matched! [{currentStep.stepName}] -> '{(bestMatch.targetNode.stepData != null ? bestMatch.targetNode.stepData.stepName : "???")}'", Color.green);
            currentNode = bestMatch.targetNode;
            treeDepth++;
            return currentNode.stepData;
        }

        return null;
    }

    public AttackStepData ResolveImmediateRoute(StateContext ctx)
    {
        return null;
    }

    // =========================================================
    // Force Transitions
    // =========================================================

    public AttackStepData ForceTransitionToNode(ComboNode node)
    {
        if (node == null || node.stepData == GetCurrentStepData()) return null;
        currentNode = node;
        treeDepth++;
        return currentNode.stepData;
    }

    // =========================================================
    // Plunge Runtime
    // =========================================================

    public AttackStepData ResolvePlungeDive()
    {
        if (activeStandalone == null || !activeStandalone.isPlungeAction)
        {
            return null;
        }

        ComboNode diveNode = null;

        if (CurrentPlungePhase == PlungeSessionPhase.Charge)
        {
            diveNode = currentNode != null ? currentNode.defaultNext : activeStandalone.GetDiveNode();
        }
        else if (CurrentPlungePhase == PlungeSessionPhase.None)
        {
            diveNode = activeStandalone.GetDiveNode();
        }

        if (diveNode == null || diveNode.stepData == null)
        {
            return null;
        }

        currentNode = diveNode;
        CurrentPlungePhase = PlungeSessionPhase.Dive;
        treeDepth++;
        return currentNode.stepData;
    }

    /// <summary>
    /// Called when the player lands during plunge dive mode.
    /// Returns the plunge impact step data.
    /// </summary>
    public AttackStepData ResolvePlungeLanding(StateContext ctx, int activeFrame)
    {
        CurrentPlungePhase = PlungeSessionPhase.None;

        if (activeStandalone == null) return null;

        AttackStepData currentStep = GetCurrentStepData();
        if (currentStep != null && currentStep.triggerWindows != null)
        {
            TriggerWindow bestMatch = null;
            for (int i = 0; i < currentStep.triggerWindows.Count; i++)
            {
                var trigger = currentStep.triggerWindows[i];
                if (trigger == null || trigger.targetNode == null) continue;

                bool inWindow = trigger.IsInWindow(activeFrame);
                bool conditionsMet = ActionConditionUtility.AllMet(trigger.triggerConditions, ctx);
                if (!inWindow || !conditionsMet) continue;

                if (bestMatch == null || trigger.priority > bestMatch.priority)
                {
                    bestMatch = trigger;
                }
            }

            if (bestMatch != null)
            {
                foreach (var condition in bestMatch.triggerConditions)
                {
                    condition?.OnTriggered(ctx);
                }

                currentNode = bestMatch.targetNode;
                treeDepth++;
                return currentNode.stepData;
            }
        }

        ComboNode fallbackImpactNode = currentNode != null ? currentNode.defaultNext : activeStandalone.followUpNode;
        if (fallbackImpactNode != null && fallbackImpactNode.stepData != null)
        {
            currentNode = fallbackImpactNode;
            treeDepth++;
            return currentNode.stepData;
        }

        return activeStandalone.stepData;
    }

    // =========================================================
    // Queries
    // =========================================================

    public bool HasNextStep(InputContext ctx)
    {
        if (currentNode == null) return false;
        if (currentNode.defaultNext != null) return true;

        if (currentNode.routes != null)
        {
            for (int i = 0; i < currentNode.routes.Count; i++)
            {
                if (currentNode.routes[i]?.targetNode != null) return true;
            }
        }

        return false;
    }

    public bool IsLastStep()
    {
        if (currentNode == null) return true;
        if (currentNode.defaultNext != null) return false;

        if (currentNode.routes != null)
        {
            for (int i = 0; i < currentNode.routes.Count; i++)
            {
                if (currentNode.routes[i]?.targetNode != null) return false;
            }
        }

        return true;
    }

    public bool IsAirCombo => (ActiveCombatAction != null && ActiveCombatAction.context == CombatActionContext.Air)
        || (activeStandalone != null && activeStandalone.requireAirborne);

    public float GetComboResetDelay()
    {
        return activeTree?.comboResetDelay ?? 0.8f;
    }

    public AttackStepData GetCurrentStepData()
    {
        return currentNode?.stepData;
    }

    // =========================================================
    // Reset
    // =========================================================

    public void Reset()
    {
        currentNode = null;
        activeTree = null;
        activeStandalone = null;
        ActiveCombatAction = null;
        treeDepth = 0;
        CurrentPlungePhase = PlungeSessionPhase.None;
    }

    // =========================================================
    // Internal - Route Matching
    // =========================================================

    private StandaloneAction FindMatchingStandalone(StateContext ctx, ActionResolverTable table)
    {
        if (table.standaloneActions == null) return null;

        StandaloneAction bestMatch = null;

        for (int i = 0; i < table.standaloneActions.Count; i++)
        {
            StandaloneAction sa = table.standaloneActions[i];

            if (sa?.comboTree == null && sa?.stepData == null && !sa.isPlungeAction) continue;

            if (ActionMatches(sa, ctx))
            {
                if (bestMatch == null || sa.priority > bestMatch.priority)
                {
                    bestMatch = sa;
                }
            }
        }

        return bestMatch;
    }

    private static bool ActionMatches(CombatAction action, InputContext ctx)
    {
        if (action == null)
        {
            return false;
        }

        if (!ActionConditionUtility.AllMet(action.conditions, ctx))
        {
            return false;
        }

        if (ctx.passiveController == null)
        {
            return true;
        }

        if (!ctx.passiveController.IsActionUnlocked(action.actionName))
        {
            return false;
        }

        if (ctx.passiveController.TryGetReplacementActionName(action.actionName, out string replacementActionName)
            && !string.IsNullOrEmpty(replacementActionName)
            && !string.Equals(replacementActionName, action.actionName))
        {
            return false;
        }

        return true;
    }

    private static int GetEffectivePriority(CombatAction action, InputContext ctx)
    {
        if (action == null)
        {
            return int.MinValue;
        }

        int bonus = ctx.passiveController != null
            ? ctx.passiveController.GetActionPriorityModifier(action.actionName)
            : 0;

        return action.priority + bonus;
    }
}
