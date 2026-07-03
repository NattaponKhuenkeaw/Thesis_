using System.Collections.Generic;
using UnityEngine;

namespace CombatCore
{
    /// <summary>
    /// Lifecycle state for a grab session. A single session spans
    /// from grab-confirm through the entire throw sequence.
    /// </summary>
    public enum GrabSessionState
    {
        Inactive,       // No active grab
        Held,           // Enemy is bound to socket, combo in progress
        Releasing       // Release animation playing, enemy being ejected
    }

    /// <summary>
    /// Represents the state and targets of an active grab sequence.
    /// Redesigned as a session-based state machine: a grab session is
    /// the single source of truth for whether enemies are held.
    ///
    /// Session lifecycle:
    ///   Inactive → Held (BeginSession)
    ///   Held → Held (ExtendHold on each ExecuteStep)
    ///   Held → Releasing (ReleaseClean — normal throw end)
    ///   Held → Inactive (ForceRelease — player interrupted)
    ///   Releasing → Inactive (auto after release)
    /// </summary>
    [System.Serializable]
    public class ThrowContext
    {
        public GrabSessionState SessionState { get; private set; } = GrabSessionState.Inactive;
        public bool IsActive => SessionState != GrabSessionState.Inactive;
        public int AttackerId { get; private set; }

        // Supports multiple grabbed enemies (e.g. multi-throws).
        private List<EnemyController> grabbedEnemies = new List<EnemyController>();
        private Transform grabSocket;
        private Vector2 snapOffset;

        public ThrowContext(int attackerInstanceId)
        {
            AttackerId = attackerInstanceId;
            SessionState = GrabSessionState.Inactive;
        }

        // ─── Begin Session ─────────────────────────────────────
        // Called ONCE when OnGrabConnected fires (after grab hitbox confirms).
        // Transitions to Held state and binds the enemy to the socket.
        public void BeginSession(EnemyController enemy, Transform socket, Vector2 offset)
        {
            if (enemy == null) return;
            if (!grabbedEnemies.Contains(enemy))
                grabbedEnemies.Add(enemy);

            grabSocket = socket;
            snapOffset = offset;
            SessionState = GrabSessionState.Held;
        }

        // ─── Update Session ────────────────────────────────────
        // Seamlessly updates the grab socket and offset for all enemies
        // currently in this session. Used by multi-step grab combos.
        public void UpdateSession(Transform socket, Vector2 offset)
        {
            if (SessionState != GrabSessionState.Held) return;
            grabSocket = socket;
            snapOffset = offset;
        }

        // ─── Extend Hold ───────────────────────────────────────
        // Called on each ExecuteStep() to keep the enemy held through follow-ups.
        // Replaces the manual MaintainGrabThroughFollowUp() calls.
        public void ExtendHold(int additionalFrames, int recoveryFrames = 0)
        {
            if (SessionState != GrabSessionState.Held) return;

            foreach (var enemy in grabbedEnemies)
            {
                if (enemy != null && enemy.IsAlive)
                {
                    enemy.MaintainGrab(
                        AttackerId,
                        grabSocket,
                        snapOffset,
                        additionalFrames,
                        recoveryFrames);
                }
            }
        }

        // ─── Clean Release ─────────────────────────────────────
        // Normal end of throw: the slam hitbox's NormalHitbox reaction
        // handles the enemy's transition naturally.
        public void ReleaseClean()
        {
            SessionState = GrabSessionState.Releasing;
            // Don't call enemy.ReleaseGrab here — the slam hitbox's
            // NormalHitbox reaction will handle the transition naturally.
            ClearInternal();
        }

        // ─── Forced Release (Interrupt) ────────────────────────
        // Player got hit, cancelled, or exited. Safely eject the enemy
        // with a short knockdown reaction so it doesn't freeze in place.
        public void ForceRelease(Vector2 ejectDirection, float ejectSpeed = 3f)
        {
            foreach (var enemy in grabbedEnemies)
            {
                if (enemy != null && enemy.IsAlive)
                {
                    // 1. Release the grab binding on the enemy side
                    enemy.ReleaseGrab(AttackerId);

                    // 2. Clamp eject to X-axis only — never launch the enemy upward
                    //    on an interrupt. They should slide away and recover cleanly.
                    float ejectX = ejectDirection.x != 0f ? Mathf.Sign(ejectDirection.x) : 1f;
                    Vector2 horizontalEject = new Vector2(ejectX, 0f);

                    // 3. Apply a knockback reaction so the enemy cleanly
                    //    falls away instead of freezing at the socket position
                    HitReactionData knockdownData = new HitReactionData
                    {
                        damage = 0f,
                        knockbackDirection = horizontalEject,
                        reactionKind = EnemyReactionKind.Knockback,
                        reactionTiming = HitReactionData.CreateTiming(0, 8, 12),
                        horizontalSpeed = ejectSpeed,
                        launchSpeed = 0f,
                        landingBehavior = EnemyLandingBehavior.Recover,
                        canAirJuggle = false,
                        juggleLaunchScale = 1f,
                        juggleStunScale = 1f,
                        forceFaceAttacker = false,
                        flashColor = Color.white,
                        flashFrames = 0,
                        impactTier = DamageImpactTier.Low,
                        elementType = ElementType.None,
                        reactionOwnerId = AttackerId,
                    };

                    enemy.TakeHit(knockdownData, enemy.transform.position);
                }
            }
            ClearInternal();
        }

        // ─── Queries ───────────────────────────────────────────

        public bool HasAnyTargets()
        {
            // Clean up any destroyed null references
            grabbedEnemies.RemoveAll(e => e == null || !e.IsAlive);
            return grabbedEnemies.Count > 0 && SessionState == GrabSessionState.Held;
        }

        public bool HasTarget(EnemyController enemy)
        {
            return SessionState == GrabSessionState.Held
                && grabbedEnemies.Contains(enemy);
        }

        public List<EnemyController> GetGrabbedEnemies()
        {
            grabbedEnemies.RemoveAll(e => e == null || !e.IsAlive);
            return grabbedEnemies;
        }

        // ─── Legacy Compatibility ──────────────────────────────
        // These bridge methods keep existing callers working during migration.

        public void AddGrabbedTarget(EnemyController enemy)
        {
            if (enemy == null || grabbedEnemies.Contains(enemy)) return;
            grabbedEnemies.Add(enemy);
            // Note: BeginSession() should be called by HandleGrabConnected()
            // to properly start the session. This just adds to the target list.
            if (SessionState == GrabSessionState.Inactive)
                SessionState = GrabSessionState.Held;
        }

        /// <summary>
        /// Call this when the attacker gets hit/interrupted to cleanly drop all enemies.
        /// Legacy bridge — calls ForceRelease with a default eject.
        /// </summary>
        public void BreakGrab()
        {
            ForceRelease(Vector2.down, 2f);
        }

        public void ClearTargets()
        {
            ClearInternal();
        }

        // ─── Internal ──────────────────────────────────────────

        private void ClearInternal()
        {
            grabbedEnemies.Clear();
            grabSocket = null;
            snapOffset = Vector2.zero;
            SessionState = GrabSessionState.Inactive;
        }
    }
}
