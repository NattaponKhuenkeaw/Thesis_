using System.Collections.Generic;
using UnityEngine;

// =========================================================
// ActionResolver — Stateful runtime combo tree walker.
// Created per-combo session by PlayerAttackState.
// Tracks current position in the tree and resolves
// the next AttackStepData based on InputContext.
// =========================================================

public class ActionResolver
{
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
        this.fsm = stateMachine;
    }

    private void Log(string message, Color color)
    {
        if (fsm != null && fsm.showCombatLogs)
        {
            fsm.Log("Combat", $"Resolver: {message}", color);
        }
    }

    private void Log(string message) => Log(message, new Color(0.7f, 0.7f, 1f)); // Default light blue

    // =========================================================
    // Public Read-Only
    // =========================================================
    public int TreeDepth => treeDepth;
    public bool IsActive => currentNode != null || activeStandalone != null;
    public bool IsPlungeFalling { get; private set; }
    public CombatAction ActiveCombatAction { get; private set; }

    /// <summary>
    /// Plunge data from the active standalone action (if any).
    /// </summary>
    public StandaloneAction ActivePlungeAction => activeStandalone != null && activeStandalone.isPlungeAction ? activeStandalone : null;

    // =========================================================
    // Resolve — First Attack Input (no active combo)
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

            if (standalone.isPlungeAction)
            {
                // Plunge: don't execute the step yet — enter plunge fall mode
                IsPlungeFalling = true;
                treeDepth = 1;
                return null; // Signal to PlayerAttackState to enter plunge fall mode
            }

            currentNode = standalone.followUpNode; // may be null (one-shot)
            treeDepth = 1;
            return standalone.stepData;
        }

        // 2. Select the highest priority matching CombatAction
        CombatAction bestAction = null;
        CombatActionContext expectedContext = ctx.isGrounded ? CombatActionContext.Ground : CombatActionContext.Air;
        
        System.Collections.Generic.IEnumerable<CombatAction> searchList = category switch
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
            if (table.defaultLinearStep != null)
            {
                Log($"Using Default Linear Step fallback.", Color.yellow);
                activeTree = new ComboTree { rootNode = new ComboNode { stepData = table.defaultLinearStep } };
            }
            else
            {
                return null;
            }
        }

        if (activeTree?.rootNode == null)
        {
            Log("Active Tree or Root Node is null.", Color.red);
            return null;
        }

        // 3. Check if the ROOT node itself has a matching route for the initial input
        currentNode = activeTree.rootNode;
        // Don't follow defaultNext on first resolve (allowDefault = false)
        ComboNode resolved = ComboRouter.ResolveRoute(currentNode, ctx, false);
        
        if (resolved != null && resolved != currentNode)
        {
            // A root-level route matched (e.g., Down+LMB on first input)
            Log($"Root Route matched! -> '{resolved.stepData?.stepName}'");
            currentNode = resolved;
        }

        treeDepth = 1;
        return currentNode.stepData;
    }

    // =========================================================
    // Resolve — Subsequent Attack Input (mid-combo)
    // =========================================================

    /// <summary>
    /// Called on subsequent attack inputs during an active combo.
    /// Evaluates routes on the current node, falls back to defaultNext.
    /// Returns the next AttackStepData, or null if combo is finished.
    /// </summary>
    public AttackStepData ResolveNext(InputContext ctx)
    {
        if (currentNode == null) return null;

        string prevNodeName = currentNode.stepData?.stepName ?? "Unknown";

        // Check conditional routes on current node (top-to-bottom, priority-sorted)
        ComboNode nextNode = ComboRouter.ResolveRoute(currentNode, ctx, allowDefault: false);

        if (nextNode != null)
        {
            Log($"Route Match! [{prevNodeName}] -> '{nextNode.stepData?.stepName}'", Color.green);
            currentNode = nextNode;
        }
        else
        {
            // No route matched -> follow default chain
            ComboNode defaultNext = currentNode.defaultNext;
            if (defaultNext != null)
            {
                Log($"Default Chain: [{prevNodeName}] -> '{defaultNext.stepData?.stepName}'");
                currentNode = defaultNext;
            }
            else
            {
                // IMPORTANT: If no automatic follow-up exists, DO NOT wipe currentNode.
                // Wiping currentNode prevents ResolveTriggers (active phase interruptions)
                // from working for the remainder of the animation.
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
    // Grab Success — Dedicated route for successful grabs
    // =========================================================

    public AttackStepData ResolveGrabSuccess(InputContext ctx)
    {
        if (currentNode == null) return null;

        // 1. Dedicated grab success node (highest priority)
        if (currentNode.onGrabSuccessNode != null)
        {
            string prevNodeName = currentNode.stepData?.stepName ?? "Unknown";
            Log($"Grab Success (Legacy) Match! [{prevNodeName}] -> '{currentNode.onGrabSuccessNode.stepData?.stepName}'", Color.green);
            currentNode = currentNode.onGrabSuccessNode;
            treeDepth++;
            return currentNode.stepData;
        }

        // 2. Check routes that specifically require a grabbed enemy (never fall to defaultNext)
        if (currentNode.routes != null)
        {
            ComboRoute autoSuccessRoute = null;
            ComboRoute directionalMatchRoute = null;
            ComboRoute fallbackGrabRoute = null;

            for (int i = 0; i < currentNode.routes.Count; i++)
            {
                ComboRoute route = currentNode.routes[i];
                if (route?.targetNode == null || !route.requireGrabbedEnemy) continue;

                // Priority A: Explicit Auto-Execute (NONE)
                if (route.direction == DirectionRequirement.None)
                {
                    if (autoSuccessRoute == null || route.priority > autoSuccessRoute.priority)
                        autoSuccessRoute = route;
                }

                // Priority B: Matching Direction (Choice)
                if (ComboRouter.RouteMatches(route, ctx))
                {
                    if (directionalMatchRoute == null || route.priority > directionalMatchRoute.priority)
                        directionalMatchRoute = route;
                }

                // Priority C: Any Grab Route (Fallback)
                if (fallbackGrabRoute == null || route.priority > fallbackGrabRoute.priority)
                    fallbackGrabRoute = route;
            }

            // Execute in priority order: None > Directional > Fallback
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
    // Immediate Routes — Environment-driven interruptions (e.g. Wall Hit)
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
            // Trigger callbacks for matching conditions
            foreach (var condition in bestMatch.triggerConditions)
            {
                if (condition != null) condition.OnTriggered(ctx);
            }

            // Reject if we are trying to transition to ourselves
            if (bestMatch.targetNode.stepData == GetCurrentStepData())
            {
                return null;
            }

            Log($"Trigger Window Matched! [{currentStep.stepName}] → '{(bestMatch.targetNode.stepData != null ? bestMatch.targetNode.stepData.stepName : "???")}'", Color.green);
            currentNode = bestMatch.targetNode;
            treeDepth++;
            return currentNode.stepData;
        }

        return null;
    }

    public AttackStepData ResolveImmediateRoute(StateContext ctx)
    {
        // Standard conditional routes no longer immediately interrupt.
        // User must use triggerWindows for interruptions.
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
    // Plunge Landing — Execute the plunge impact step
    // =========================================================

    /// <summary>
    /// Called when the player lands during plunge fall mode.
    /// Returns the plunge impact step data.
    /// </summary>
    public AttackStepData ResolvePlungeLanding()
    {
        IsPlungeFalling = false;

        if (activeStandalone == null) return null;

        currentNode = activeStandalone.followUpNode; // follow-up after impact (may be null)
        return activeStandalone.stepData;
    }

    // =========================================================
    // Queries
    // =========================================================

    /// <summary>
    /// Whether there are more steps available after the current one.
    /// Used for ShouldPreserveAirComboOnComplete checks.
    /// </summary>
    public bool HasNextStep(InputContext ctx)
    {
        if (currentNode == null) return false;
        if (currentNode.defaultNext != null) return true;

        // Check if any route could potentially match
        if (currentNode.routes != null)
        {
            for (int i = 0; i < currentNode.routes.Count; i++)
            {
                if (currentNode.routes[i]?.targetNode != null) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the current node is the last in its chain (no defaultNext, no routes).
    /// </summary>
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

    /// <summary>
    /// Whether the current attack is an air combo.
    /// </summary>
    public bool IsAirCombo => (ActiveCombatAction != null && ActiveCombatAction.context == CombatActionContext.Air) 
                           || (activeStandalone != null && activeStandalone.requireAirborne);

    /// <summary>
    /// Get the combo reset delay from the active tree.
    /// </summary>
    public float GetComboResetDelay()
    {
        return activeTree?.comboResetDelay ?? 0.8f;
    }

    /// <summary>
    /// Get the current step data (for frame data queries).
    /// </summary>
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
        IsPlungeFalling = false;
    }

    // =========================================================
    // Internal — Route Matching
    // =========================================================



    private StandaloneAction FindMatchingStandalone(StateContext ctx, ActionResolverTable table)
    {
        if (table.standaloneActions == null) return null;

        StandaloneAction bestMatch = null;

        for (int i = 0; i < table.standaloneActions.Length; i++)
        {
            StandaloneAction sa = table.standaloneActions[i];
            
            // Allow checking if there's comboTree or stepData
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
