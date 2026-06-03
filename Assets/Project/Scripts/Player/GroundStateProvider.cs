using UnityEngine;

namespace CombatCore
{
    public enum GroundStateAuthority
    {
        Physical,
        StickyGrounded,
        ForceAirborne
    }

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
        /// Runtime override that lets a state guardrail decide whether physical ground separation
        /// should count as airborne for gameplay logic.
        /// </summary>
        public GroundStateAuthority Authority { get; private set; }

        /// <summary>
        /// Effective ground state. Jump startup always wins first, then state-owned authority,
        /// then the raw physical overlap result.
        /// </summary>
        public bool IsEffectivelyGrounded
        {
            get
            {
                if (IsInJumpSession)
                {
                    return false;
                }

                return Authority switch
                {
                    GroundStateAuthority.StickyGrounded => true,
                    GroundStateAuthority.ForceAirborne => false,
                    _ => IsGrounded
                };
            }
        }

        /// <summary>
        /// Updates the state. Called ONLY by PlayerMovement.CheckGround() in FixedUpdate.
        /// </summary>
        public void Tick(bool grounded, bool wasGrounded, float verticalVelocity, bool inJumpSession)
        {
            WasGrounded = wasGrounded;
            IsGrounded = grounded;
            VerticalVelocity = verticalVelocity;
            IsInJumpSession = inJumpSession;

            if (Authority == GroundStateAuthority.StickyGrounded && grounded && !inJumpSession)
            {
                Authority = GroundStateAuthority.Physical;
            }
            else if (Authority == GroundStateAuthority.StickyGrounded && inJumpSession)
            {
                Authority = GroundStateAuthority.Physical;
            }
        }

        public void SetAuthority(GroundStateAuthority authority)
        {
            Authority = authority;
        }

        public void ClearAuthority()
        {
            Authority = GroundStateAuthority.Physical;
        }
    }
}
