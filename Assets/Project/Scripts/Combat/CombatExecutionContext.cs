/// <summary>Shared by a step and its projectiles; field sessions prevent stale hits from rearming QTEs.</summary>
public sealed class CombatExecutionContext
{
    public readonly PlayerCombat owner;
    public readonly CombatActionExecutionToken cast;
    public readonly CharacterObject character;
    public readonly int fieldSession;
    public readonly bool isQteIntro;
    public readonly CombatActionType actionType;
    public readonly string actionKey;
    public bool HasConfirmedHit { get; private set; }

    public CombatExecutionContext(PlayerCombat owner, CharacterObject character, int fieldSession, bool isQteIntro, CombatAction action = null, CombatActionExecutionToken cast = null)
    {
        this.owner = owner;
        this.cast = cast;
        this.character = character;
        this.fieldSession = fieldSession;
        this.isQteIntro = isQteIntro;
        actionType = action?.EffectiveActionType ?? CombatActionType.FromInput;
        actionKey = action?.ActionKey;
    }

    public bool IsOnField => owner != null && owner.FieldSession == fieldSession
        && owner.playerCharacter != null && !owner.playerCharacter.IsDead
        && owner.playerCharacter.characterObject == character;

    public void ConfirmHit() => HasConfirmedHit = true;

    public bool UsesUltimateHitFlash(AttackCategory inputCategory) => actionType == CombatActionType.Ultimate
        || (actionType == CombatActionType.FromInput && inputCategory == AttackCategory.Ultimate);
}
