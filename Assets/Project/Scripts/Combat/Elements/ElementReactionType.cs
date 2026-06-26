public enum ElementReactionType
{
    None,
    Stagger,    // Memory: unable to react for 8s
    Accumulate, // Echo: deal accumulated damage / 2 on expiry
    LostHP,     // Ash: deal damage based on enemy lost HP (capped 8% max HP)
    Explosion   // Void: AOE 150% ATK + Confusion 15s (or -30% Action Speed for Elites)
}
