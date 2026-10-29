/// <summary>Shared by a step and its projectiles; field sessions prevent stale hits from rearming QTEs.</summary>
public sealed class CombatExecutionContext
{
    public readonly PlayerCombat owner;
    public readonly CharacterObject character;
    public readonly int fieldSession;
    public readonly bool isQteIntro;
    public bool HasConfirmedHit { get; private set; }

    public CombatExecutionContext(PlayerCombat owner, CharacterObject character, int fieldSession, bool isQteIntro)
    {
        this.owner = owner;
        this.character = character;
        this.fieldSession = fieldSession;
        this.isQteIntro = isQteIntro;
    }

    public bool IsOnField => owner != null && owner.FieldSession == fieldSession
        && owner.playerCharacter != null && !owner.playerCharacter.IsDead
        && owner.playerCharacter.characterObject == character;

    public void ConfirmHit() => HasConfirmedHit = true;
}
