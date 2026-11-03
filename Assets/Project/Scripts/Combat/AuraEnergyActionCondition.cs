using System;
using UnityEngine;

[Serializable]
public sealed class AuraEnergyActionCondition : ActionConditionBase
{
    [Min(0f)] public float minimumExclusive;
    public override ConditionCategory Category => ConditionCategory.Resource;
    public override bool IsMet(InputContext context)
    {
        TeamLoader team = TeamLoader.Instance;
        CharacterObject actor = context.passiveController?.playerCharacter?.characterObject ?? team?.CurrentCharacter;
        return team != null && actor != null && team.GetCharacterCurrentAE(actor) > minimumExclusive;
    }
}
