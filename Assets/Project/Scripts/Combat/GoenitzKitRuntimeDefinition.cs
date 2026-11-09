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
    [Header("Blood Frenzy Ability UI")]
    [Tooltip("Optional base Normal Attack icon shown while Goenitz's Blood Frenzy is active.")]
    public Sprite frenzyNormalAttackIcon;
    [Tooltip("Optional base Skill icon shown while Goenitz's Blood Frenzy is active.")]
    public Sprite frenzySkillIcon;
    [Tooltip("Optional base Ultimate icon shown while Goenitz's Blood Frenzy is active.")]
    public Sprite frenzyUltimateIcon;
    [Tooltip("Optional base Dash icon shown while Goenitz's Blood Frenzy is active.")]
    public Sprite frenzyDashIcon;
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
    public RuntimeEffectRecipe frenzySpeed = new RuntimeEffectRecipe();
    public RuntimeEffectRecipe frenzyEntryBuff = new RuntimeEffectRecipe();
    public RuntimeEffectRecipe frenzyEntryBleed = new RuntimeEffectRecipe();
    [Header("Passive")]
    public RuntimeEffectRecipe criticalDamageResistanceShred = new RuntimeEffectRecipe();

    public override ICharacterKitRuntime CreateRuntime(CharacterObject owner, TeamLoader team)
        => new GoenitzKitRuntime(owner, team, this);
}
