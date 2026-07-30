using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;

public sealed class DeployedCharacterRuntime
{
    public CharacterObject Character { get; }
    public int SlotIndex { get; private set; }
    public CharacterStats EffectiveStats { get; private set; }
    public IReadOnlyList<Artifact> EquippedArtifacts => equippedArtifacts;
    public CombatRuntimeState CombatState { get; }
    public uint StatsRevision { get; private set; }

    private Artifact[] equippedArtifacts;

    public DeployedCharacterRuntime(
        CharacterObject character,
        int slotIndex,
        CharacterStats effectiveStats,
        Artifact[] artifacts,
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
        Artifact[] artifacts)
    {
        SlotIndex = slotIndex;
        EffectiveStats = effectiveStats;
        equippedArtifacts = CopyArtifacts(artifacts);
        StatsRevision++;
    }

    private static Artifact[] CopyArtifacts(Artifact[] artifacts)
    {
        if (artifacts == null || artifacts.Length == 0)
        {
            return Array.Empty<Artifact>();
        }

        Artifact[] snapshot = new Artifact[artifacts.Length];
        Array.Copy(artifacts, snapshot, artifacts.Length);
        return snapshot;
    }
}
