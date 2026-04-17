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
            if (action.context == expectedContext
                && ActionMatches(action, ctx))
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
        }

        if (activeTree?.rootNode == null)
        {
            Log("Active Tree or Root Node is null.", Color.red);
            return null;
        }

        // 3. Check if the ROOT node itself has a matching route for the initial input
        currentNode = activeTree.rootNode;
        // Don't follow defaultNext on first resolve (allowDefault = false)
        ComboNode resolved = TryResolveRouteAtNode(currentNode, ctx, false);
        
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
        ComboNode nextNode = TryResolveRouteAtNode(currentNode, ctx, allowDefault: false);

        if (nextNode != null)
        {
            Log($"Route Match! [{prevNodeName}] -> '{nextNode.stepData?.stepName}'", Color.green);
            currentNode = nextNode;
        }
        else
        {
            // No route matched → follow default chain
            ComboNode defaultNext = currentNode.defaultNext;
            if (defaultNext != null)
            {
                Log($"Default Chain: [{prevNodeName}] -> '{defaultNext.stepData?.stepName}'");
            }
            else
            {
                Log($"Combo End: [{prevNodeName}] has no follow-up.", Color.gray);
            }
            currentNode = defaultNext;
        }

        if (currentNode == null) return null;

        treeDepth++;
        return currentNode.stepData;
    }

    // =========================================================
    // Grab Success — Dedicated route for successful grabs
    // =========================================================

    public AttackStepData ResolveGrabSuccess()
    {
        if (currentNode == null) return null;

        // 1. Dedicated grab success node (highest priority)
        if (currentNode.onGrabSuccessNode != null)
        {
            string prevNodeName = currentNode.stepData?.stepName ?? "Unknown";
            Log($"Grab Success Match! [{prevNodeName}] -> '{currentNode.onGrabSuccessNode.stepData?.stepName}'", Color.green);
            currentNode = currentNode.onGrabSuccessNode;
            treeDepth++;
            return currentNode.stepData;
        }

        // 2. Check routes that specifically require a grabbed enemy (never fall to defaultNext)
        if (currentNode.routes != null)
        {
            ComboRoute bestGrabRoute = null;
            for (int i = 0; i < currentNode.routes.Count; i++)
            {
                ComboRoute route = currentNode.routes[i];
                if (route?.targetNode == null) continue;
                if (!route.requireGrabbedEnemy) continue; // ONLY grab-specific routes

                if (bestGrabRoute == null || route.priority > bestGrabRoute.priority)
                    bestGrabRoute = route;
            }

            if (bestGrabRoute != null)
            {
                string prevNodeName = currentNode.stepData?.stepName ?? "Unknown";
                Log($"Grab Route Match! [{prevNodeName}] -> '{bestGrabRoute.targetNode.stepData?.stepName}'", Color.green);
                currentNode = bestGrabRoute.targetNode;
                treeDepth++;
                return currentNode.stepData;
            }
        }

        return null;
    }

    // =========================================================
    // Immediate Routes — Environment-driven interruptions (e.g. Wall Hit)
    // =========================================================

    public AttackStepData ResolveImmediateRoute(InputContext ctx)
    {
        if (currentNode == null || currentNode.routes == null) return null;

        ComboRoute bestMatch = null;
        for (int i = 0; i < currentNode.routes.Count; i++)
        {
            ComboRoute route = currentNode.routes[i];
            if (route == null || !route.allowImmediateInterrupt || route.targetNode == null)
                continue;

            if (RouteMatches(route, ctx))
            {
                if (bestMatch == null || route.priority > bestMatch.priority)
                    bestMatch = route;
            }
        }

        if (bestMatch != null)
        {
            // Safety: Do not interrupt if the target step is already the one we are playing.
            // This prevents infinite reset loops (frame-1 restarts) when against a wall.
            if (bestMatch.targetNode.stepData == GetCurrentStepData())
                return null;

            string prevNodeName = currentNode.stepData?.stepName ?? "Unknown";
            Log($"Immediate Interrupt! [{prevNodeName}] -> '{bestMatch.targetNode.stepData?.stepName}'", Color.cyan);
            currentNode = bestMatch.targetNode;
            treeDepth++;
            return currentNode.stepData;
        }

        return null;
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

    private ComboNode TryResolveRouteAtNode(ComboNode node, InputContext ctx, bool allowDefault = true)
    {
        if (node.routes == null || node.routes.Count == 0)
            return allowDefault ? node.defaultNext : null;

        // Sort by priority (highest first) — use simple insertion since lists are small
        ComboRoute bestMatch = null;

        for (int i = 0; i < node.routes.Count; i++)
        {
            ComboRoute route = node.routes[i];
            if (route?.targetNode == null) continue;

            if (RouteMatches(route, ctx))
            {
                if (bestMatch == null || route.priority > bestMatch.priority)
                {
                    bestMatch = route;
                }
            }
        }

        if (bestMatch != null)
            return bestMatch.targetNode;

        return allowDefault ? node.defaultNext : null;
    }

    private static bool RouteMatches(ComboRoute route, InputContext ctx)
    {
        // Direction check
        if (route.direction != DirectionRequirement.Any
            && route.direction != ctx.ResolvedDirection)
            return false;

        // Grounding checks
        if (route.requireGrounded && !ctx.isGrounded) return false;
        if (route.requireAirborne && ctx.isGrounded) return false;

        // Grabbed enemy check
        if (route.requireGrabbedEnemy && !ctx.hasGrabbedEnemy) return false;

        // Previous state check
        if (route.requiredPreviousState != PlayerStateId.None
            && route.requiredPreviousState != ctx.previousState)
            return false;

        bool conditionsMet = ActionConditionUtility.AllMet(route.conditions, ctx);
        
        // --- DIAGNOSTIC LOG ---
        if (conditionsMet && route.targetNode != null && route.targetNode.stepData != null 
            && route.targetNode.stepData.stepName == "Wall Slam")
        {
            Debug.Log($"[Resolver] MATCH FOUND for 'Wall Slam'. Context Wall={ctx.isAgainstWall}");
        }

        return conditionsMet;
    }

    private StandaloneAction FindMatchingStandalone(InputContext ctx, ActionResolverTable table)
    {
        if (table.standaloneActions == null) return null;

        StandaloneAction bestMatch = null;

        for (int i = 0; i < table.standaloneActions.Length; i++)
        {
            StandaloneAction sa = table.standaloneActions[i];
            if (sa?.stepData == null && !sa.isPlungeAction) continue;

            if (StandaloneMatches(sa, ctx))
            {
                if (bestMatch == null || sa.priority > bestMatch.priority)
                {
                    bestMatch = sa;
                }
            }
        }

        return bestMatch;
    }

    private static bool StandaloneMatches(StandaloneAction sa, InputContext ctx)
    {
        if (sa.direction != DirectionRequirement.Any
            && sa.direction != ctx.ResolvedDirection)
            return false;

        if (sa.requireGrounded && !ctx.isGrounded) return false;
        if (sa.requireAirborne && ctx.isGrounded) return false;

        if (sa.requiredPreviousState != PlayerStateId.None
            && sa.requiredPreviousState != ctx.previousState)
            return false;

        return ActionConditionUtility.AllMet(sa.conditions, ctx);
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
