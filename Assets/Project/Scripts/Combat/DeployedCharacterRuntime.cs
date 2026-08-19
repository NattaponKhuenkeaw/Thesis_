using System;
using System.Collections.Generic;

public sealed class DeployedCharacterRuntime
{
    public CharacterObject Character { get; }
    public int SlotIndex { get; private set; }
    public CharacterStats EffectiveStats { get; private set; }
    public IReadOnlyList<ArtifactRuntimeSnapshot> EquippedArtifacts => equippedArtifacts;
    public CombatRuntimeState CombatState { get; }
    public uint StatsRevision { get; private set; }

    private ArtifactRuntimeSnapshot[] equippedArtifacts;

    public DeployedCharacterRuntime(
        CharacterObject character,
        int slotIndex,
        CharacterStats effectiveStats,
        ArtifactRuntimeSnapshot[] artifacts,
        CombatRuntimeState combatState)
    {
        Character = character;
        SlotIndex = slotIndex;
        EffectiveStats = effectiveStats;
        equippedArtifacts = CopyArtifacts(artifacts);
        CombatState = combatState ?? new CombatRuntimeState();
        StatsRevision = 1;
    }

    public void UpdateStats(
        int slotIndex,
        CharacterStats effectiveStats,
        ArtifactRuntimeSnapshot[] artifacts)
    {
        SlotIndex = slotIndex;
        EffectiveStats = effectiveStats;
        equippedArtifacts = CopyArtifacts(artifacts);
        StatsRevision++;
    }

    private static ArtifactRuntimeSnapshot[] CopyArtifacts(ArtifactRuntimeSnapshot[] artifacts)
    {
        if (artifacts == null || artifacts.Length == 0)
        {
            return Array.Empty<ArtifactRuntimeSnapshot>();
        }

        ArtifactRuntimeSnapshot[] snapshot = new ArtifactRuntimeSnapshot[artifacts.Length];
        Array.Copy(artifacts, snapshot, artifacts.Length);
        return snapshot;
    }
}
