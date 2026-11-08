using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using Project.Save;
using UnityEngine;

namespace Project.Progression
{
    public enum ProgressionFragmentType
    {
        CharacterPotential,
        [Obsolete("Artifact fragments are retired. Retained only to deserialize legacy saves.")]
        ArtifactTranscendence
    }

    [Serializable]
    public class ProgressionFragmentStack
    {
        public string sourceItemId;
        public string displayName;
        public ProgressionFragmentType fragmentType;
        [Min(0)] public int quantity;
    }

    [Serializable]
    public class PermanentItemStack
    {
        public string itemId;
        public string displayName;
        [Min(0)] public int quantity = 1;
    }

    [Serializable]
    public class CharacterPotentialProgress
    {
        public string characterId;
        [Range(0, CharacterKitDefinition.MaxPotentialLevel)] public int skillLevel;
        [Range(0, CharacterKitDefinition.MaxPotentialLevel)] public int ultimateLevel;
        [Range(0, CharacterKitDefinition.MaxPotentialLevel)] public int passiveLevel;

        public int GetLevel(PotentialTrack track)
        {
            return track switch
            {
                PotentialTrack.Skill => skillLevel,
                PotentialTrack.Ultimate => ultimateLevel,
                PotentialTrack.Passive => passiveLevel,
                _ => 0
            };
        }

        public void SetLevel(PotentialTrack track, int level)
        {
            int clamped = Mathf.Clamp(level, 0, CharacterKitDefinition.MaxPotentialLevel);
            switch (track)
            {
                case PotentialTrack.Skill:
                    skillLevel = clamped;
                    break;
                case PotentialTrack.Ultimate:
                    ultimateLevel = clamped;
                    break;
                case PotentialTrack.Passive:
                    passiveLevel = clamped;
                    break;
            }
        }
    }

    [Serializable]
    internal class SavedPlayerMetaProgressionData
    {
        public int schemaVersion = 2;
        public int metaCurrency;
        public List<ProgressionFragmentStack> fragments = new List<ProgressionFragmentStack>();
        public List<CharacterPotentialProgress> characterPotentials = new List<CharacterPotentialProgress>();
        public List<CharacterMemoryProgress> characterMemories = new List<CharacterMemoryProgress>();
        public List<string> unlockedCharacterIds = new List<string>();
        public List<PermanentItemStack> permanentItems = new List<PermanentItemStack>();
    }

    public partial class CharacterProgressionState : ScriptableObject
    {
        protected sealed class UnlockedPotentialCacheEntry
        {
            public CharacterKitDefinition kit;
            public IReadOnlyList<PotentialNodeDefinition> nodes;
        }

        protected static string SaveKey => SaveSlotStore.ActiveMetaKey;
        /// <summary>Step 1 (Not Perma): meta save keeps fragments/potential only - not permanent dungeon items.</summary>
        protected const bool PersistPermanentItemsInMeta = false;

        [Header("Meta Currency (Lobby / Permanent Upgrades)")]
        [SerializeField] protected int metaCurrency;

        [Header("Character Potential Fragments")]
        [SerializeField] protected List<ProgressionFragmentStack> fragments = new List<ProgressionFragmentStack>();

        [Header("Permanent Character Potential")]
        [SerializeField] protected List<CharacterPotentialProgress> characterPotentials = new List<CharacterPotentialProgress>();

        [Header("Permanent Roster")]
        [SerializeField] protected List<string> unlockedCharacterIds = new List<string>();

        [Header("Permanent Items")]
        [SerializeField] protected List<PermanentItemStack> permanentItems = new List<PermanentItemStack>();

        [NonSerialized] protected bool hasLoadedPersistentState;
        [NonSerialized] protected bool isApplyingPersistentState;
        [NonSerialized] protected string committedJson;
        [NonSerialized] protected Dictionary<CharacterObject, UnlockedPotentialCacheEntry> unlockedPotentialCache;

        public IReadOnlyList<ProgressionFragmentStack> Fragments => fragments;
        public IReadOnlyList<CharacterPotentialProgress> CharacterPotentials => characterPotentials;
        public IReadOnlyList<string> UnlockedCharacterIds => unlockedCharacterIds;
        public IReadOnlyList<PermanentItemStack> PermanentItems => permanentItems;
        public int MetaCurrency => Mathf.Max(0, metaCurrency);
        public event Action<string> OnPotentialChanged;
        public event Action OnFragmentsChanged;

