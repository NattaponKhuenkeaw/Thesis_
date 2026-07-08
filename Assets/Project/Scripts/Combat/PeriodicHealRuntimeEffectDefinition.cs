using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_PeriodicHeal", menuName = "Game/Runtime Effects/Periodic Heal", order = 12)]
public class PeriodicHealRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [HideInInspector]
    [Min(0.05f)] public float tickInterval = 1f;
    [HideInInspector]
    public RuntimeEffectStatTarget valueSource = RuntimeEffectStatTarget.Attack;
    [HideInInspector]
    [Min(0f)] public float valueRatio = 0.1f;
    [HideInInspector]
    [Min(0f)] public float flatValue;
    [HideInInspector]
    public bool useSourceStats = true;

    public override bool RequiresFormulaTicking => true;

    public override void OnUpdate(RuntimeEffectContext context)
    {
        if (context.instance == null)
        {
            return;
        }

        bool hasOverride = TryGetOverrideParameters(context, out var parameters);
        float interval = hasOverride ? parameters.tickInterval : tickInterval;

        if (context.instance.nextTickAt <= 0f)
        {
            context.instance.nextTickAt = Mathf.Max(0.05f, interval);
        }

        while (context.instance.elapsedSeconds >= context.instance.nextTickAt)
        {
            ApplyTickHeal(context, hasOverride, parameters);
            context.instance.nextTickAt += Mathf.Max(0.05f, interval);
        }
    }

    private bool TryGetOverrideParameters(RuntimeEffectContext context, out RuntimeEffectPeriodicHealParameters parameters)
    {
        RuntimeEffectParameterSet parameterSet = RuntimeEffectRuntimeUtility.GetParameters(context.instance);
        if (parameterSet != null && parameterSet.overridePeriodicHeal && parameterSet.periodicHeal != null)
        {
            parameters = parameterSet.periodicHeal;
            return true;
        }

        parameters = null;
        return false;
    }

    private void ApplyTickHeal(RuntimeEffectContext context, bool hasOverride, RuntimeEffectPeriodicHealParameters parameters)
    {
        bool useSource = hasOverride ? parameters.useSourceStats : useSourceStats;
        RuntimeEffectStatTarget source = hasOverride ? parameters.valueSource : valueSource;
        float ratio = hasOverride ? parameters.valueRatio : valueRatio;
        float flat = hasOverride ? parameters.flatValue : flatValue;

        CharacterStats stats = useSource ? context.sourceStats : context.targetStats;
        float amount = Mathf.Max(0f, flat + GetStat(stats, source) * Mathf.Max(0f, ratio));
        if (amount <= 0f)
        {
            return;
        }

        if (context.targetPlayer != null && context.targetPlayer.characterObject == context.targetCharacter)
        {
            context.targetPlayer.Heal(amount);
            return;
        }

        TeamLoader teamLoader = TeamLoader.Instance;
        if (teamLoader != null && context.targetCharacter != null)
        {
            float maxHp = teamLoader.GetCharacterMaxHP(context.targetCharacter);
            float currentHp = teamLoader.GetCharacterCurrentHP(context.targetCharacter);
            teamLoader.SetCharacterCurrentHP(context.targetCharacter, Mathf.Min(maxHp, currentHp + amount));
        }
    }
}
