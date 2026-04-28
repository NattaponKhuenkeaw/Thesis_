using UnityEngine;

namespace CombatCore
{
    /// <summary>
    /// Single authority for the character's ground state.
    /// Updates once per FixedUpdate to prevent physics-vs-logic timing mismatches.
    /// All action logic should query this provider instead of decentralized IsGrounded() calls.
    /// </summary>
    public class GroundStateProvider
    {
        /// <summary>True if the physical ground check hit a collider.</summary>
        public bool IsGrounded { get; private set; }
        
        /// <summary>Physical ground state from the previous frame.</summary>
        public bool WasGrounded { get; private set; }
        
        /// <summary>Player just landed this frame.</summary>
        public bool JustLanded => IsGrounded && !WasGrounded;
        
        /// <summary>Player just left the ground this frame.</summary>
        public bool JustBecameAirborne => !IsGrounded && WasGrounded;
        
        /// <summary>Current Y velocity reported by Rigidbody.</summary>
        public float VerticalVelocity { get; private set; }
        
        /// <summary>True if actively in a jump startup/impulse session. Suppresses grounded logic.</summary>
        public bool IsInJumpSession { get; private set; }
        
        /// <summary>
        /// Effective ground state — accounts for physics hit but OVERRIDES to false if in a jump session.
        /// This prevents "grounded" events from firing while the player is starting a jump but the
        /// physics circle hasn't detached from the floor yet.
        /// </summary>
        public bool IsEffectivelyGrounded => IsGrounded && !IsInJumpSession;

        /// <summary>
        /// Updates the state. Called ONLY by PlayerMovement.CheckGround() in FixedUpdate.
        /// </summary>
        public void Tick(bool grounded, bool wasGrounded, float verticalVelocity, bool inJumpSession)
        {
            WasGrounded = wasGrounded;
            IsGrounded = grounded;
            VerticalVelocity = verticalVelocity;
            IsInJumpSession = inJumpSession;
        }
    }
}