        public static bool HasPersistentSave()
        {
            SaveSlotStore.EnsureMigrated();
            return PlayerPrefs.HasKey(SaveKey);
        }

        public bool CanAffordMetaCurrency(int amount)
        {
            return amount <= 0 || MetaCurrency >= amount;
        }

        public void AddMetaCurrency(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            EnsureData();
            metaCurrency = Mathf.Max(0, metaCurrency + amount);
            SavePersistentState();
        }

        public bool TrySpendMetaCurrency(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            EnsureData();
            if (metaCurrency < amount)
            {
                return false;
            }

            metaCurrency -= amount;
            SavePersistentState();
            return true;
        }

        protected virtual void OnEnable()
        {
            InvalidatePotentialCache();
            if (ShouldUsePersistentStorage())
            {
                LoadPersistentStateIfNeeded();
                return;
            }

            EnsureData();
        }

        public int GetFragmentCount(BaseItem sourceItem)
        {
            if (sourceItem is ArtifactDefinition) return 0;
            string sourceId = GetSourceId(sourceItem);
            if (string.IsNullOrEmpty(sourceId))
            {
                return 0;
            }

            ProgressionFragmentStack stack = FindFragmentStack(sourceId, GetFragmentType(sourceItem));
            return stack != null ? stack.quantity : 0;
        }

        public int GetFragmentCount(string sourceId, ProgressionFragmentType fragmentType = ProgressionFragmentType.CharacterPotential)
        {
            if (fragmentType == ProgressionFragmentType.ArtifactTranscendence) return 0;
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return 0;
            }

            ProgressionFragmentStack stack = FindFragmentStack(sourceId, fragmentType);
            return stack != null ? stack.quantity : 0;
        }

        public void AddFragments(BaseItem sourceItem, int amount)
        {
            if (sourceItem == null || sourceItem is ArtifactDefinition || amount <= 0)
            {
                return;
            }

            string sourceId = GetSourceId(sourceItem);
            if (string.IsNullOrEmpty(sourceId))
            {
                return;
            }

            ProgressionFragmentType fragmentType = GetFragmentType(sourceItem);
            ProgressionFragmentStack stack = FindFragmentStack(sourceId, fragmentType);
            if (stack == null)
            {
                stack = new ProgressionFragmentStack
                {
                    sourceItemId = sourceId,
                    displayName = BuildFragmentDisplayName(sourceItem),
                    fragmentType = fragmentType,
                    quantity = 0
                };
                fragments.Add(stack);
            }

            stack.displayName = BuildFragmentDisplayName(sourceItem);
            stack.quantity = Mathf.Max(0, stack.quantity + amount);
            SavePersistentState();
            NotifyFragmentsChanged();
        }

        public bool TrySpendFragments(BaseItem sourceItem, int amount)
        {
            if (sourceItem == null || sourceItem is ArtifactDefinition || amount <= 0)
            {
                return false;
            }

            ProgressionFragmentStack stack = FindFragmentStack(GetSourceId(sourceItem), GetFragmentType(sourceItem));
            if (stack == null || stack.quantity < amount)
            {
                return false;
            }

            stack.quantity -= amount;
            NotifyFragmentsChanged();
            return true;
        }

        public bool TryRemoveFragments(BaseItem sourceItem, int amount)
        {
            return TrySpendFragments(sourceItem, amount);
        }

        public bool TrySpendFragments(string sourceId, ProgressionFragmentType fragmentType, int amount)
        {
            if (fragmentType == ProgressionFragmentType.ArtifactTranscendence
                || string.IsNullOrWhiteSpace(sourceId) || amount <= 0)
            {
                return false;
            }

            ProgressionFragmentStack stack = FindFragmentStack(sourceId, fragmentType);
            if (stack == null || stack.quantity < amount)
            {
                return false;
            }

            stack.quantity -= amount;
            NotifyFragmentsChanged();
            return true;
        }

        public void ClearFragments()
        {
            fragments.Clear();
            SavePersistentState();
            NotifyFragmentsChanged();
        }

        private void NotifyFragmentsChanged()
        {
            OnFragmentsChanged?.Invoke();
        }

