using System.Collections.Generic;
using UnityEngine;

// =========================================================
// ComboRouter — Centralized routing engine.
// Evaluates combo paths based on StateContext.
// Replaces the fragmented resolver logic.
// =========================================================

public static class ComboRouter
{
    public static ComboRoute FindMatchedRoute(ComboNode node, StateContext ctx, bool initial = false)
    {
        if (node == null || node.routes == null || node.routes.Count == 0)
            return null;

        ComboRoute bestMatch = null;
        for (int i = 0; i < node.routes.Count; i++)
        {
            ComboRoute route = node.routes[i];
            if (route?.targetNode == null || (initial && route.followUpOnly)) continue;

            if (RouteMatches(route, ctx))
            {
                if (bestMatch == null || route.priority > bestMatch.priority)
                {
                    bestMatch = route;
                }
            }
        }
        return bestMatch;
    }

    /// <summary>
    /// Evaluates all routes on a node against the current StateContext.
    /// Priority order: Immediate triggers > Directional > Fallback/Default.
    /// </summary>
    public static ComboNode ResolveRoute(ComboNode node, StateContext ctx, bool allowDefault = true)
    {
        if (node == null) return null;
        ComboRoute matchedRoute = FindMatchedRoute(node, ctx);
        if (matchedRoute != null)
            return matchedRoute.targetNode;
        return allowDefault ? node.defaultNext : null;
    }

    /// <summary>
    /// Evaluates environmental/immediate routes that can interrupt the active phase.
    /// </summary>
    public static ComboNode ResolveImmediateRoute(ComboNode node, StateContext ctx)
    {
        // Immediate Interrupts from the 'routes' list are no longer supported.
        // All interrupts must be defined in the 'triggerWindows' (Automatic Transitions) list.
        return null;
    }

    /// <summary>
    /// Determines if a specific route matches the StateContext.
    /// Handles both legacy fields and the new conditions list.
    /// </summary>
    public static bool RouteMatches(ComboRoute route, StateContext ctx)
    {
        // 1. Check legacy/obsolete fields if they haven't been migrated
        #pragma warning disable 618
        if (route.direction != DirectionRequirement.Any
            && route.direction != DirectionRequirement.None
            && route.direction != ctx.input.ResolvedDirection)
            return false;

        if (route.requireGrounded && !ctx.input.isGrounded) return false;
        if (route.requireAirborne && ctx.input.isGrounded) return false;
        if (route.requireGrabbedEnemy && !ctx.input.hasGrabbedEnemy) return false;

        if (route.requiredPreviousState != PlayerStateId.None
            && route.requiredPreviousState != ctx.input.previousState)
            return false;
        #pragma warning restore 618

        // 2. Check the unified conditions list
        if (route.targetNode != null && route.targetNode.stepData != null)
        {
            ctx.stepName = route.targetNode.stepData.stepName;
        }
        return ActionConditionUtility.AllMet(route.conditions, ctx);
    }
}
