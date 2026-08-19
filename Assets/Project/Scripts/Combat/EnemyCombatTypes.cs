/// <summary>
/// How an enemy attack interacts with player invulnerability.
/// BBEE-style: dash/dodge frames only beat Normal hits.
/// </summary>
public enum EnemyAttackProperty
{
    /// <summary>Blocked by dash i-frames / dodge frames.</summary>
    Normal = 0,
    /// <summary>Ignores dash i-frames. Needs true invuln, jump out, or reposition.</summary>
    Undodgeable = 1,
    /// <summary>Ignores dash i-frames and standard block. True invuln / avoid position.</summary>
    Grab = 2
}

/// <summary>
/// Defense layer for trash/elite enemies (not the same as boss pattern armor).
/// </summary>
public enum EnemyDefenseState
{
    /// <summary>Full hit reactions / juggle allowed.</summary>
    Normal = 0,
    /// <summary>Orange armor: reduced damage, no stagger until broken.</summary>
    Armored = 1,
    /// <summary>Brief post-break window (optional juice). Treated like Normal for reactions.</summary>
    Broken = 2
}

/// <summary>
/// Prefab-authored combat kit. Shared Behavior Graph reads this instead of inlined node values.
/// Enemy1 InterruptibleMelee, Enemy2 CommitMelee, Enemy3 RangedHold — variants of EnemyCore only.
/// </summary>
public enum EnemyCombatRole
{
    /// <summary>Melee. Hits cancel windup and swing.</summary>
    InterruptibleMelee = 0,
    /// <summary>Melee. From telegraph start until recovery, hits deal damage but cannot stagger.</summary>
    CommitMelee = 1,
    /// <summary>Ranged shot. Holds ground when the player walks in; does not kite.</summary>
    RangedHold = 2
}

/// <summary>
/// Visual style for attack telegraph icons.
/// </summary>
public enum AttackWarningStyle
{
    Normal = 0,
    /// <summary>Purple ! — undodgeable / grab tell.</summary>
    Undodgeable = 1
}

/// <summary>
/// Context passed from enemy hitboxes into player damage resolution.
/// </summary>
public struct IncomingAttackContext
{
    public EnemyAttackProperty property;
    public EnemyController sourceEnemy;

    public static IncomingAttackContext FromEnemy(EnemyController enemy, EnemyAttackProperty property)
    {
        return new IncomingAttackContext
        {
            sourceEnemy = enemy,
            property = property
        };
    }
}
