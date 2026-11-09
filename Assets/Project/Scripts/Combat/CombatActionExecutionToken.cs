using System.Collections.Generic;

/// <summary>Cast identity survives step changes and off-field projectile delivery.</summary>
public sealed class CombatActionExecutionToken
{
    public readonly CharacterObject owner;
    public readonly CombatAction action;
    public CharacterObject rememberedModeOwner;
    public uint rememberedModeEpoch;
    public bool modeEligibilityPending;
    public bool projectileDuplicationRolled;
    private HashSet<ulong> _payloadTargets;
    private HashSet<RuntimeEffectRecipeData> _triggeredRecipes;
    private HashSet<string> artifactInfusionRolls;
    public ElementType? ArtifactInfusion { get; set; }

    public CombatActionExecutionToken(CharacterObject owner, CombatAction action)
    {
        this.owner = owner;
        this.action = action;
    }

    public bool ClaimPayload(ulong enemyLife)
    {
        _payloadTargets ??= new HashSet<ulong>();
        return _payloadTargets.Add(enemyLife);
    }

    public bool ClaimRecipe(RuntimeEffectRecipeData recipe)
    {
        _triggeredRecipes ??= new HashSet<RuntimeEffectRecipeData>();
        return _triggeredRecipes.Add(recipe);
    }

    public bool ClaimArtifactInfusionRoll(string instanceId)
    {
        artifactInfusionRolls ??= new HashSet<string>();
        return artifactInfusionRolls.Add(instanceId);
    }
}
