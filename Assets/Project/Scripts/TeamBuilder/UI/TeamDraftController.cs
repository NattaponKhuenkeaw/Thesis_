using System.Collections.Generic;
using UnityEngine;
using Project.InventorySystem.Items;
using Project.ShopSystem;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Encapsulates the draft loadout state (characters and artifacts) for editing, caching, and restoring.
    /// </summary>
    public class TeamDraftController
    {
        private PlayerShopInventory playerInventory;
        private TeamLoader teamLoader;
        private TeamSlotUI[] teamSlots;

        public int stagedActiveIndex { get; set; }
        public bool hasDraftChanges { get; set; }

        private CharacterObject[] cachedTeamData;
        private Artifact[][] cachedArtifactsBySlot;
        private int cachedActiveIndex;

        private readonly Dictionary<CharacterObject, CharacterItem> runtimeCharacterItems = new Dictionary<CharacterObject, CharacterItem>();

        public TeamDraftController(PlayerShopInventory inventory, TeamLoader loader, TeamSlotUI[] slots)
        {
            playerInventory = inventory;
            teamLoader = loader;
            teamSlots = slots;
        }

        public void LoadRuntimeDraft()
        {
            if (playerInventory == null) return;

            CharacterObject[] existingTeam = teamLoader != null ? teamLoader.team : null;
            if (existingTeam == null) return;

            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                    teamSlots[i].ClearSlot();
            }

            stagedActiveIndex = teamLoader != null ? teamLoader.CurrentIndex : 0;

            for (int i = 0; i < teamSlots.Length && i < existingTeam.Length; i++)
            {
                if (teamSlots[i] == null) continue;

                CharacterObject charObj = existingTeam[i];
                if (charObj == null)
                {
                    teamSlots[i].ClearSlot();
                    continue;
                }

                CharacterItem matchingItem = FindCharacterItem(charObj);
                teamSlots[i].SetCharacter(matchingItem);

                if (teamLoader != null)
                {
                    Artifact[] runtimeArtifacts = teamLoader.GetEquippedArtifactsAt(i);
                    teamSlots[i].SetEquippedArtifacts(CloneArtifactArray(runtimeArtifacts));
                }
            }

            NormalizeStagedActiveIndex();
            hasDraftChanges = false;
            SaveDraftCache();
        }

        public void RestoreCachedDraft()
        {
            if (cachedTeamData == null)
            {
                LoadRuntimeDraft();
                return;
            }

            LoadDraftSnapshot(cachedTeamData, cachedArtifactsBySlot, cachedActiveIndex);
        }

        public void SaveDraftCache()
        {
            cachedTeamData = BuildDraftTeamData();
            cachedArtifactsBySlot = BuildDraftArtifactData();
            cachedActiveIndex = stagedActiveIndex;
        }

        public void ClearDraftCache()
        {
            cachedTeamData = null;
            cachedArtifactsBySlot = null;
            cachedActiveIndex = 0;
            hasDraftChanges = false;
        }

        public void LoadDraftSnapshot(CharacterObject[] teamData, Artifact[][] artifactsBySlot, int activeIndex)
        {
            if (playerInventory == null || teamData == null) return;

            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null)
                    teamSlots[i].ClearSlot();
            }

            stagedActiveIndex = activeIndex;

            for (int i = 0; i < teamSlots.Length && i < teamData.Length; i++)
            {
                if (teamSlots[i] == null) continue;

                CharacterObject charObj = teamData[i];
                if (charObj == null)
                {
                    teamSlots[i].ClearSlot();
                    continue;
                }

                CharacterItem matchingItem = FindCharacterItem(charObj);
                teamSlots[i].SetCharacter(matchingItem);

                if (artifactsBySlot != null && i < artifactsBySlot.Length)
                {
                    teamSlots[i].SetEquippedArtifacts(CloneArtifactArray(artifactsBySlot[i]));
                }
            }

            NormalizeStagedActiveIndex();
        }

        public CharacterItem FindCharacterItem(CharacterObject charObj)
        {
            if (charObj == null) return null;

            if (playerInventory != null)
            {
                foreach (var item in playerInventory.ownedItems)
                {
                    if (item is CharacterItem charItem && charItem.characterObjectData == charObj)
                    {
                        return charItem;
                    }
                }
            }

            if (runtimeCharacterItems.TryGetValue(charObj, out CharacterItem cachedItem) && cachedItem != null)
                return cachedItem;

            CharacterItem runtimeItem = ScriptableObject.CreateInstance<CharacterItem>();
            runtimeItem.characterObjectData = charObj;
            runtimeItem.SyncFromCharacterObjectData();
            runtimeCharacterItems[charObj] = runtimeItem;
            return runtimeItem;
        }

        public Dictionary<CharacterObject, CharacterItem>.ValueCollection GetRuntimeCharacterItems()
        {
            return runtimeCharacterItems.Values;
        }

        public CharacterObject[] BuildDraftTeamData()
        {
            CharacterObject[] teamData = new CharacterObject[teamSlots.Length];
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null && teamSlots[i].CurrentCharacter != null)
                    teamData[i] = teamSlots[i].CurrentCharacter.characterObjectData;
                else
                    teamData[i] = null;
            }
            return teamData;
        }

        public Artifact[][] BuildDraftArtifactData()
        {
            Artifact[][] artifactsBySlot = new Artifact[teamSlots.Length][];
            for (int i = 0; i < teamSlots.Length; i++)
            {
                artifactsBySlot[i] = teamSlots[i] != null
                    ? teamSlots[i].GetEquippedArtifacts()
                    : null;
            }
            return artifactsBySlot;
        }

        public void NormalizeStagedActiveIndex()
        {
            if (teamSlots == null || teamSlots.Length == 0)
            {
                stagedActiveIndex = 0;
                return;
            }

            if (stagedActiveIndex >= 0
                && stagedActiveIndex < teamSlots.Length
                && teamSlots[stagedActiveIndex] != null
                && teamSlots[stagedActiveIndex].CurrentCharacter != null)
            {
                return;
            }

            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null && teamSlots[i].CurrentCharacter != null)
                {
                    stagedActiveIndex = i;
                    return;
                }
            }

            stagedActiveIndex = 0;
        }

        private static Artifact[] CloneArtifactArray(Artifact[] source)
        {
            if (source == null) return null;
            Artifact[] clone = new Artifact[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = source[i];
            }
            return clone;
        }
    }
}