        public void ClearAllProgression()
        {
            EnsureData();
            fragments.Clear();
            characterPotentials.Clear();
            _characterMemories.Clear();
            unlockedCharacterIds.Clear();
            permanentItems.Clear();
            metaCurrency = 0;
            InvalidatePotentialCache();
            SavePersistentState();
            OnPotentialChanged?.Invoke(null);
            OnMemoryChanged?.Invoke(null);
            NotifyFragmentsChanged();
        }

        public int AddPermanentItem(BaseItem item, int amount = 1)
        {
            if (!PersistPermanentItemsInMeta)
            {
                return 0;
            }

            if (item == null || amount <= 0)
            {
                return 0;
            }

            string itemId = GetSourceId(item);
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return 0;
            }

            EnsureData();
            PermanentItemStack stack = FindPermanentItemStack(itemId);
            if (stack == null)
            {
                stack = new PermanentItemStack
                {
                    itemId = itemId,
                    displayName = BuildPermanentItemDisplayName(item),
                    quantity = 0
                };
                permanentItems.Add(stack);
            }

            int previousQuantity = Mathf.Max(0, stack.quantity);
            int requestedAmount = Mathf.Max(1, amount);
            stack.displayName = BuildPermanentItemDisplayName(item);
            stack.quantity = item is Item
                ? Mathf.Max(previousQuantity, requestedAmount)
                : Mathf.Max(previousQuantity, 1);

            int added = Mathf.Max(0, stack.quantity - previousQuantity);
            if (added > 0)
            {
                SavePersistentState();
            }

