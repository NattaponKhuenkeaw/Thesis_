using UnityEngine;

public abstract class CharacterKitRuntimeDefinition : ScriptableObject
{
    public abstract ICharacterKitRuntime CreateRuntime(CharacterObject owner, TeamLoader team);
}
