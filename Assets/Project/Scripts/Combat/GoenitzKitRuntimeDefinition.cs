using UnityEngine;

[CreateAssetMenu(fileName = "GoenitzKitRuntime", menuName = "Game/Character/Goenitz Kit Runtime")]
public sealed class GoenitzKitRuntimeDefinition : CharacterKitRuntimeDefinition
{
    [Header("Action Identity")]
    public string transformationActionId = "goenitz.ultimate.blood_frenzy";
    public string ultimateActionId = "goenitz.ultimate.yamidokoku";
    public string finisherActionId = "goenitz.ultimate2.shin.yaotome";
    public string exSkillActionId = "goenitz.skill.ex_yonokaze";
    public string modeTag = "goenitz.blood_frenzy";
    public string sharedModeTag = "goenitz.blood_frenzy.shared";
    [Header("Aura Energy")]
    [Min(0.01f)] public float drainInterval = 1f;
    [Min(0.01f)] public float drainAmount = 3f;
    [Min(0f)] public float killExtension = 6f;
    [Min(0f)] public float exCriticalAuraEnergy = 5f;
    [Header("Recipes")]
    public RuntimeEffectRecipe frenzy = new RuntimeEffectRecipe();
    public RuntimeEffectRecipe evasion = new RuntimeEffectRecipe();
    public RuntimeEffectRecipe blizzard = new RuntimeEffectRecipe();
    public RuntimeEffectRecipe killAttack = new RuntimeEffectRecipe();
    public RuntimeEffectRecipe vulnerability = new RuntimeEffectRecipe();
    public RuntimeEffectRecipe frenzyBleed = new RuntimeEffectRecipe();
    public RuntimeEffectRecipe exBleed = new RuntimeEffectRecipe();
    public RuntimeEffectRecipe finisherBleed = new RuntimeEffectRecipe();
    [Header("Passive")]
    public PassiveEnemyElementModifierDefinition criticalPhysicalShred = new PassiveEnemyElementModifierDefinition
    {
        elementType = ElementType.None, resistancePercentAdd = -15f, permanent = true, stacks = false
    };

    public override ICharacterKitRuntime CreateRuntime(CharacterObject owner, TeamLoader team)
        => new GoenitzKitRuntime(owner, team, this);
}