            return added;
        }

        public bool IsCharacterUnlocked(CharacterObject character)
        {
            return character != null && IsCharacterUnlocked(character.characterId);
        }

        public bool IsCharacterUnlocked(CharacterItem characterItem)
        {
            return characterItem != null && IsCharacterUnlocked(characterItem.characterObjectData);
        }

        public bool IsCharacterUnlocked(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            EnsureData();
            return unlockedCharacterIds.Contains(characterId);
        }

        public bool UnlockCharacter(CharacterObject character)
        {
            if (character == null)
            {
                return false;
            }

            return UnlockCharacter(character.characterId);
        }

        public bool UnlockCharacter(CharacterItem characterItem)
        {
            if (characterItem == null)
            {
                return false;
            }

            return UnlockCharacter(characterItem.characterObjectData);
        }

        public bool UnlockCharacter(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            EnsureData();
            if (unlockedCharacterIds.Contains(characterId))
            {
                return false;
            }

            unlockedCharacterIds.Add(characterId);
            SavePersistentState();
            return true;
        }

        public int GetPotentialLevel(CharacterObject character, PotentialTrack track)
        {
            return character != null ? GetPotentialLevel(character.characterId, track) : 0;
        }

        public int GetPotentialLevel(string characterId, PotentialTrack track)
        {
            CharacterPotentialProgress progress = FindPotentialProgress(characterId, createIfMissing: false);
            return progress != null ? progress.GetLevel(track) : 0;
        }

        public bool SetPotentialLevel(CharacterObject character, PotentialTrack track, int level)
        {
            if (character == null)
            {
                return false;
            }

            return SetPotentialLevel(character.characterId, track, level);
        }

        public bool SetPotentialLevel(string characterId, PotentialTrack track, int level)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            CharacterPotentialProgress progress = FindPotentialProgress(characterId, createIfMissing: true);
            int before = progress.GetLevel(track);
            progress.SetLevel(track, level);
            bool changed = before != progress.GetLevel(track);
            if (changed)
            {
                InvalidatePotentialCache(characterId);
                OnPotentialChanged?.Invoke(characterId);
            }

            return changed;
        }

        public bool ResetPotentialLevels(CharacterObject character)
        {
            return character != null && ResetPotentialLevels(character.characterId);
        }

        public bool ResetPotentialLevels(string characterId)
        {
            CharacterPotentialProgress progress = FindPotentialProgress(characterId, createIfMissing: false);
            if (progress == null
                || (progress.skillLevel == 0 && progress.ultimateLevel == 0 && progress.passiveLevel == 0))
            {
                return false;
            }

            progress.skillLevel = 0;
            progress.ultimateLevel = 0;
            progress.passiveLevel = 0;
            InvalidatePotentialCache(characterId);
            SavePersistentState();
            OnPotentialChanged?.Invoke(characterId);
            return true;
        }

        public bool CanUpgradePotential(CharacterObject character, PotentialTrack track, out PotentialNodeDefinition nextNode)
        {
            nextNode = GetNextPotentialNode(character, track);
            if (character == null || nextNode == null)
            {
                return false;
            }

            int cost = Mathf.Max(0, nextNode.fragmentCost);
            return cost == 0 || GetFragmentCount(character.characterId) >= cost;
        }

        public bool TryUpgradePotential(CharacterObject character, PotentialTrack track)
        {
            if (!CanUpgradePotential(character, track, out PotentialNodeDefinition nextNode))
            {
                return false;
            }

            int cost = Mathf.Max(0, nextNode.fragmentCost);
            if (cost > 0 && !TrySpendFragments(character.characterId, ProgressionFragmentType.CharacterPotential, cost))
            {
                return false;
            }

            SetPotentialLevel(character.characterId, track, nextNode.level);
            return true;
        }

        public PotentialNodeDefinition GetNextPotentialNode(CharacterObject character, PotentialTrack track)
        {
            if (character == null || character.kitDefinition == null || MainMemoryKit.AppliesTo(character))
            {
                return null;
            }

            int currentLevel = GetPotentialLevel(character, track);
            if (currentLevel >= CharacterKitDefinition.MaxPotentialLevel)
            {
                return null;
            }

            int nextLevel = Mathf.Clamp(currentLevel + 1, 1, CharacterKitDefinition.MaxPotentialLevel);
            PotentialNodeDefinition bestNode = null;
            foreach (PotentialNodeDefinition node in character.kitDefinition.EnumerateAllNodes(
                includeBasePassive: false,
                includeBaseSkill: false,
                includeBaseUltimate: false))
            {
                if (node == null || node.track != track || node.level != nextLevel)
                {
                    continue;
                }

                if (bestNode == null || node.fragmentCost < bestNode.fragmentCost)
                {
                    bestNode = node;
                }
            }

            return bestNode ?? new PotentialNodeDefinition
            {
                nodeId = $"{character.characterId}.{track}.lv{nextLevel}",
                nodeName = $"{track} Lv.{nextLevel}",
                track = track,
                level = nextLevel,
                fragmentCost = 1
            };
        }

        public IReadOnlyList<PotentialNodeDefinition> GetUnlockedPotentialNodes(CharacterObject character)
        {
            if (character == null || character.kitDefinition == null)
            {
                return Array.Empty<PotentialNodeDefinition>();
            }

            unlockedPotentialCache ??=
                new Dictionary<CharacterObject, UnlockedPotentialCacheEntry>();
            if (unlockedPotentialCache.TryGetValue(character, out UnlockedPotentialCacheEntry cached)
                && cached != null
                && cached.kit == character.kitDefinition
                && cached.nodes != null)
            {
                return cached.nodes;
            }

            List<PotentialNodeDefinition> unlockedNodes = new List<PotentialNodeDefinition>();
            foreach (PotentialNodeDefinition node in character.kitDefinition.EnumerateAllNodes())
            {
                if (IsPotentialNodeUnlocked(character, node))
                {
                    unlockedNodes.Add(node);
                }
            }

            IReadOnlyList<PotentialNodeDefinition> result = unlockedNodes.AsReadOnly();
            unlockedPotentialCache[character] = new UnlockedPotentialCacheEntry
            {
                kit = character.kitDefinition,
                nodes = result
            };
            return result;
        }

        public bool IsPotentialNodeUnlocked(CharacterObject character, PotentialNodeDefinition node)
        {
            if (character == null || node == null)
            {
                return false;
            }

            if (node.unlockedByDefault || node.level <= 0)
            {
                return true;
            }

            return GetCombatPotentialLevel(character, node.track) >= node.level;
        }

        public void LoadPersistentStateIfNeeded()
        {
            if (!ShouldUsePersistentStorage())
            {
                EnsureData();
                return;
            }

            if (hasLoadedPersistentState)
            {
                return;
            }

            SaveSlotStore.EnsureMigrated();
            hasLoadedPersistentState = true;
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                ClearLoadedState();
                return;
            }

            int retiredArtifactFragments = ImportJsonInternal(json);
            SavedPlayerMetaProgressionData loaded = JsonUtility.FromJson<SavedPlayerMetaProgressionData>(json);
            if (retiredArtifactFragments > 0 || loaded == null || loaded.schemaVersion < 2)
            {
                Debug.Log($"PlayerMetaProgression: Migrated slot save to v2 and omitted {retiredArtifactFragments} retired Artifact fragment(s). Character Potential data was preserved.");
                SavePersistentState();
            }

            MarkCommitted();
        }

        private void ClearLoadedState()
        {
            EnsureData();
            fragments.Clear();
            characterPotentials.Clear();
            _characterMemories.Clear();
            unlockedCharacterIds.Clear();
            permanentItems.Clear();
            metaCurrency = 0;
            InvalidatePotentialCache();
            MarkCommitted();
        }

        public void MarkCommitted()
        {
            committedJson = ExportJson();
        }

        public void RevertToCommitted()
        {
            if (string.IsNullOrWhiteSpace(committedJson))
            {
                return;
            }

            RestoreRunCheckpoint(committedJson);
            SavePersistentState();
        }

        public void ReloadPersistentState()
        {
            hasLoadedPersistentState = false;
            LoadPersistentStateIfNeeded();
        }

        public void SavePersistentState()
        {
            if (!ShouldUsePersistentStorage() || isApplyingPersistentState)
            {
                return;
            }

            SaveSlotStore.EnsureMigrated();
            PlayerPrefs.SetString(SaveKey, ExportJson());
            PlayerPrefs.Save();
        }

        public string ExportJson()
        {
            EnsureData();
            SavedPlayerMetaProgressionData data = new SavedPlayerMetaProgressionData
            {
                schemaVersion = 2,
                metaCurrency = Mathf.Max(0, metaCurrency),
                characterMemories = _characterMemories
            };

            for (int i = 0; i < fragments.Count; i++)
            {
                ProgressionFragmentStack stack = fragments[i];
                if (stack == null || stack.quantity <= 0 || string.IsNullOrWhiteSpace(stack.sourceItemId))
                {
                    continue;
                }

                data.fragments.Add(new ProgressionFragmentStack
                {
                    sourceItemId = stack.sourceItemId,
                    displayName = stack.displayName,
                    fragmentType = stack.fragmentType,
                    quantity = stack.quantity
                });
            }

            for (int i = 0; i < characterPotentials.Count; i++)
            {
                CharacterPotentialProgress progress = characterPotentials[i];
                if (progress == null || string.IsNullOrWhiteSpace(progress.characterId))
                {
                    continue;
                }

                data.characterPotentials.Add(new CharacterPotentialProgress
                {
                    characterId = progress.characterId,
                    skillLevel = Mathf.Clamp(progress.skillLevel, 0, CharacterKitDefinition.MaxPotentialLevel),
                    ultimateLevel = Mathf.Clamp(progress.ultimateLevel, 0, CharacterKitDefinition.MaxPotentialLevel),
                    passiveLevel = Mathf.Clamp(progress.passiveLevel, 0, CharacterKitDefinition.MaxPotentialLevel)
                });
            }

            for (int i = 0; i < unlockedCharacterIds.Count; i++)
            {
                string characterId = unlockedCharacterIds[i];
                if (!string.IsNullOrWhiteSpace(characterId) && !data.unlockedCharacterIds.Contains(characterId))
                {
                    data.unlockedCharacterIds.Add(characterId);
                }
            }

            for (int i = 0; i < permanentItems.Count; i++)
            {
                if (!PersistPermanentItemsInMeta)
                {
                    break;
                }

                PermanentItemStack stack = permanentItems[i];
                if (stack == null || stack.quantity <= 0 || string.IsNullOrWhiteSpace(stack.itemId))
                {
                    continue;
                }

                data.permanentItems.Add(new PermanentItemStack
                {
                    itemId = stack.itemId,
                    displayName = stack.displayName,
                    quantity = Mathf.Max(0, stack.quantity)
                });
            }

            return JsonUtility.ToJson(data);
        }

        public void ImportJson(string json)
        {
            ImportJsonInternal(json);
        }

        private int ImportJsonInternal(string json, bool preserveMemory = false)
        {
            EnsureData();
            isApplyingPersistentState = true;
            int retiredArtifactFragments = 0;
            metaCurrency = 0;
            fragments.Clear();
            characterPotentials.Clear();
            if (!preserveMemory) _characterMemories.Clear();
            unlockedCharacterIds.Clear();
            permanentItems.Clear();

            if (!string.IsNullOrWhiteSpace(json))
            {
                SavedPlayerMetaProgressionData data = JsonUtility.FromJson<SavedPlayerMetaProgressionData>(json);
                if (data != null)
                {
                    metaCurrency = Mathf.Max(0, data.metaCurrency);
                    if (!preserveMemory) ImportMemory(data.characterMemories);

                    if (data.fragments != null)
                    {
                        for (int i = 0; i < data.fragments.Count; i++)
                        {
                            ProgressionFragmentStack stack = data.fragments[i];
                            if (stack == null || stack.quantity <= 0 || string.IsNullOrWhiteSpace(stack.sourceItemId))
                            {
                                continue;
                            }

                            if (stack.fragmentType == ProgressionFragmentType.ArtifactTranscendence)
                            {
                                retiredArtifactFragments += Mathf.Max(0, stack.quantity);
                                continue;
                            }

                            fragments.Add(new ProgressionFragmentStack
                            {
                                sourceItemId = stack.sourceItemId,
                                displayName = stack.displayName,
                                fragmentType = stack.fragmentType,
                                quantity = Mathf.Max(0, stack.quantity)
                            });
                        }
                    }

                    if (data.characterPotentials != null)
                    {
                        for (int i = 0; i < data.characterPotentials.Count; i++)
                        {
                            CharacterPotentialProgress progress = data.characterPotentials[i];
                            if (progress == null || string.IsNullOrWhiteSpace(progress.characterId))
                            {
                                continue;
                            }

                            characterPotentials.Add(new CharacterPotentialProgress
                            {
                                characterId = progress.characterId,
                                skillLevel = Mathf.Clamp(progress.skillLevel, 0, CharacterKitDefinition.MaxPotentialLevel),
                                ultimateLevel = Mathf.Clamp(progress.ultimateLevel, 0, CharacterKitDefinition.MaxPotentialLevel),
                                passiveLevel = Mathf.Clamp(progress.passiveLevel, 0, CharacterKitDefinition.MaxPotentialLevel)
                            });
                        }
                    }

                    if (data.unlockedCharacterIds != null)
                    {
                        for (int i = 0; i < data.unlockedCharacterIds.Count; i++)
                        {
                            string characterId = data.unlockedCharacterIds[i];
                            if (!string.IsNullOrWhiteSpace(characterId) && !unlockedCharacterIds.Contains(characterId))
                            {
                                unlockedCharacterIds.Add(characterId);
                            }
                        }
                    }

                    if (data.permanentItems != null && PersistPermanentItemsInMeta)
                    {
                        for (int i = 0; i < data.permanentItems.Count; i++)
                        {
                            PermanentItemStack stack = data.permanentItems[i];
                            if (stack == null || stack.quantity <= 0 || string.IsNullOrWhiteSpace(stack.itemId))
                            {
                                continue;
                            }

                            PermanentItemStack existingStack = FindPermanentItemStack(stack.itemId);
                            if (existingStack == null)
                            {
                                permanentItems.Add(new PermanentItemStack
                                {
                                    itemId = stack.itemId,
                                    displayName = stack.displayName,
                                    quantity = Mathf.Max(0, stack.quantity)
                                });
                            }
                            else
                            {
                                existingStack.displayName = !string.IsNullOrWhiteSpace(stack.displayName)
                                    ? stack.displayName
                                    : existingStack.displayName;
                                existingStack.quantity = Mathf.Max(existingStack.quantity, stack.quantity);
                            }
                        }
                    }
                }
            }

            isApplyingPersistentState = false;
            InvalidatePotentialCache();
            OnPotentialChanged?.Invoke(null);
            if (!preserveMemory) OnMemoryChanged?.Invoke(null);
            NotifyFragmentsChanged();
            return retiredArtifactFragments;
        }

        protected void InvalidatePotentialCache(string characterId = null)
        {
            if (unlockedPotentialCache == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(characterId))
            {
                unlockedPotentialCache.Clear();
                return;
            }

            List<CharacterObject> matches = null;
            foreach (CharacterObject character in unlockedPotentialCache.Keys)
            {
                if (character != null
                    && string.Equals(character.characterId, characterId, StringComparison.Ordinal))
                {
                    matches ??= new List<CharacterObject>();
                    matches.Add(character);
                }
            }

            if (matches == null)
            {
                return;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                unlockedPotentialCache.Remove(matches[i]);
            }
        }

        private ProgressionFragmentStack FindFragmentStack(string sourceId, ProgressionFragmentType fragmentType)
        {
            EnsureData();
            if (fragments == null)
            {
                fragments = new List<ProgressionFragmentStack>();
            }

            for (int i = fragments.Count - 1; i >= 0; i--)
            {
                ProgressionFragmentStack stack = fragments[i];
                if (stack == null || stack.quantity <= 0 || string.IsNullOrEmpty(stack.sourceItemId))
                {
                    fragments.RemoveAt(i);
                    continue;
                }

                if (string.Equals(stack.sourceItemId, sourceId, StringComparison.Ordinal)
                    && stack.fragmentType == fragmentType)
                {
                    return stack;
                }
            }

            return null;
        }

        private PermanentItemStack FindPermanentItemStack(string itemId)
        {
            EnsureData();
            if (permanentItems == null)
            {
                permanentItems = new List<PermanentItemStack>();
            }

            for (int i = permanentItems.Count - 1; i >= 0; i--)
            {
                PermanentItemStack stack = permanentItems[i];
                if (stack == null || stack.quantity <= 0 || string.IsNullOrWhiteSpace(stack.itemId))
                {
                    permanentItems.RemoveAt(i);
                    continue;
                }

                if (string.Equals(stack.itemId, itemId, StringComparison.Ordinal))
                {
                    return stack;
                }
            }

            return null;
        }

        protected CharacterPotentialProgress FindPotentialProgress(string characterId, bool createIfMissing)
        {
            EnsureData();
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            for (int i = characterPotentials.Count - 1; i >= 0; i--)
            {
                CharacterPotentialProgress progress = characterPotentials[i];
                if (progress == null || string.IsNullOrWhiteSpace(progress.characterId))
                {
                    characterPotentials.RemoveAt(i);
                    continue;
                }

                if (string.Equals(progress.characterId, characterId, StringComparison.Ordinal))
                {
                    return progress;
                }
            }

            if (!createIfMissing)
            {
                return null;
            }

            CharacterPotentialProgress created = new CharacterPotentialProgress
            {
                characterId = characterId
            };
            characterPotentials.Add(created);
            return created;
        }

        private void EnsureData()
        {
            fragments ??= new List<ProgressionFragmentStack>();
            fragments.RemoveAll(stack => stack != null && stack.fragmentType == ProgressionFragmentType.ArtifactTranscendence);
            characterPotentials ??= new List<CharacterPotentialProgress>();
            unlockedCharacterIds ??= new List<string>();
            permanentItems ??= new List<PermanentItemStack>();
        }

        private static ProgressionFragmentType GetFragmentType(BaseItem sourceItem)
        {
            return sourceItem is ArtifactDefinition
                ? ProgressionFragmentType.ArtifactTranscendence
                : ProgressionFragmentType.CharacterPotential;
        }

        private static string BuildFragmentDisplayName(BaseItem sourceItem)
        {
            string itemName = !string.IsNullOrWhiteSpace(sourceItem.itemName)
                ? sourceItem.itemName
                : sourceItem.name;

            return $"{itemName} Fragment";
        }

        private static string BuildPermanentItemDisplayName(BaseItem item)
        {
            return !string.IsNullOrWhiteSpace(item.itemName) ? item.itemName : item.name;
        }

        private static string GetSourceId(BaseItem sourceItem)
        {
            if (sourceItem == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(sourceItem.id))
            {
                return sourceItem.id;
            }

            return sourceItem.name;
        }

        protected virtual bool PersistenceEnabled => false;
        protected virtual bool IsPersistentAsset => false;

        protected void NotifyPotentialLevelsChanged()
        {
            InvalidatePotentialCache();
            OnPotentialChanged?.Invoke(null);
        }

        private bool ShouldUsePersistentStorage()
        {
            if (!PersistenceEnabled) return false;
            if (!Application.isPlaying)
            {
                return false;
            }

            return IsPersistentAsset;
        }
    }
}
