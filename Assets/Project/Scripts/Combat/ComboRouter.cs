using System.Collections.Generic;
using UnityEngine;

// =========================================================
// ComboRouter — Centralized routing engine.
// Evaluates combo paths based on StateContext.
// Replaces the fragmented resolver logic.
// =========================================================

public static class ComboRouter
{
    /// <summary>
    /// Evaluates all routes on a node against the current StateContext.
    /// Priority order: Immediate triggers > Directional > Fallback/Default.
    /// </summary>
    public static ComboNode ResolveRoute(ComboNode node, StateContext ctx, bool allowDefault = true)
    {
        if (node == null) return null;

        if (node.routes == null || node.routes.Count == 0)
            return allowDefault ? node.defaultNext : null;

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
        return ActionConditionUtility.AllMet(route.conditions, ctx);
    }
}
