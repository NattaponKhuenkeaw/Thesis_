namespace CombatCore
{
    /// <summary>
    /// Fixed-step history for real player ground contact.
    /// Combat action context is intentionally owned by combat, not by this provider.
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

        /// <summary>Gameplay ground state follows contact exactly.</summary>
        public bool IsEffectivelyGrounded => IsGrounded;

        /// <summary>
        /// Updates the state. Called only by PlayerMovement in FixedUpdate.
        /// </summary>
        public void Tick(bool grounded, float verticalVelocity)
        {
            WasGrounded = IsGrounded;
            IsGrounded = grounded;
            VerticalVelocity = verticalVelocity;
        }
    }
}
