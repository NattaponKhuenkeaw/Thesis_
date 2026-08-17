using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
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
        public List<ProgressionFragmentStack> fragments = new List<ProgressionFragmentStack>();
        public List<CharacterPotentialProgress> characterPotentials = new List<CharacterPotentialProgress>();
    }

    [CreateAssetMenu(fileName = "New Player Meta Progression", menuName = "Progression/Player Meta Progression", order = 20)]
    public class PlayerMetaProgression : ScriptableObject
    {
        private sealed class UnlockedPotentialCacheEntry
        {
            public CharacterKitDefinition kit;
            public IReadOnlyList<PotentialNodeDefinition> nodes;
        }

        private const string SaveKey = "project.player_meta_progression.v2";
        private const string LegacySaveKey = "project.player_meta_progression.v1";

        [Header("Character Potential Fragments")]
        [SerializeField] private List<ProgressionFragmentStack> fragments = new List<ProgressionFragmentStack>();

        [Header("Permanent Character Potential")]
        [SerializeField] private List<CharacterPotentialProgress> characterPotentials = new List<CharacterPotentialProgress>();

        [NonSerialized] private bool hasLoadedPersistentState;
        [NonSerialized] private bool isApplyingPersistentState;
        [NonSerialized] private Dictionary<CharacterObject, UnlockedPotentialCacheEntry> unlockedPotentialCache;

        public IReadOnlyList<ProgressionFragmentStack> Fragments => fragments;
        public IReadOnlyList<CharacterPotentialProgress> CharacterPotentials => characterPotentials;
        public event Action<string> OnPotentialChanged;

        private void OnEnable()
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
            SavePersistentState();
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
            SavePersistentState();
            return true;
        }

        public void ClearFragments()
        {
            fragments.Clear();
            SavePersistentState();
        }

        public void ClearAllProgression()
        {
            EnsureData();
            fragments.Clear();
            characterPotentials.Clear();
            InvalidatePotentialCache();
            SavePersistentState();
            OnPotentialChanged?.Invoke(null);
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
                SavePersistentState();
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
            if (character == null || character.kitDefinition == null)
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

            return GetPotentialLevel(character, node.track) >= node.level;
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

            hasLoadedPersistentState = true;
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            bool loadedLegacy = false;
            if (string.IsNullOrWhiteSpace(json))
            {
                json = PlayerPrefs.GetString(LegacySaveKey, string.Empty);
                loadedLegacy = !string.IsNullOrWhiteSpace(json);
            }
            if (string.IsNullOrWhiteSpace(json))
            {
                EnsureData();
                SavePersistentState();
                return;
            }

            int retiredArtifactFragments = ImportJsonInternal(json);
            if (loadedLegacy || retiredArtifactFragments > 0)
            {
                Debug.Log($"PlayerMetaProgression: Migrated to v2 and omitted {retiredArtifactFragments} retired Artifact fragment(s). Character Potential data was preserved.");
                SavePersistentState();
            }
        }

        public void SavePersistentState()
        {
            if (!ShouldUsePersistentStorage() || isApplyingPersistentState)
            {
                return;
            }

            PlayerPrefs.SetString(SaveKey, ExportJson());
            PlayerPrefs.Save();
        }

        public string ExportJson()
        {
            EnsureData();
            SavedPlayerMetaProgressionData data = new SavedPlayerMetaProgressionData();

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

            return JsonUtility.ToJson(data);
        }

        public void ImportJson(string json)
        {
            ImportJsonInternal(json);
        }

        private int ImportJsonInternal(string json)
        {
            EnsureData();
            isApplyingPersistentState = true;
            int retiredArtifactFragments = 0;
            fragments.Clear();
            characterPotentials.Clear();

            if (!string.IsNullOrWhiteSpace(json))
            {
                SavedPlayerMetaProgressionData data = JsonUtility.FromJson<SavedPlayerMetaProgressionData>(json);
                if (data != null)
                {
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
                }
            }

            isApplyingPersistentState = false;
            InvalidatePotentialCache();
            OnPotentialChanged?.Invoke(null);
            return retiredArtifactFragments;
        }

        private void InvalidatePotentialCache(string characterId = null)
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

        private CharacterPotentialProgress FindPotentialProgress(string characterId, bool createIfMissing)
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

        private bool ShouldUsePersistentStorage()
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            PlayerMetaProgression persistentAsset = Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
            return persistentAsset == this;
        }
    }
}
