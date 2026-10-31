using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Project.Core.ZoneState;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.Save;

namespace Project.ShopSystem
{
    [Serializable]
    public class InventoryStack
    {
        public BaseItem item;
        [Min(0)] public int quantity = 1;

        public InventoryStack(BaseItem item, int quantity)
        {
            this.item = item;
            this.quantity = quantity;
        }
    }

    [Serializable]
    public class RunCharacterState
    {
        public CharacterObject character;
        [Tooltip("LEGACY: Per-character level/EXP is retained for serialized migration only. Run Global Level is authoritative.")]
        public CharacterUpgrade upgrade = new CharacterUpgrade();
        public float currentHP = -1f;
        public float currentAE = -1f;
        [NonSerialized] protected List<RuntimeEffectInstance> activeEffects = new List<RuntimeEffectInstance>();
        [NonSerialized] protected bool hasTickingEffects;

        public IReadOnlyList<RuntimeEffectInstance> ActiveEffects
        {
            get
            {
                EnsureEffectList();
                return activeEffects;
            }
        }

        internal List<RuntimeEffectInstance> MutableActiveEffects
        {
            get
            {
                EnsureEffectList();
                return activeEffects;
            }
        }

        internal bool HasTickingEffects
        {
            get => hasTickingEffects;
            set => hasTickingEffects = value;
        }

        internal void EnsureEffectList()
        {
            activeEffects ??= new List<RuntimeEffectInstance>();
        }

        internal void ClearTransientRuntimeEffects()
        {
            EnsureEffectList();
            activeEffects.Clear();
            hasTickingEffects = false;
        }

        internal void RecalculateTickingEffects()
        {
            EnsureEffectList();
            hasTickingEffects = false;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                RuntimeEffectInstance effect = activeEffects[i];
                if (effect != null && effect.RequiresTicking)
                {
                    hasTickingEffects = true;
                    return;
                }
            }
        }
    }

    [Serializable]
    public class RunTeamSlot
    {
        protected const int DefaultArtifactSlotCount = 3;

        [SerializeField] protected CharacterObject character;
        [FormerlySerializedAs("artifacts")]
        [SerializeField, HideInInspector] protected Artifact[] legacyArtifacts = new Artifact[DefaultArtifactSlotCount];
        [SerializeField] protected ArtifactInstanceRef[] artifactRefs = new ArtifactInstanceRef[DefaultArtifactSlotCount];

        public CharacterObject Character
        {
            get => character;
            set => character = value;
        }

        public Artifact[] GetArtifactsSnapshot(int minimumSlots = DefaultArtifactSlotCount)
        {
            EnsureLegacyArtifactCapacity(minimumSlots);
            Artifact[] snapshot = new Artifact[legacyArtifacts.Length];
            Array.Copy(legacyArtifacts, snapshot, legacyArtifacts.Length);
            return snapshot;
        }

        public void SetArtifacts(Artifact[] source, int minimumSlots = DefaultArtifactSlotCount)
        {
            int artifactCount = Mathf.Max(minimumSlots, source != null ? source.Length : 0);
            legacyArtifacts = new Artifact[artifactCount];
            if (source != null)
            {
                Array.Copy(source, legacyArtifacts, Mathf.Min(source.Length, legacyArtifacts.Length));
            }
        }

        public ArtifactInstanceRef[] GetArtifactRefsSnapshot(int minimumSlots = DefaultArtifactSlotCount)
        {
            int requiredCount = Mathf.Max(1, minimumSlots);
            EnsureArtifactRefCapacity(requiredCount);
            ArtifactInstanceRef[] snapshot = new ArtifactInstanceRef[artifactRefs.Length];
            Array.Copy(artifactRefs, snapshot, artifactRefs.Length);
            return snapshot;
        }

        public void SetArtifactRefs(ArtifactInstanceRef[] source, int minimumSlots = DefaultArtifactSlotCount)
        {
            int count = Mathf.Max(minimumSlots, source != null ? source.Length : 0);
            artifactRefs = new ArtifactInstanceRef[count];
            if (source != null)
            {
                Array.Copy(source, artifactRefs, Mathf.Min(source.Length, artifactRefs.Length));
            }
        }

        public void ClearLegacyArtifacts()
        {
            legacyArtifacts = Array.Empty<Artifact>();
        }

        private void EnsureLegacyArtifactCapacity(int minimumSlots)
        {
            int requiredCount = Mathf.Max(1, minimumSlots);
            if (legacyArtifacts != null && legacyArtifacts.Length >= requiredCount)
            {
                return;
            }

            Artifact[] previousArtifacts = legacyArtifacts;
            legacyArtifacts = new Artifact[requiredCount];
            if (previousArtifacts != null)
            {
                Array.Copy(previousArtifacts, legacyArtifacts, Mathf.Min(previousArtifacts.Length, legacyArtifacts.Length));
            }
        }

        private void EnsureArtifactRefCapacity(int minimumSlots)
        {
            int requiredCount = Mathf.Max(1, minimumSlots);
            if (artifactRefs != null && artifactRefs.Length >= requiredCount)
            {
                return;
            }

            ArtifactInstanceRef[] previous = artifactRefs;
            artifactRefs = new ArtifactInstanceRef[requiredCount];
            if (previous != null)
            {
                Array.Copy(previous, artifactRefs, Mathf.Min(previous.Length, artifactRefs.Length));
            }
        }
    }

    [Serializable]
    internal class SavedInventoryItemRecord
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    internal class SavedRunCharacterRecord
    {
        public string characterId;
        public CharacterUpgrade upgrade = new CharacterUpgrade();
        public float currentHP = -1f;
        public float currentAE = -1f;
    }

    [Serializable]
    internal class SavedRunTeamSlotRecord
    {
        public string characterId;
        public string[] artifactIds = Array.Empty<string>();
        public string[] artifactInstanceIds = Array.Empty<string>();
    }

    [Serializable]
    internal class SavedArtifactInstanceRecord
    {
        public string instanceId;
        public string definitionId;
        public List<ArtifactRoll> rolls = new List<ArtifactRoll>();
        public bool isFavourite;
        public bool isNew;
        public long acquisitionOrder;
        public long collectedTimestampUtc;
        public int generationSeed;
        public int acquisitionBiomeIndex;
    }

    [Serializable]
    internal class SavedPlayerRunInventoryData
    {
        public int schemaVersion = 3;
        public int currency = 1000;
        public int globalLevel = 1;
        public int biomeIndex;
        public int activeTeamSlotIndex;
        public List<SavedInventoryItemRecord> items = new List<SavedInventoryItemRecord>();
        public List<SavedRunCharacterRecord> runCharacters = new List<SavedRunCharacterRecord>();
        public List<SavedRunTeamSlotRecord> teamSlots = new List<SavedRunTeamSlotRecord>();
        public List<SavedArtifactInstanceRecord> artifactInstances = new List<SavedArtifactInstanceRecord>();
    }

    public partial class CharacterInventoryState : ScriptableObject
    {
        public readonly struct RuntimeEffectMutationBatch : IDisposable
        {
            private readonly CharacterInventoryState owner;

            internal RuntimeEffectMutationBatch(CharacterInventoryState owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                owner?.EndRuntimeEffectMutationBatch();
            }
        }

        protected const int DefaultTeamSlotCount = 2;
        protected const int DefaultArtifactSlotCount = 3;
        protected const int DefaultCurrency = 1000;
        protected const string SaveKey = "project.player_run_inventory.v3";
        protected const string LegacySaveKey = "project.player_run_inventory.v2";
        protected static string BossLobbySaveKey => SaveSlotStore.ActiveBossRunKey;
        protected static string BossLobbyMetaSaveKey => SaveSlotStore.ActiveBossMetaKey;
        protected const string MetaProgressionResourcePath = "InventorySO/PlayerMetaProgression";
        /// <summary>Step 1 (Not Perma): run inventory must not persist items/team/characters across sessions.</summary>
        protected const bool PersistRunInventoryAcrossSessions = false;
        protected const string DefaultCharacterItemId = "char-0-main";
        protected const string DefaultCharacterItemResourcePath = "ItemSO/char_0_Main";
        protected static readonly int RuntimeEffectStatTargetCount = Enum.GetValues(typeof(RuntimeEffectStatTarget)).Length;
        protected static int currentPlaySessionId;

        [Header("Player Owned Items")]
        [Tooltip("Runtime-owned items for the current run. Character items represent roster ownership.")]
        public List<BaseItem> ownedItems = new List<BaseItem>();

        [Header("Artifact Instances")]
        [SerializeField] protected List<ArtifactInstance> artifactInstances = new List<ArtifactInstance>();
        [SerializeField] protected ArtifactRollTable artifactRollTable;

        [Header("Stacked Items")]
        [Tooltip("Runtime stack quantities for stackable consumables.")]
        [SerializeField] protected List<InventoryStack> itemStacks = new List<InventoryStack>();

        [Header("Currency")]
        public int currency = DefaultCurrency;

        [Header("Run Global Level")]
        [SerializeField, Min(1)]
        [Tooltip("Shared level used by every character for the current run.")]
        protected int _globalLevel = 1;

        [SerializeField, Min(1)]
        [Tooltip("Maximum shared level for a run.")]
        protected int _globalMaxLevel = 30;

        [Header("Run Character State")]
        [Tooltip("LEGACY level/EXP records plus active per-character HP, AE, and runtime effects.")]
        [SerializeField] protected List<RunCharacterState> runCharacters = new List<RunCharacterState>();
        [NonSerialized] protected List<CharacterObject> defeatedCharactersThisRun = new List<CharacterObject>();
        [NonSerialized] protected bool isDeathTeardown;
        [NonSerialized] protected bool isDeathLobby;

        [Header("Deployed Team")]
        [Tooltip("Run-owned team slots. Slots resolve to RunCharacterState; CharacterObject is identity only.")]
        [SerializeField] protected RunTeamSlot[] teamSlots = new RunTeamSlot[DefaultTeamSlotCount];
        [SerializeField] protected int activeTeamSlotIndex;

        /// <summary>
        /// Starting loadout used to classify run-acquired loot, never to restore defeated teammates.
        /// Static copy survives a replacement TeamLoader / inventory instance in the same play session.
        /// </summary>
        [NonSerialized] protected CharacterObject[] runStartTeamSnapshot;
        [NonSerialized] protected static CharacterObject[] sessionRunStartTeam;
        [NonSerialized] protected bool pendingLobbyMainReset;
        [NonSerialized] protected bool deathForcesLevelOne;
        [NonSerialized] protected bool suppressAutoBootstrap;

        [NonSerialized] protected bool hasLoadedPersistentState;
        [NonSerialized] protected bool isApplyingPersistentState;
        [NonSerialized] protected Dictionary<string, BaseItem> itemIndexById;
        [NonSerialized] protected Dictionary<string, ArtifactDefinition> artifactDefinitionsById;
        [NonSerialized] protected Dictionary<string, ArtifactInstance> artifactInstancesById;
        [NonSerialized] protected ArtifactRollTable runtimeDefaultArtifactRollTable;
        [NonSerialized] protected long nextArtifactAcquisitionOrder = 1;
        [NonSerialized] protected readonly Stack<float[]> runtimeEffectMultiplierSumPool = new Stack<float[]>();
        [NonSerialized] protected readonly List<RuntimeEffectInstance> expiredRuntimeEffects = new List<RuntimeEffectInstance>();
        [NonSerialized] protected readonly HashSet<CharacterObject> resolvingRuntimeStats = new HashSet<CharacterObject>();
        [NonSerialized] protected readonly List<CharacterObject> pendingRuntimeEffectCharacters = new List<CharacterObject>();
        [NonSerialized] protected readonly HashSet<CharacterObject> pendingRuntimeEffectCharacterSet = new HashSet<CharacterObject>();
        [NonSerialized] protected readonly HashSet<CharacterObject> pendingRuntimeEffectStatCharacters = new HashSet<CharacterObject>();
        [NonSerialized] protected Dictionary<string, CharacterItem> characterItemsByCharacterId;
        [NonSerialized] protected int runtimeEffectMutationBatchDepth;
        [NonSerialized] protected int initializedPlaySessionId = -1;

        public event Action<CharacterObject> OnRuntimeEffectsChanged;
        public event Action<int, int> OnGlobalLevelChanged;
        public event Action<int> OnCurrencyChanged;
        public event Action OnArtifactsChanged;
        public event Action<string, bool> OnArtifactFavouriteChanged;
        public event Action<string> OnArtifactSeenChanged;
        internal event Action<CharacterObject, bool> OnRuntimeEffectsChangedDetailed;
        public bool IsDispatchingRuntimeEffectStatChange { get; protected set; }

        public IReadOnlyList<InventoryStack> ItemStacks
        {
            get
            {
                EnsureRunData();
                return itemStacks;
            }
        }

        public IReadOnlyList<ArtifactInstance> ArtifactInstances
        {
            get
            {
                EnsureRunData();
                return artifactInstances;
            }
        }

        public ArtifactRollTable ArtifactRollTable
        {
            get
            {
                if (artifactRollTable != null) return artifactRollTable;
                runtimeDefaultArtifactRollTable ??= Project.InventorySystem.Artifacts.ArtifactRollTable.CreateRuntimeDefault();
                return runtimeDefaultArtifactRollTable;
            }
        }

        public IReadOnlyList<RunCharacterState> RunCharacters
        {
            get
            {
                EnsureRunData();
                return runCharacters;
            }
        }

        public int GlobalLevel
        {
            get
            {
                EnsureRunData();
                return _globalLevel;
            }
        }

        public int GlobalMaxLevel => Mathf.Max(1, _globalMaxLevel);

        public int ActiveTeamSlotIndex
        {
            get
            {
                EnsureRunData();
                return activeTeamSlotIndex;
            }
        }

        public int TeamSlotCount
        {
            get
            {
                EnsureRunData();
                return teamSlots.Length;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginPlaySession()
        {
            unchecked
            {
                currentPlaySessionId++;
            }
        }

        protected virtual void OnEnable()
        {
            EnsureTransientRuntimeStateForPlaySession();
            if (ShouldUsePersistentStorage())
            {
                LoadPersistentStateIfNeeded();
                return;
            }

            EnsureRunData();
        }

        public bool HasItem(BaseItem item)
        {
            return GetItemCount(item) > 0;
        }

        public void AddItem(BaseItem item)
        {
            TryAddItem(item);
        }

        public bool TryAcquireArtifact(
            ArtifactDefinition definition,
            out ArtifactInstance instance,
            int? seedOverride = null,
            int? biomeIndexOverride = null)
        {
            instance = null;
            if (definition == null || string.IsNullOrWhiteSpace(definition.id))
            {
                return false;
            }

            EnsureRunData();
            int seed = seedOverride ?? UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            int activeBiome = biomeIndexOverride
                ?? (Project.Core.ZoneState.RunProgressionManager.Instance != null
                    ? Project.Core.ZoneState.RunProgressionManager.Instance.CurrentBiomeIndex
                    : 0);
            ArtifactRollGenerator generator = new ArtifactRollGenerator(ArtifactRollTable);
            instance = generator.Generate(definition, nextArtifactAcquisitionOrder++, seed, activeBiome);
            if (!TryAddArtifactInstanceInternal(instance))
            {
                instance = null;
                return false;
            }

            SavePersistentState();
            NotifyArtifactsChanged();
            return true;
        }

        public bool TryGetArtifactInstance(string instanceId, out ArtifactInstance instance)
        {
            EnsureRunData();
            EnsureArtifactIndices();
            instance = null;
            return !string.IsNullOrWhiteSpace(instanceId)
                && artifactInstancesById.TryGetValue(instanceId, out instance);
        }

        public ArtifactDefinition ResolveArtifactDefinition(string definitionId)
        {
            EnsureArtifactDefinitionIndex();
            artifactDefinitionsById.TryGetValue(definitionId ?? string.Empty, out ArtifactDefinition definition);
            return definition;
        }

        public bool TrySetArtifactFavourite(string instanceId, bool favourite)
        {
            if (!TryGetArtifactInstance(instanceId, out ArtifactInstance instance)
                || instance.isFavourite == favourite)
            {
                return false;
            }

            instance.isFavourite = favourite;
            instance.isNew = false;
            SavePersistentState();
            OnArtifactFavouriteChanged?.Invoke(instanceId, favourite);
            return true;
        }

        public bool TryMarkArtifactSeen(string instanceId)
        {
            if (!TryGetArtifactInstance(instanceId, out ArtifactInstance instance) || !instance.isNew)
            {
                return false;
            }

            instance.isNew = false;
            SavePersistentState();
            OnArtifactSeenChanged?.Invoke(instanceId);
            return true;
        }

        public bool TryRemoveArtifactInstance(string instanceId)
        {
            EnsureRunData();
            if (string.IsNullOrWhiteSpace(instanceId) || IsArtifactEquipped(instanceId))
            {
                return false;
            }

            for (int i = 0; i < artifactInstances.Count; i++)
            {
                if (!string.Equals(artifactInstances[i]?.instanceId, instanceId, StringComparison.Ordinal))
                    continue;

                artifactInstances.RemoveAt(i);
                RebuildArtifactInstanceIndex();
                SavePersistentState();
                NotifyArtifactsChanged();
                return true;
            }
            return false;
        }

        public HashSet<string> GetEquippedArtifactInstanceIds(HashSet<string> destination = null)
        {
            EnsureRunData();
            destination ??= new HashSet<string>(StringComparer.Ordinal);
            destination.Clear();
            for (int i = 0; i < teamSlots.Length; i++)
            {
                ArtifactInstanceRef[] refs = teamSlots[i]?.GetArtifactRefsSnapshot(DefaultArtifactSlotCount);
                if (refs == null) continue;
                for (int j = 0; j < refs.Length; j++)
                    if (!refs[j].IsEmpty) destination.Add(refs[j].instanceId);
            }
            return destination;
        }

        public ArtifactInstanceRef[] GetEquippedArtifactRefsAt(int slotIndex)
        {
            EnsureRunData();
            if (slotIndex < 0 || slotIndex >= teamSlots.Length || teamSlots[slotIndex] == null)
                return Array.Empty<ArtifactInstanceRef>();
            return teamSlots[slotIndex].GetArtifactRefsSnapshot(DefaultArtifactSlotCount);
        }

        public ArtifactInstance[] GetEquippedArtifactInstancesAt(int slotIndex)
        {
            ArtifactInstanceRef[] refs = GetEquippedArtifactRefsAt(slotIndex);
            ArtifactInstance[] result = new ArtifactInstance[refs.Length];
            for (int i = 0; i < refs.Length; i++)
                TryGetArtifactInstance(refs[i].instanceId, out result[i]);
            return result;
        }

        public void SetEquippedArtifactRefsAt(int slotIndex, ArtifactInstanceRef[] refs)
        {
            EnsureRunData();
            if (slotIndex < 0 || slotIndex >= teamSlots.Length) return;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            ArtifactInstanceRef[] sanitized = refs != null
                ? new ArtifactInstanceRef[Mathf.Max(DefaultArtifactSlotCount, refs.Length)]
                : new ArtifactInstanceRef[DefaultArtifactSlotCount];
            for (int i = 0; refs != null && i < refs.Length && i < sanitized.Length; i++)
            {
                string id = refs[i].instanceId;
                if (!string.IsNullOrWhiteSpace(id)
                    && TryGetArtifactInstance(id, out _)
                    && seen.Add(id))
                {
                    sanitized[i] = refs[i];
                    artifactInstancesById[id].isNew = false;
                }
            }

            for (int otherSlotIndex = 0; otherSlotIndex < teamSlots.Length; otherSlotIndex++)
            {
                if (otherSlotIndex == slotIndex || teamSlots[otherSlotIndex] == null) continue;
                ArtifactInstanceRef[] otherRefs = teamSlots[otherSlotIndex].GetArtifactRefsSnapshot(DefaultArtifactSlotCount);
                bool changed = false;
                for (int i = 0; i < otherRefs.Length; i++)
                {
                    if (!otherRefs[i].IsEmpty && seen.Contains(otherRefs[i].instanceId))
                    {
                        otherRefs[i] = default;
                        changed = true;
                    }
                }
                if (changed) teamSlots[otherSlotIndex].SetArtifactRefs(otherRefs, DefaultArtifactSlotCount);
            }

            teamSlots[slotIndex].SetArtifactRefs(sanitized, DefaultArtifactSlotCount);
            SavePersistentState();
            NotifyArtifactsChanged();
        }

        protected void NotifyArtifactsChanged()
        {
            if (OnArtifactsChanged == null) return;
            Delegate[] invocationList = OnArtifactsChanged.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                var del = invocationList[i];
                if (del.Target is UnityEngine.Object unityObj && unityObj == null)
                {
                    OnArtifactsChanged -= (Action)del;
                }
                else
                {
                    try
                    {
                        ((Action)del)();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
        }

        public bool IsArtifactEquipped(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) return false;
            EnsureRunData();
            for (int i = 0; i < teamSlots.Length; i++)
            {
                ArtifactInstanceRef[] refs = teamSlots[i]?.GetArtifactRefsSnapshot(DefaultArtifactSlotCount);
                if (refs == null) continue;
                for (int j = 0; j < refs.Length; j++)
                    if (string.Equals(refs[j].instanceId, instanceId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public bool TryAddItem(BaseItem item, int amount = 1)
        {
            if (item == null || amount <= 0)
            {
                return false;
            }

            EnsureRunData();

            if (item is CharacterItem defeatedItem && CannotAcquireCharacter(defeatedItem.characterObjectData))
            {
                return false;
            }

            if (item is ArtifactDefinition artifactDefinition)
            {
                for (int i = 0; i < amount; i++)
                {
                    if (!TryAcquireArtifact(artifactDefinition, out _)) return false;
                }
                return true;
            }

            bool added;
            if (IsStackable(item))
            {
                InventoryStack stack = FindStack(item);
                int currentQuantity = stack != null ? stack.quantity : 0;
                int stackLimit = GetStackLimit(item);
                if (currentQuantity + amount > stackLimit)
                {
                    Debug.LogWarning($"Cannot add {amount}x {item.itemName}; stack limit is {stackLimit}.");
                    return false;
                }

                if (stack == null)
                {
                    itemStacks.Add(new InventoryStack(item, amount));
                }
                else
                {
                    stack.quantity += amount;
                }

                EnsureLegacyItemListed(item);
                added = true;
            }
            else
            {
                if (ownedItems.Contains(item))
                {
                    return false;
                }

                ownedItems.Add(item);
                added = true;
            }

            if (added && item is CharacterItem characterItem)
            {
                GetOrCreateRunCharacterState(characterItem.characterObjectData);
            }

            PostInventoryMutation(save: true);
            return true;
        }

        public bool CanAddItem(BaseItem item, int amount = 1)
        {
            if (item == null || amount <= 0)
            {
                return false;
            }

            EnsureRunData();

            if (item is CharacterItem characterItem && CannotAcquireCharacter(characterItem.characterObjectData))
            {
                return false;
            }

            if (item is ArtifactDefinition)
            {
                return true;
            }

            if (IsStackable(item))
            {
                InventoryStack stack = FindStack(item);
                int currentQuantity = stack != null ? stack.quantity : 0;
                return currentQuantity + amount <= GetStackLimit(item);
            }

            return !ownedItems.Contains(item);
        }

        public void RemoveItem(BaseItem item)
        {
            TryRemoveItem(item);
        }

        public bool TryRemoveItem(BaseItem item, int amount = 1)
        {
            if (item == null || amount <= 0)
            {
                return false;
            }

            EnsureRunData();

            if (item is ArtifactDefinition)
            {
                // Artifact copies require an explicit instance selection.
                return false;
            }

            if (IsStackable(item))
            {
                InventoryStack stack = FindStack(item);
                if (stack == null || stack.quantity < amount)
                {
                    return false;
                }

                stack.quantity -= amount;
                if (stack.quantity <= 0)
                {
                    itemStacks.Remove(stack);
                    RemoveAllLegacyEntries(item);
                }

                PostInventoryMutation(save: true);
                return true;
            }

            if (amount > 1 || !ownedItems.Contains(item))
            {
                return false;
            }

            ownedItems.Remove(item);
            PostInventoryMutation(save: true);
            return true;
        }

        public int GetItemCount(BaseItem item)
        {
            if (item == null)
            {
                return 0;
            }

            EnsureRunData();

            if (item is ArtifactDefinition artifactDefinition)
            {
                int count = 0;
                for (int i = 0; i < artifactInstances.Count; i++)
                    if (string.Equals(artifactInstances[i]?.definitionId, artifactDefinition.id, StringComparison.Ordinal)) count++;
                return count;
            }

            if (IsStackable(item))
            {
                InventoryStack stack = FindStack(item);
                return stack != null ? stack.quantity : 0;
            }

            return ownedItems.Contains(item) ? 1 : 0;
        }

        public void ClearInventory()
        {
            EnsureInventoryData();
            defeatedCharactersThisRun.Clear();
            isDeathTeardown = false;
            isDeathLobby = false;
            int previousGlobalLevel = _globalLevel;
            _globalLevel = 1;
            ownedItems.Clear();
            itemStacks.Clear();
            runCharacters.Clear();
            artifactInstances.Clear();
            artifactInstancesById = null;
            EnsureTeamSlotCapacity(DefaultTeamSlotCount);
            for (int i = 0; i < teamSlots.Length; i++)
            {
                teamSlots[i].Character = null;
                teamSlots[i].SetArtifacts(null, DefaultArtifactSlotCount);
                teamSlots[i].SetArtifactRefs(null, DefaultArtifactSlotCount);
            }

            activeTeamSlotIndex = 0;
            ClearRunStartTeamSnapshot();
            PostInventoryMutation(save: true);
            if (previousGlobalLevel != _globalLevel)
            {
                OnGlobalLevelChanged?.Invoke(previousGlobalLevel, _globalLevel);
            }
        }

        public void ResetRunProgress()
        {
            ClearInventory();
        }

        /// <summary>
        /// Menu Continue / New Game always start in Lobby with authored run state.
        /// New Game resets currency; Continue can preserve it when no checkpoint exists.
        /// Meta progression is owned separately and is not cleared here.
        /// </summary>
        public void ResetRunStateForMenuStart(bool resetCurrency = true)
        {
            hasLoadedPersistentState = true;
            isDeathTeardown = false;
            isDeathLobby = false;
            pendingLobbyMainReset = false;
            suppressAutoBootstrap = false;
            if (resetCurrency)
            {
                SetCurrency(DefaultCurrency);
            }
            ClearInventory();
            EnsurePlayableState();
        }

        public void ResetRunProgressForDeath(CharacterProgressionState metaProgression = null)
        {
            _ = metaProgression;
            EnsureInventoryData();
            CharacterObject[] lobbyTeam = ResolveDeathResetTeam();
            isDeathTeardown = true;
            isDeathLobby = false;
            pendingLobbyMainReset = true;
            deathForcesLevelOne = true;

            int previousGlobalLevel = _globalLevel;

            suppressAutoBootstrap = true;
            try
            {
                foreach (RunCharacterState state in runCharacters.ToArray())
                {
                    if (state != null && state.currentHP >= 0f && state.currentHP <= 0.01f)
                    {
                        RecordCharacterDefeat(state.character);
                    }
                }
                RecordCharacterDefeat(ResolveMainCharacter());
                runCharacters.Clear();
                SetCurrency(DefaultCurrency);
                _globalLevel = 1;
                StripNonCharacterRunLoot();
                StripRunAcquiredCharacterItems(lobbyTeam);
                artifactInstances.Clear();
                artifactInstancesById = null;
                ClearDeployedTeamSlots();
            }
            finally
            {
                suppressAutoBootstrap = false;
            }

            ClearCapturedRunStartTeam();
            RunProgressionManager.EnsureInstance()?.ResetRun();
            ClearBossLobbyCheckpoint();
            PostInventoryMutation(save: true);
            if (previousGlobalLevel != _globalLevel)
            {
                OnGlobalLevelChanged?.Invoke(previousGlobalLevel, _globalLevel);
            }
        }

        public int AddGlobalLevels(int requestedLevels)
        {
            EnsureRunData();
            if (requestedLevels <= 0 || _globalLevel >= GlobalMaxLevel)
            {
                return 0;
            }

            int previousGlobalLevel = _globalLevel;
            int remainingLevels = GlobalMaxLevel - previousGlobalLevel;
            int appliedLevels = Mathf.Min(requestedLevels, remainingLevels);
            _globalLevel = previousGlobalLevel + appliedLevels;

            SavePersistentState();
            OnGlobalLevelChanged?.Invoke(previousGlobalLevel, _globalLevel);
            return appliedLevels;
        }

        public bool CanAfford(int amount)
        {
            return currency >= amount;
        }

        public void SpendCurrency(int amount)
        {
            SetCurrency(currency - Mathf.Max(0, amount));
        }

        public void AddCurrency(int amount)
        {
            SetCurrency(currency + Mathf.Max(0, amount));
        }

        private void SetCurrency(int amount)
        {
            int next = Mathf.Max(0, amount);
            if (currency == next)
            {
                return;
            }

            currency = next;
            SavePersistentState();
            NotifyCurrencyChanged();
        }

        private void NotifyCurrencyChanged()
        {
            OnCurrencyChanged?.Invoke(Mathf.Max(0, currency));
        }

        public bool HasDeployedTeam()
        {
            EnsureRunData();
            return CountTeamCharacters() > 0;
        }

        public int GetDeployedTeamMemberCount()
        {
            EnsureRunData();
            return CountTeamCharacters();
        }

        /// <summary>
        /// Starts a fresh run with Main and the remaining owned lobby loadout.
        /// </summary>
        public bool TryPrepareForNewRun(int minimumTeamMembers, out string failureReason)
        {
            EnsureRunData();

            CharacterItem mainItem = Resources.Load<CharacterItem>(DefaultCharacterItemResourcePath);
            if (mainItem == null || mainItem.characterObjectData == null)
            {
                failureReason = "The Main character item is missing.";
                return false;
            }

            CharacterObject main = mainItem.characterObjectData;
            CharacterObject[] previousTeam = GetTeamSnapshot();
            List<CharacterObject> nextTeam = new List<CharacterObject> { main };
            foreach (CharacterObject character in previousTeam)
            {
                if (nextTeam.Count >= previousTeam.Length) break;
                if (character != null && !IsMainCharacter(character)
                    && !IsCharacterDefeated(character) && FindOwnedCharacterItem(character) != null
                    && !nextTeam.Exists(existing => IsSameTeamCharacter(existing, character)))
                {
                    nextTeam.Add(character);
                }
            }

            if (isDeathTeardown)
            {
                foreach (CharacterObject character in GetOwnedCharactersInOrder())
                {
                    if (nextTeam.Count >= previousTeam.Length) break;
                    if (!IsMainCharacter(character) && !IsCharacterDefeated(character)
                        && !nextTeam.Exists(existing => IsSameTeamCharacter(existing, character)))
                    {
                        nextTeam.Add(character);
                    }
                }
            }

            if (nextTeam.Count < Mathf.Max(1, minimumTeamMembers))
            {
                failureReason = $"Deploy at least {Mathf.Max(1, minimumTeamMembers)} team member(s) before starting a run.";
                return false;
            }

            CharacterObject[] teamSnapshot = new CharacterObject[Mathf.Max(previousTeam.Length, nextTeam.Count)];
            nextTeam.CopyTo(teamSnapshot);
            ArtifactInstanceRef[][] previousArtifacts = BuildTeamArtifactSnapshot(previousTeam.Length);
            ArtifactInstanceRef[][] artifactSnapshot = new ArtifactInstanceRef[teamSnapshot.Length][];
            for (int i = 0; i < nextTeam.Count; i++)
            {
                for (int j = 0; j < previousTeam.Length; j++)
                {
                    if (IsSameTeamCharacter(teamSnapshot[i], previousTeam[j]))
                    {
                        artifactSnapshot[i] = previousArtifacts[j];
                        break;
                    }
                }
            }

            if (!TryRestoreMainOwnershipForNewRun(mainItem, out failureReason))
            {
                return false;
            }

            CaptureRunStartTeam(teamSnapshot);

            SetCurrency(DefaultCurrency);
            runCharacters.Clear();
            StripNonCharacterRunLoot();

            DeployTeam(teamSnapshot, 0, artifactSnapshot);
            if (FindOwnedCharacterItem(main) == null && !TryRestoreMainOwnershipForNewRun(mainItem, out failureReason))
            {
                return false;
            }

            // Keep defeat guards through deployment; only a prepared new run allows reacquisition.
            defeatedCharactersThisRun.Clear();
            PostInventoryMutation(save: false);
            failureReason = null;
            return true;
        }

        public void DeployTeam(CharacterObject[] characters, int preferredIndex, ArtifactInstanceRef[][] artifactsBySlot = null)
        {
            EnsureRunData();

            int requestedSlotCount = Mathf.Max(
                DefaultTeamSlotCount,
                characters != null ? characters.Length : 0,
                artifactsBySlot != null ? artifactsBySlot.Length : 0);
            EnsureTeamSlotCapacity(requestedSlotCount);
            HashSet<string> deployedArtifactIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < teamSlots.Length; i++)
            {
                CharacterObject requested = characters != null && i < characters.Length ? characters[i] : null;
                CharacterObject character = ResolveOwnedTeamCharacter(requested);
                teamSlots[i].Character = character;
                if (character != null)
                {
                    GetOrCreateRunCharacterState(character);
                }

                ArtifactInstanceRef[] sourceArtifacts = artifactsBySlot != null && i < artifactsBySlot.Length
                    ? artifactsBySlot[i]
                    : null;
                ArtifactInstanceRef[] sanitized = sourceArtifacts != null
                    ? new ArtifactInstanceRef[Mathf.Max(DefaultArtifactSlotCount, sourceArtifacts.Length)]
                    : new ArtifactInstanceRef[DefaultArtifactSlotCount];
                for (int artifactIndex = 0; sourceArtifacts != null && artifactIndex < sourceArtifacts.Length; artifactIndex++)
                {
                    string instanceId = sourceArtifacts[artifactIndex].instanceId;
                    if (!string.IsNullOrWhiteSpace(instanceId)
                        && TryGetArtifactInstance(instanceId, out _)
                        && deployedArtifactIds.Add(instanceId))
                        sanitized[artifactIndex] = sourceArtifacts[artifactIndex];
                    if (!string.IsNullOrWhiteSpace(instanceId)
                        && artifactInstancesById.TryGetValue(instanceId, out ArtifactInstance equippedInstance))
                        equippedInstance.isNew = false;
                }
                teamSlots[i].SetArtifactRefs(sanitized, DefaultArtifactSlotCount);
            }

            activeTeamSlotIndex = ResolveActiveSlotIndex(preferredIndex);
            PostInventoryMutation(save: true);
        }

        public bool HasPendingLobbyMainReset => pendingLobbyMainReset;
        public bool DeathForcesLevelOne => deathForcesLevelOne;

        public void ClearDeathForcesLevelOne()
        {
            deathForcesLevelOne = false;
        }
        public bool IsDeathTeardown => isDeathTeardown;
        public bool IsDeathLobby => isDeathLobby;

        public bool IsLobbyMainCharacter(CharacterObject character)
        {
            return isDeathLobby && IsMainCharacter(character);
        }

        public bool IsCharacterDefeated(CharacterObject character)
        {
            if (character == null || defeatedCharactersThisRun == null) return false;
            foreach (CharacterObject defeated in defeatedCharactersThisRun)
            {
                if (IsSameTeamCharacter(defeated, character)
                    || (IsMainCharacter(defeated) && IsMainCharacter(character))) return true;
            }
            return false;
        }

        public bool IsCharacterUnavailable(CharacterObject character)
        {
            if (character == null) return true;
            if (IsLobbyMainCharacter(character)) return false;
            return IsCharacterDefeated(character);
        }

        public bool CanReacquireDefeatedCharacter(CharacterItem item)
        {
            if (item == null || item.characterObjectData == null || HasItem(item))
            {
                return false;
            }

            if (!IsCharacterDefeated(item.characterObjectData))
            {
                return false;
            }

            // A full wipe still owns the death flow. Buying Main there would skip the lobby reset.
            return !IsMainCharacter(item.characterObjectData) || !isDeathTeardown;
        }

        public bool TryReacquireDefeatedCharacter(CharacterItem item)
        {
            if (!CanReacquireDefeatedCharacter(item))
            {
                return false;
            }

            ReleaseCharacterDefeat(item.characterObjectData);
            if (TryAddItem(item))
            {
                return true;
            }

            RecordCharacterDefeat(item.characterObjectData);
            return false;
        }

        private bool CannotAcquireCharacter(CharacterObject character)
        {
            if (character == null)
            {
                return true;
            }

            // Only the explicit New Run boundary clears a defeat during the same session.
            if (IsMainCharacter(character))
            {
                return isDeathTeardown || IsCharacterDefeated(character);
            }

            return IsCharacterDefeated(character);
        }

        private void ReleaseCharacterDefeat(CharacterObject character)
        {
            if (character == null || defeatedCharactersThisRun == null)
            {
                return;
            }

            for (int i = defeatedCharactersThisRun.Count - 1; i >= 0; i--)
            {
                CharacterObject defeated = defeatedCharactersThisRun[i];
                if (IsSameTeamCharacter(defeated, character)
                    || (IsMainCharacter(defeated) && IsMainCharacter(character)))
                {
                    defeatedCharactersThisRun.RemoveAt(i);
                }
            }
        }

        private bool TryRestoreMainOwnershipForNewRun(CharacterItem mainItem, out string failureReason)
        {
            failureReason = null;
            isDeathTeardown = false;
            isDeathLobby = false;
            pendingLobbyMainReset = false;
            defeatedCharactersThisRun ??= new List<CharacterObject>();
            for (int i = defeatedCharactersThisRun.Count - 1; i >= 0; i--)
            {
                if (IsMainCharacter(defeatedCharactersThisRun[i]))
                {
                    defeatedCharactersThisRun.RemoveAt(i);
                }
            }

            if (mainItem == null)
            {
                failureReason = "The Main character item is missing.";
                return false;
            }

            if (!ownedItems.Contains(mainItem))
            {
                ownedItems.Add(mainItem);
            }

            CharacterObject main = mainItem.characterObjectData;
            if (main != null)
            {
                GetOrCreateRunCharacterState(main);
            }

            if (FindOwnedCharacterItem(main) == null)
            {
                failureReason = "New Run started without Main in ownedItems.";
                return false;
            }

            return true;
        }

        public void RequestLobbyMainReset()
        {
            pendingLobbyMainReset = true;
        }

        public void ConsumeLobbyMainReset()
        {
            pendingLobbyMainReset = false;
        }

        /// <summary>
        /// Store the starting roster to classify run-acquired characters. Do not call this
        /// on scene load because that freezes whatever
        /// is currently on the inventory asset, including ghost CharacterObjects.
        /// </summary>
        public void RememberRunStartTeam(CharacterObject[] team, bool overwrite)
        {
            if (!overwrite && HasAnyCharacter(runStartTeamSnapshot))
            {
                return;
            }

            if (!overwrite && HasAnyCharacter(sessionRunStartTeam))
            {
                if (!HasAnyCharacter(runStartTeamSnapshot))
                {
                    runStartTeamSnapshot = CopyTeamArray(sessionRunStartTeam);
                }

                return;
            }

            CaptureRunStartTeam(team);
        }

        private void CaptureRunStartTeam(CharacterObject[] team)
        {
            if (!HasAnyCharacter(team))
            {
                return;
            }

            runStartTeamSnapshot = CopyTeamArray(team);
            sessionRunStartTeam = CopyTeamArray(team);
        }

        public CharacterObject[] GetRunStartTeamOrCurrent()
        {
            return GetTeamSnapshot();
        }

        /// <summary>
        /// Restore Main's team slot and owned item at the Lobby boundary, not during death teardown.
        /// </summary>
        public void EnsureMainCharacterOnLobbyTeam()
        {
            EnsureRunData();
            if (isDeathTeardown)
            {
                CharacterItem mainItem = Resources.Load<CharacterItem>(DefaultCharacterItemResourcePath);
                if (mainItem == null || mainItem.characterObjectData == null)
                {
                    Debug.LogError("PlayerRunInventory: Cannot restore Lobby Main because its CharacterItem is missing.");
                    return;
                }

                isDeathLobby = true;
                // Keep the old run's defeat guards until New Run. Only this explicit Lobby
                // restore grants Main ownership; normal bootstrap must remain blocked.
                if (!ownedItems.Contains(mainItem))
                    ownedItems.Add(mainItem);
                ClearDeployedTeamSlots();
                teamSlots[0].Character = mainItem.characterObjectData;
                activeTeamSlotIndex = 0;
                PostInventoryMutation(save: false);
                return;
            }

            EnsureMainCharacterOwned();
            CharacterObject main = ResolveMainCharacter();
            CharacterObject[] lobbyTeam = GetTeamSnapshot();
            int mainIndex = Array.FindIndex(lobbyTeam, IsMainCharacter);
            if (mainIndex > 0)
            {
                lobbyTeam[mainIndex] = lobbyTeam[0];
            }
            lobbyTeam[0] = main;
            DeployTeam(lobbyTeam, preferredIndex: 0, artifactsBySlot: null);
            SetActiveTeamSlotIndex(GetMainCharacterSlotIndex());
        }

        public bool HasMainCharacterDeployed()
        {
            EnsureRunData();
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (IsMainCharacter(teamSlots[i] != null ? teamSlots[i].Character : null))
                {
                    return true;
                }
            }

            return false;
        }

        public int GetMainCharacterSlotIndex()
        {
            EnsureRunData();
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (IsMainCharacter(teamSlots[i] != null ? teamSlots[i].Character : null))
                {
                    return i;
                }
            }

            return 0;
        }

        public bool IsMainCharacter(CharacterObject character)
        {
            if (character == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(character.characterId)
                && string.Equals(character.characterId, DefaultCharacterItemId, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(character.name, "CharacterObject_main", StringComparison.Ordinal);
        }

        private CharacterObject ResolveMainCharacter()
        {
            CharacterObject byId = ResolveCharacterById(DefaultCharacterItemId);
            if (byId != null)
            {
                return byId;
            }

            CharacterItem defaultCharacterItem = Resources.Load<CharacterItem>(DefaultCharacterItemResourcePath);
            if (defaultCharacterItem != null && defaultCharacterItem.characterObjectData != null)
            {
                return defaultCharacterItem.characterObjectData;
            }

            EnsureRunData();
            for (int i = 0; i < teamSlots.Length; i++)
            {
                CharacterObject slotted = teamSlots[i] != null ? teamSlots[i].Character : null;
                if (IsMainCharacter(slotted))
                {
                    return slotted;
                }
            }

            return null;
        }

        private CharacterObject[] ResolveDeathResetTeam()
        {
            CharacterObject[] source = HasAnyCharacter(runStartTeamSnapshot)
                ? runStartTeamSnapshot
                : HasAnyCharacter(sessionRunStartTeam)
                    ? sessionRunStartTeam
                    : null;
            if (source != null)
            {
                return CopyTeamArray(source);
            }

            return GetTeamSnapshot();
        }

        private CharacterObject ResolveOwnedTeamCharacter(CharacterObject character)
        {
            if (IsCharacterUnavailable(character)) return null;
            if (IsLobbyMainCharacter(character)) return ResolveMainCharacter();
            CharacterItem ownedItem = FindOwnedCharacterItem(character);
            return ownedItem != null ? ownedItem.characterObjectData : null;
        }

        private void StripNonCharacterRunLoot()
        {
            itemStacks.Clear();
            if (ownedItems == null)
            {
                return;
            }

            for (int i = ownedItems.Count - 1; i >= 0; i--)
            {
                BaseItem item = ownedItems[i];
                if (item == null || item is CharacterItem)
                {
                    continue;
                }

                ownedItems.RemoveAt(i);
            }
        }

        private void StripRunAcquiredCharacterItems(CharacterObject[] lobbyTeam)
        {
            if (ownedItems == null)
            {
                return;
            }

            for (int i = ownedItems.Count - 1; i >= 0; i--)
            {
                if (ownedItems[i] is not CharacterItem characterItem)
                {
                    continue;
                }

                CharacterObject character = characterItem.characterObjectData;
                if (IsMainCharacter(character) || TeamArrayContains(lobbyTeam, character))
                {
                    continue;
                }

                ownedItems.RemoveAt(i);
                RemoveRunCharacterState(character);
            }
        }

        private static bool TeamArrayContains(CharacterObject[] team, CharacterObject character)
        {
            if (team == null || character == null)
            {
                return false;
            }

            for (int i = 0; i < team.Length; i++)
            {
                if (IsSameTeamCharacter(team[i], character))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearDeployedTeamSlots()
        {
            EnsureTeamSlotCapacity(DefaultTeamSlotCount);
            for (int i = 0; i < teamSlots.Length; i++)
            {
                teamSlots[i].Character = null;
                teamSlots[i].SetArtifacts(null, DefaultArtifactSlotCount);
                teamSlots[i].SetArtifactRefs(null, DefaultArtifactSlotCount);
            }

            activeTeamSlotIndex = 0;
        }

        private void ClearCapturedRunStartTeam()
        {
            runStartTeamSnapshot = null;
            sessionRunStartTeam = null;
        }

        private void ClearRunStartTeamSnapshot()
        {
            ClearCapturedRunStartTeam();
            pendingLobbyMainReset = false;
        }

        private static bool HasAnyCharacter(CharacterObject[] team)
        {
            if (team == null)
            {
                return false;
            }

            for (int i = 0; i < team.Length; i++)
            {
                if (team[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static CharacterObject[] CopyTeamArray(CharacterObject[] team)
        {
            CharacterObject[] copy = new CharacterObject[Mathf.Max(DefaultTeamSlotCount, team != null ? team.Length : 0)];
            if (team == null)
            {
                return copy;
            }

            for (int i = 0; i < team.Length && i < copy.Length; i++)
            {
                copy[i] = team[i];
            }

            return copy;
        }

        /// <summary>
        /// Removes a defeated teammate from the run team, returns their equipped
        /// artifacts to inventory, and compacts survivors so the preferred survivor
        /// lands at index 0. Defeat removes ownership even when no survivors remain.
        /// </summary>
        public bool TryRemoveDefeatedTeamCharacter(
            CharacterObject defeatedCharacter,
            CharacterObject preferredSurvivor,
            out int newActiveIndex)
        {
            newActiveIndex = -1;
            EnsureRunData();
            if (defeatedCharacter == null)
            {
                return false;
            }

            List<CharacterObject> survivors = new List<CharacterObject>(teamSlots.Length);
            List<ArtifactInstanceRef[]> survivorArtifacts = new List<ArtifactInstanceRef[]>(teamSlots.Length);
            for (int i = 0; i < teamSlots.Length; i++)
            {
                CharacterObject character = teamSlots[i].Character;
                if (character == null || IsSameTeamCharacter(character, defeatedCharacter)
                    || (IsMainCharacter(character) && IsMainCharacter(defeatedCharacter)))
                {
                    continue;
                }

                // Drop any other 0-HP slots so compact team only keeps living members.
                if (!IsRunCharacterAlive(character))
                {
                    RecordCharacterDefeat(character);
                    continue;
                }

                survivors.Add(character);
                survivorArtifacts.Add(teamSlots[i].GetArtifactRefsSnapshot(DefaultArtifactSlotCount));
            }

            RecordCharacterDefeat(defeatedCharacter);
            if (survivors.Count == 0)
            {
                isDeathTeardown = true;
                isDeathLobby = false;
                suppressAutoBootstrap = true;
                try
                {
                    DeployTeam(Array.Empty<CharacterObject>(), 0, null);
                }
                finally
                {
                    suppressAutoBootstrap = false;
                }

                return false;
            }

            int preferIndex = 0;
            if (preferredSurvivor != null)
            {
                preferIndex = survivors.FindIndex(c => IsSameTeamCharacter(c, preferredSurvivor));
                if (preferIndex < 0)
                {
                    preferIndex = 0;
                }
            }

            if (preferIndex > 0)
            {
                RotateList(survivors, preferIndex);
                RotateList(survivorArtifacts, preferIndex);
            }

            CharacterObject[] compacted = new CharacterObject[Mathf.Max(DefaultTeamSlotCount, survivors.Count)];
            ArtifactInstanceRef[][] compactedArtifacts = new ArtifactInstanceRef[compacted.Length][];
            for (int i = 0; i < survivors.Count; i++)
            {
                compacted[i] = survivors[i];
                compactedArtifacts[i] = survivorArtifacts[i];
            }

            DeployTeam(compacted, preferredIndex: 0, compactedArtifacts);
            newActiveIndex = 0;
            return true;
        }

        private static void RotateList<T>(List<T> values, int startIndex)
        {
            if (values == null || values.Count == 0 || startIndex <= 0 || startIndex >= values.Count)
            {
                return;
            }

            T[] buffer = values.ToArray();
            values.Clear();
            for (int i = 0; i < buffer.Length; i++)
            {
                values.Add(buffer[(startIndex + i) % buffer.Length]);
            }
        }

        private CharacterItem FindOwnedCharacterItem(CharacterObject character)
        {
            if (character == null || ownedItems == null)
            {
                return null;
            }

            for (int i = 0; i < ownedItems.Count; i++)
            {
                if (ownedItems[i] is CharacterItem characterItem
                    && characterItem.characterObjectData == character)
                {
                    return characterItem;
                }
            }

            if (!string.IsNullOrWhiteSpace(character.characterId))
            {
                BuildItemIndices();
                if (characterItemsByCharacterId != null
                    && characterItemsByCharacterId.TryGetValue(character.characterId, out CharacterItem byId)
                    && ownedItems.Contains(byId))
                {
                    return byId;
                }
            }

            return null;
        }

        private void RecordCharacterDefeat(CharacterObject character)
        {
            if (character == null) return;
            defeatedCharactersThisRun ??= new List<CharacterObject>();
            if (!IsCharacterDefeated(character)) defeatedCharactersThisRun.Add(character);

            for (int i = ownedItems.Count - 1; i >= 0; i--)
            {
                if (ownedItems[i] is CharacterItem item
                    && (IsSameTeamCharacter(item.characterObjectData, character)
                        || (IsMainCharacter(item.characterObjectData) && IsMainCharacter(character))))
                {
                    ownedItems.RemoveAt(i);
                }
            }
            itemIndexById = null;
            characterItemsByCharacterId = null;
            RemoveRunCharacterState(character);
        }

        private void RemoveRunCharacterState(CharacterObject character)
        {
            if (character == null || runCharacters == null)
            {
                return;
            }

            for (int i = runCharacters.Count - 1; i >= 0; i--)
            {
                RunCharacterState state = runCharacters[i];
                if (state != null && (IsSameTeamCharacter(state.character, character)
                    || (IsMainCharacter(state.character) && IsMainCharacter(character))))
                {
                    runCharacters.RemoveAt(i);
                }
            }
        }

        public CharacterObject[] GetTeamSnapshot(int minimumSlots = DefaultTeamSlotCount)
        {
            EnsureRunData();
            int slotCount = Mathf.Max(minimumSlots, teamSlots.Length);
            CharacterObject[] snapshot = new CharacterObject[slotCount];
            for (int i = 0; i < teamSlots.Length && i < snapshot.Length; i++)
            {
                snapshot[i] = teamSlots[i].Character;
            }

            return snapshot;
        }

        public CharacterObject GetTeamCharacterAt(int slotIndex)
        {
            EnsureRunData();
            if (slotIndex < 0 || slotIndex >= teamSlots.Length)
            {
                return null;
            }

            return teamSlots[slotIndex].Character;
        }

        public RunCharacterState GetTeamRunCharacterAt(int slotIndex)
        {
            return GetRunCharacter(GetTeamCharacterAt(slotIndex));
        }

        public Artifact[] GetEquippedArtifactsAt(int slotIndex)
        {
            ArtifactDefinition[] source = GetEquippedArtifactDefinitionsAt(slotIndex);
            Artifact[] definitions = new Artifact[source.Length];
            for (int i = 0; i < source.Length; i++) definitions[i] = source[i] as Artifact;
            return definitions;
        }

        public ArtifactDefinition[] GetEquippedArtifactDefinitionsAt(int slotIndex)
        {
            ArtifactInstance[] instances = GetEquippedArtifactInstancesAt(slotIndex);
            ArtifactDefinition[] definitions = new ArtifactDefinition[instances.Length];
            for (int i = 0; i < instances.Length; i++)
                definitions[i] = ResolveArtifactDefinition(instances[i]?.definitionId);
            return definitions;
        }

        public ArtifactRuntimeSnapshot[] GetEquippedArtifactRuntimeSnapshotsAt(int slotIndex)
        {
            ArtifactInstance[] instances = GetEquippedArtifactInstancesAt(slotIndex);
            ArtifactRuntimeSnapshot[] snapshots = new ArtifactRuntimeSnapshot[instances.Length];
            for (int i = 0; i < instances.Length; i++)
            {
                ArtifactInstance instance = instances[i];
                if (instance != null)
                    snapshots[i] = new ArtifactRuntimeSnapshot(instance, ResolveArtifactDefinition(instance.definitionId));
            }
            return snapshots;
        }

        public void SetEquippedArtifactsAt(int slotIndex, Artifact[] artifacts)
        {
            EnsureRunData();
            if (slotIndex < 0)
            {
                return;
            }

            EnsureTeamSlotCapacity(slotIndex + 1);
            teamSlots[slotIndex].SetArtifacts(artifacts, DefaultArtifactSlotCount);
            SavePersistentState();
        }

        public void SetActiveTeamSlotIndex(int slotIndex)
        {
            EnsureRunData();
            activeTeamSlotIndex = ResolveActiveSlotIndex(slotIndex);
            SavePersistentState();
        }

        public bool ContainsTeamCharacter(CharacterObject character)
        {
            return FindTeamSlot(character) >= 0;
        }

        public bool IsArtifactEquipped(Artifact artifact)
        {
            if (artifact == null)
            {
                return false;
            }

            EnsureRunData();
            for (int i = 0; i < artifactInstances.Count; i++)
            {
                ArtifactInstance instance = artifactInstances[i];
                if (instance != null
                    && string.Equals(instance.definitionId, artifact.id, StringComparison.Ordinal)
                    && IsArtifactEquipped(instance.instanceId)) return true;
            }

            return false;
        }

        public int FindTeamSlot(CharacterObject character)
        {
            if (character == null)
            {
                return -1;
            }

            EnsureRunData();
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i].Character == character)
                {
                    return i;
                }
            }

            // CharacterObject assets can desync by reference after load/deploy; match id as fallback.
            if (!string.IsNullOrWhiteSpace(character.characterId))
            {
                for (int i = 0; i < teamSlots.Length; i++)
                {
                    CharacterObject slotted = teamSlots[i].Character;
                    if (slotted != null
                        && string.Equals(slotted.characterId, character.characterId, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static bool IsSameTeamCharacter(CharacterObject a, CharacterObject b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (a == b)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(a.characterId)
                && string.Equals(a.characterId, b.characterId, StringComparison.Ordinal);
        }

        private bool IsRunCharacterAlive(CharacterObject character)
        {
            if (IsCharacterUnavailable(character))
            {
                return false;
            }

            RunCharacterState state = GetRunCharacter(character);
            if (state == null)
            {
                return true;
            }

            return state.currentHP < 0f || state.currentHP > 0.01f;
        }

        public CharacterUpgrade GetCharacterUpgrade(CharacterObject character)
        {
            // Compatibility API for the disabled individual EXP UI. Active stat
            // resolution uses GlobalLevel and does not require this record.
            RunCharacterState state = GetOrCreateRunCharacterState(character);
            return state != null ? state.upgrade : null;
        }

        public RunCharacterState GetRunCharacter(CharacterObject character)
        {
            if (character == null)
            {
                return null;
            }

            EnsureRunData();
            return FindRunCharacterState(character);
        }

        /// <summary>
        /// Writes run HP into inventory only (does not push onto the active PlayerCharacter).
        /// Used when knocking out the active character before searching for a living teammate.
        /// </summary>
        public void SetRunCharacterCurrentHP(CharacterObject character, float hp)
        {
            RunCharacterState state = GetOrCreateRunCharacterState(character);
            if (state == null)
            {
                return;
            }

            state.currentHP = Mathf.Max(0f, hp);
        }

        /// <summary>
        /// Writes run Aura Energy into inventory only (does not require TeamLoader UI notify).
        /// </summary>
        public void SetRunCharacterCurrentAE(CharacterObject character, float ae)
        {
            RunCharacterState state = GetOrCreateRunCharacterState(character);
            if (state == null)
            {
                return;
            }

            state.currentAE = Mathf.Max(0f, ae);
        }

        public CharacterStats GetCharacterStats(CharacterObject character)
        {
            CharacterStats stats = GetCharacterBaseStats(character);
            int slotIndex = FindTeamSlot(character);
            if (slotIndex >= 0)
            {
                ArtifactInstance[] artifacts = GetEquippedArtifactInstancesAt(slotIndex);
                CharacterStats artifactBaseStats = stats.Clone();
                ArtifactStatAggregator.Apply(stats, artifactBaseStats, artifacts);
            }
            // Live source-stat formulas may form a cross-character dependency cycle
            // (for example, A reads B's current stats while B reads A's). Keep the
            // outer calculation registered so source resolution can break that cycle
            // with the effect's captured source snapshot.
            if (!resolvingRuntimeStats.Add(character))
            {
                return stats;
            }

            try
            {
                ApplyRuntimeEffectsToStats(character, stats);
            }
            finally
            {
                resolvingRuntimeStats.Remove(character);
            }

            return stats;
        }

        public CharacterStats GetCharacterBaseStats(CharacterObject character)
        {
            if (character == null)
            {
                return new CharacterStats();
            }

            CharacterUpgrade authoredGrowth = character.upgrade;
            CharacterStats leveled = authoredGrowth != null
                ? authoredGrowth.CalculateStatsAtLevel(character.stats, GlobalLevel)
                : (character.stats != null ? character.stats.Clone() : new CharacterStats());

            if (character.movementProfile != null)
            {
                leveled.speed = character.movementProfile.moveSpeed;
            }

            return leveled;
        }

        public IReadOnlyList<RuntimeEffectInstance> GetActiveRuntimeEffects(CharacterObject character)
        {
            RunCharacterState state = GetRunCharacter(character);
            return state != null
                ? state.ActiveEffects
                : Array.Empty<RuntimeEffectInstance>();
        }

        public RuntimeEffectInstance GetRuntimeEffectByChannel(
            CharacterObject character,
            RuntimeEffectStateChannel channel)
        {
            RunCharacterState state = GetRunCharacter(character);
            return state != null
                ? RuntimeEffectRuntimeUtility.FindByStateChannel(state.ActiveEffects, channel)
                : null;
        }

        public int GetRuntimeEffectStackCount(CharacterObject character, string effectIdOrStackKey)
        {
            RunCharacterState state = GetRunCharacter(character);
            if (state == null || string.IsNullOrWhiteSpace(effectIdOrStackKey))
            {
                return 0;
            }

            int count = 0;
            List<RuntimeEffectInstance> activeEffects = state.MutableActiveEffects;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                RuntimeEffectInstance instance = activeEffects[i];
                if (RuntimeEffectMatchesKey(instance, effectIdOrStackKey))
                {
                    count += Mathf.Max(1, instance.stackCount);
                }
            }

            return count;
        }

        public bool ApplyRuntimeEffect(
            CharacterObject targetCharacter,
            RuntimeEffectDefinition effectDefinition,
            CharacterObject sourceCharacter = null,
            CharacterStats sourceStats = null,
            float durationOverride = -1f,
            RuntimeEffectStackMode stackMode = RuntimeEffectStackMode.UseDefinition,
            int stackLimit = 0,
            bool refreshDurationOnReapply = true)
        {
            return ApplyRuntimeEffectInternal(
                targetCharacter,
                effectDefinition,
                null,
                sourceCharacter,
                sourceStats,
                durationOverride,
                stackMode,
                stackLimit,
                refreshDurationOnReapply,
                0);
        }

        public bool ApplyRuntimeEffect(
            CharacterObject targetCharacter,
            RuntimeEffectRecipe recipe,
            CharacterObject sourceCharacter = null,
            CharacterStats sourceStats = null,
            float durationOverride = -1f,
            int stackLimitOverride = 0)
        {
            if (recipe == null || recipe.definition == null)
            {
                return false;
            }

            return ApplyRuntimeEffectInternal(
                targetCharacter,
                recipe.definition,
                recipe,
                sourceCharacter,
                sourceStats,
                durationOverride,
                RuntimeEffectRuntimeUtility.GetStackMode(recipe.definition, recipe),
                RuntimeEffectRuntimeUtility.GetStackLimit(recipe, 1),
                RuntimeEffectRuntimeUtility.GetRefreshDurationOnReapply(recipe, true),
                stackLimitOverride);
        }

        private bool ApplyRuntimeEffectInternal(
            CharacterObject targetCharacter,
            RuntimeEffectDefinition effectDefinition,
            RuntimeEffectRecipe recipe,
            CharacterObject sourceCharacter,
            CharacterStats sourceStats,
            float durationOverride,
            RuntimeEffectStackMode stackMode,
            int stackLimit,
            bool refreshDurationOnReapply,
            int stackLimitOverride)
        {
            if (targetCharacter == null || effectDefinition == null)
            {
                return false;
            }

            RunCharacterState state = GetOrCreateRunCharacterState(targetCharacter);
            if (state == null)
            {
                return false;
            }

            state.EnsureEffectList();
            List<RuntimeEffectInstance> activeEffects = state.MutableActiveEffects;
            int fallbackStackLimit = stackLimit > 0
                ? stackLimit
                : RuntimeEffectRuntimeUtility.GetDefaultStackLimit(effectDefinition);
            int resolvedStackLimit = stackLimitOverride > 0
                ? stackLimitOverride
                : RuntimeEffectRuntimeUtility.GetStackLimit(recipe, fallbackStackLimit);
            bool resolvedRefreshDuration = RuntimeEffectRuntimeUtility.GetRefreshDurationOnReapply(recipe, refreshDurationOnReapply);
            bool effectRequiresTicking = RuntimeEffectRuntimeUtility.RequiresTicking(effectDefinition, recipe);
            bool effectAffectsStats = RuntimeEffectRuntimeUtility.AffectsStats(effectDefinition, recipe);
            float resolvedDuration = RuntimeEffectRuntimeUtility.ResolveDuration(effectDefinition, recipe, durationOverride);
            bool shouldStack = ResolveShouldStack(effectDefinition, recipe, stackMode);
            CharacterStats resolvedSourceStats = sourceStats != null
                ? sourceStats.Clone()
                : (sourceCharacter != null ? GetCharacterStats(sourceCharacter) : GetCharacterBaseStats(targetCharacter));

            RuntimeEffectStateChannel stateChannel =
                RuntimeEffectRuntimeUtility.GetStateChannel(effectDefinition, recipe);
            RuntimeEffectInstance instance = stateChannel != RuntimeEffectStateChannel.None
                ? FindAndCollapseStateChannel(activeEffects, stateChannel)
                : !shouldStack ? FindRuntimeEffect(activeEffects, effectDefinition, recipe) : null;

            if (instance != null && stateChannel != RuntimeEffectStateChannel.None)
            {
                bool affectsStats = effectAffectsStats || instance.AffectsStats;
                instance.Rebind(effectDefinition, recipe, sourceCharacter, resolvedSourceStats);
                instance.stackCount = 1;
                instance.Refresh(durationOverride);
                effectDefinition.OnReapplied(BuildRuntimeEffectContext(targetCharacter, state, instance, 0f));
                state.RecalculateTickingEffects();

                if (instance.IsExpired
                    || RuntimeEffectRuntimeUtility.GetDurationType(effectDefinition, recipe) == RuntimeEffectDurationType.Instant)
                {
                    RemoveRuntimeEffect(targetCharacter, state, instance);
                    return true;
                }

                RaiseRuntimeEffectsChanged(targetCharacter, affectsStats);
                return true;
            }

            if (shouldStack && effectDefinition.SupportsStackMerging)
            {
                RuntimeEffectInstance stackTarget = FindMergeableRuntimeEffect(
                    activeEffects,
                    effectDefinition,
                    recipe,
                    sourceCharacter,
                    resolvedSourceStats,
                    resolvedDuration);
                if (stackTarget != null)
                {
                    stackTarget.Rebind(effectDefinition, recipe, sourceCharacter, resolvedSourceStats);
                    if (stackTarget.stackCount < resolvedStackLimit)
                    {
                        stackTarget.AddStack(durationOverride, resolvedRefreshDuration);
                    }
                    else if (resolvedRefreshDuration)
                    {
                        stackTarget.Refresh(durationOverride);
                    }
                    effectDefinition.OnReapplied(BuildRuntimeEffectContext(targetCharacter, state, stackTarget, 0f));

                    if (effectRequiresTicking)
                    {
                        state.HasTickingEffects = true;
                    }

                    RaiseRuntimeEffectsChanged(targetCharacter, effectAffectsStats);
                    return true;
                }
            }

            if (instance != null)
            {
                bool affectsStats = effectAffectsStats || instance.AffectsStats;

                if (instance.definition != effectDefinition || instance.recipe != recipe)
                {
                    if (instance.definition == effectDefinition && effectDefinition.ReapplyWhenRecipeChanges)
                    {
                        instance.Rebind(recipe, sourceCharacter, resolvedSourceStats);
                        if (resolvedRefreshDuration)
                        {
                            instance.Refresh(durationOverride);
                        }
                        effectDefinition.OnReapplied(BuildRuntimeEffectContext(targetCharacter, state, instance, 0f));
                    }
                    else
                    {
                        instance.definition?.OnExpired(BuildRuntimeEffectContext(targetCharacter, state, instance, 0f));
                        if (recipe != null)
                        {
                            instance.Initialize(recipe, sourceCharacter, resolvedSourceStats, durationOverride);
                        }
                        else
                        {
                            instance.Initialize(effectDefinition, sourceCharacter, resolvedSourceStats, durationOverride);
                        }
                        effectDefinition.OnApply(BuildRuntimeEffectContext(targetCharacter, state, instance, 0f));
                    }
                }
                else if (resolvedRefreshDuration)
                {
                    instance.Refresh(durationOverride);
                    effectDefinition.OnReapplied(BuildRuntimeEffectContext(targetCharacter, state, instance, 0f));
                }
                else
                {
                    effectDefinition.OnReapplied(BuildRuntimeEffectContext(targetCharacter, state, instance, 0f));
                }

                if (effectRequiresTicking)
                {
                    state.HasTickingEffects = true;
                }

                if (instance.IsExpired
                    || RuntimeEffectRuntimeUtility.GetDurationType(effectDefinition, recipe) == RuntimeEffectDurationType.Instant)
                {
                    RemoveRuntimeEffect(targetCharacter, state, instance);
                    return true;
                }

                RaiseRuntimeEffectsChanged(targetCharacter, affectsStats);
                return true;
            }

            if (shouldStack && CountRuntimeEffects(activeEffects, effectDefinition, recipe) >= resolvedStackLimit)
            {
                RuntimeEffectInstance refreshTarget = FindOldestRuntimeEffect(activeEffects, effectDefinition, recipe);
                if (refreshTarget != null && resolvedRefreshDuration)
                {
                    refreshTarget.Refresh(durationOverride);
                }
                if (refreshTarget != null)
                {
                    effectDefinition.OnReapplied(BuildRuntimeEffectContext(targetCharacter, state, refreshTarget, 0f));
                }

                if (effectRequiresTicking)
                {
                    state.HasTickingEffects = true;
                }

                RaiseRuntimeEffectsChanged(targetCharacter, effectAffectsStats);
                return refreshTarget != null;
            }

            instance = new RuntimeEffectInstance();
            if (recipe != null)
            {
                instance.Initialize(recipe, sourceCharacter, resolvedSourceStats, durationOverride);
            }
            else
            {
                instance.Initialize(effectDefinition, sourceCharacter, resolvedSourceStats, durationOverride);
            }
            activeEffects.Add(instance);
            if (effectRequiresTicking)
            {
                state.HasTickingEffects = true;
            }
            effectDefinition.OnApply(BuildRuntimeEffectContext(targetCharacter, state, instance, 0f));
            RaiseRuntimeEffectsChanged(targetCharacter, effectAffectsStats);

            if (instance.IsExpired
                || RuntimeEffectRuntimeUtility.GetDurationType(effectDefinition, recipe) == RuntimeEffectDurationType.Instant)
            {
                RemoveRuntimeEffect(targetCharacter, state, instance);
            }

            return true;
        }

        private static bool ResolveShouldStack(
            RuntimeEffectDefinition definition,
            RuntimeEffectRecipe recipe,
            RuntimeEffectStackMode stackMode)
        {
            return stackMode switch
            {
                RuntimeEffectStackMode.Override => false,
                RuntimeEffectStackMode.Stack => true,
                _ => RuntimeEffectRuntimeUtility.IsStackable(definition, recipe)
            };
        }

        public void ClearRuntimeEffects(CharacterObject character)
        {
            RunCharacterState state = GetRunCharacter(character);
            if (state == null || state.MutableActiveEffects.Count == 0)
            {
                return;
            }

            bool affectedStats = false;
            for (int i = 0; i < state.MutableActiveEffects.Count; i++)
            {
                RuntimeEffectInstance instance = state.MutableActiveEffects[i];
                affectedStats |= instance != null && instance.AffectsStats;
            }

            state.MutableActiveEffects.Clear();
            state.HasTickingEffects = false;
            RaiseRuntimeEffectsChanged(character, affectedStats);
        }

        public bool RemoveRuntimeEffect(
            CharacterObject targetCharacter,
            RuntimeEffectDefinition effectDefinition,
            RuntimeEffectRecipe recipe,
            CharacterObject sourceCharacter = null)
        {
            RunCharacterState state = GetRunCharacter(targetCharacter);
            if (state == null || effectDefinition == null || state.MutableActiveEffects.Count == 0)
            {
                return false;
            }

            bool changed = false;
            for (int i = state.MutableActiveEffects.Count - 1; i >= 0; i--)
            {
                RuntimeEffectInstance instance = state.MutableActiveEffects[i];
                if (instance == null || instance.definition == null)
                {
                    continue;
                }

                if (!RuntimeEffectRuntimeUtility.MatchesIdentity(instance, effectDefinition, recipe))
                {
                    continue;
                }

                if (sourceCharacter != null && instance.sourceCharacter != sourceCharacter)
                {
                    continue;
                }

                RemoveRuntimeEffect(targetCharacter, state, instance);
                changed = true;
            }

            return changed;
        }

        public bool RemoveRuntimeEffectStack(
            CharacterObject targetCharacter,
            RuntimeEffectDefinition effectDefinition,
            RuntimeEffectRecipe recipe,
            int count = 1,
            CharacterObject sourceCharacter = null)
        {
            RunCharacterState state = GetRunCharacter(targetCharacter);
            if (state == null || effectDefinition == null || state.MutableActiveEffects.Count == 0)
            {
                return false;
            }

            for (int i = state.MutableActiveEffects.Count - 1; i >= 0; i--)
            {
                RuntimeEffectInstance instance = state.MutableActiveEffects[i];
                if (instance == null || instance.definition == null)
                {
                    continue;
                }

                if (!RuntimeEffectRuntimeUtility.MatchesIdentity(instance, effectDefinition, recipe))
                {
                    continue;
                }

                if (sourceCharacter != null && instance.sourceCharacter != sourceCharacter)
                {
                    continue;
                }

                bool affectsStats = instance.AffectsStats;
                if (instance.RemoveStack(count))
                {
                    RemoveRuntimeEffect(targetCharacter, state, instance);
                }
                else
                {
                    RaiseRuntimeEffectsChanged(targetCharacter, affectsStats);
                }

                return true;
            }

            return false;
        }

        public void RemoveRuntimeEffectsOnOwnerDamaged(CharacterObject character)
        {
            RunCharacterState state = GetRunCharacter(character);
            if (state == null || state.MutableActiveEffects.Count == 0)
            {
                return;
            }

            bool affectedStats = false;
            bool changed = false;
            for (int i = state.MutableActiveEffects.Count - 1; i >= 0; i--)
            {
                RuntimeEffectInstance instance = state.MutableActiveEffects[i];
                if (instance == null || instance.definition == null)
                {
                    state.MutableActiveEffects.RemoveAt(i);
                    changed = true;
                    affectedStats = true;
                    continue;
                }

                if (!RuntimeEffectRuntimeUtility.RemoveOnOwnerDamaged(instance))
                {
                    continue;
                }

                affectedStats |= instance.AffectsStats;
                instance.definition.OnExpired(BuildRuntimeEffectContext(character, state, instance, 0f));
                state.MutableActiveEffects.RemoveAt(i);
                changed = true;
            }

            if (changed)
            {
                state.RecalculateTickingEffects();
                RaiseRuntimeEffectsChanged(character, affectedStats);
            }
        }

        public void TickRuntimeEffects(float deltaTime)
        {
            EnsureRunData();
            float resolvedDelta = Mathf.Max(0f, deltaTime);

            for (int stateIndex = 0; stateIndex < runCharacters.Count; stateIndex++)
            {
                RunCharacterState state = runCharacters[stateIndex];
                if (state == null || state.character == null)
                {
                    continue;
                }

                state.EnsureEffectList();
                List<RuntimeEffectInstance> activeEffects = state.MutableActiveEffects;
                if (!state.HasTickingEffects)
                {
                    continue;
                }

                CharacterStats targetStatsSnapshot = null;
                bool changed = false;
                bool affectedStats = false;
                for (int effectIndex = activeEffects.Count - 1; effectIndex >= 0; effectIndex--)
                {
                    RuntimeEffectInstance instance = activeEffects[effectIndex];
                    if (instance == null || instance.definition == null)
                    {
                        activeEffects.RemoveAt(effectIndex);
                        changed = true;
                        affectedStats = true;
                        continue;
                    }

                    if (!instance.RequiresTicking)
                    {
                        continue;
                    }

                    if (instance.UsesTimer)
                    {
                        instance.remainingSeconds -= resolvedDelta;
                    }

                    instance.elapsedSeconds += resolvedDelta;
                    if (instance.RequiresFormulaUpdate
                        && instance.definition.IsFormulaUpdateDue(instance))
                    {
                        targetStatsSnapshot ??= GetCharacterStats(state.character);
                        instance.definition.OnUpdate(BuildRuntimeEffectContext(
                            state.character,
                            state,
                            instance,
                            resolvedDelta,
                            targetStatsSnapshot));
                    }

                    if (!instance.IsExpired)
                    {
                        continue;
                    }

                    affectedStats |= instance.AffectsStats;
                    instance.definition.OnExpired(BuildRuntimeEffectContext(
                        state.character,
                        state,
                        instance,
                        0f,
                        targetStatsSnapshot));
                    activeEffects.RemoveAt(effectIndex);
                    changed = true;
                }

                if (changed)
                {
                    state.RecalculateTickingEffects();
                    RaiseRuntimeEffectsChanged(state.character, affectedStats);
                }
            }
        }

        public void ApplyRuntimeEffectsToStats(CharacterObject character, CharacterStats stats)
        {
            if (character == null || stats == null)
            {
                return;
            }

            RunCharacterState state = GetRunCharacter(character);
            if (state == null)
            {
                return;
            }

            float[] multiplierSums = RentRuntimeEffectMultiplierSums();
            try
            {
                List<RuntimeEffectInstance> activeEffects = state.MutableActiveEffects;
                for (int i = 0; i < activeEffects.Count; i++)
                {
                    RuntimeEffectInstance instance = activeEffects[i];
                    if (instance == null || instance.definition == null)
                    {
                        continue;
                    }

                    RuntimeEffectContext context = BuildRuntimeEffectContext(
                        character,
                        state,
                        instance,
                        0f,
                        stats,
                        GetCharacterBaseStats(character));
                    context.multiplierSums = multiplierSums;

                    instance.definition.ModifyStats(context, stats);
                }

                for (int i = 0; i < multiplierSums.Length; i++)
                {
                    float sum = multiplierSums[i];
                    if (Mathf.Abs(sum) > 0.0001f)
                    {
                        RuntimeEffectStatTarget targetStat = (RuntimeEffectStatTarget)i;
                        float currentVal = RuntimeEffectDefinition.GetStat(stats, targetStat);
                        float multipliedVal = currentVal * (1f + sum);
                        RuntimeEffectDefinition.SetStat(stats, targetStat, multipliedVal);
                    }
                }
            }
            finally
            {
                ReturnRuntimeEffectMultiplierSums(multiplierSums);
            }
        }

        private float[] RentRuntimeEffectMultiplierSums()
        {
            float[] multiplierSums = runtimeEffectMultiplierSumPool.Count > 0
                ? runtimeEffectMultiplierSumPool.Pop()
                : new float[RuntimeEffectStatTargetCount];
            Array.Clear(multiplierSums, 0, multiplierSums.Length);
            return multiplierSums;
        }

        private void ReturnRuntimeEffectMultiplierSums(float[] multiplierSums)
        {
            if (multiplierSums == null || multiplierSums.Length != RuntimeEffectStatTargetCount)
            {
                return;
            }

            runtimeEffectMultiplierSumPool.Push(multiplierSums);
        }

        public float ModifyOutgoingDamage(
            CharacterObject sourceCharacter,
            RuntimeEffectDamageContext context,
            float currentDamage)
        {
            return ModifyDamageWithRuntimeEffects(sourceCharacter, context, currentDamage, outgoing: true);
        }

        public float ModifyIncomingDamage(
            CharacterObject targetCharacter,
            RuntimeEffectDamageContext context,
            float currentDamage)
        {
            return ModifyDamageWithRuntimeEffects(targetCharacter, context, currentDamage, outgoing: false);
        }

        public void NotifyRuntimeHitDealt(CharacterObject sourceCharacter, RuntimeEffectDamageContext context)
        {
            RunCharacterState state = GetRunCharacter(sourceCharacter);
            if (state == null || context == null)
            {
                return;
            }

            List<RuntimeEffectInstance> activeEffects = state.MutableActiveEffects;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                RuntimeEffectInstance instance = activeEffects[i];
                if (instance == null || instance.definition == null)
                {
                    continue;
                }

                RuntimeEffectRuntimeUtility.BindDamageContextToInstance(
                    context,
                    instance,
                    out RuntimeEffectInstance previousInstance,
                    out CharacterObject previousSourceCharacter,
                    out CharacterStats previousSourceStats);
                instance.definition.OnHitDealt(context);
                RuntimeEffectRuntimeUtility.RestoreDamageContext(
                    context,
                    previousInstance,
                    previousSourceCharacter,
                    previousSourceStats);
            }

            context.instance = null;
        }

        /// <summary>
        /// Checkpoint written only when a boss clear returns to the lobby.
        /// Death deletes it, so Continue starts at Level 1. Menu New Game deletes it too.
        /// </summary>
        public void SaveBossLobbyCheckpoint()
        {
            if (!PersistenceEnabled) return;
            if (!Application.isPlaying || isApplyingPersistentState)
            {
                return;
            }

            SaveSlotStore.EnsureMigrated();
            PlayerPrefs.SetString(BossLobbySaveKey, JsonUtility.ToJson(CaptureRunSnapshot()));
            CharacterProgressionState meta = ResolvePersistenceProgression();
            if (meta != null)
            {
                PlayerPrefs.SetString(BossLobbyMetaSaveKey, meta.ExportJson());
                meta.MarkCommitted();
            }

            PublishActiveSaveHeader();
            PlayerPrefs.Save();
        }

        public bool TryRestoreBossLobbyCheckpoint()
        {
            SaveSlotStore.EnsureMigrated();
            string json = PlayerPrefs.GetString(BossLobbySaveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            SavedPlayerRunInventoryData data = JsonUtility.FromJson<SavedPlayerRunInventoryData>(json);
            if (data == null)
            {
                return false;
            }

            hasLoadedPersistentState = true;
            isDeathTeardown = false;
            isDeathLobby = false;
            pendingLobbyMainReset = false;
            suppressAutoBootstrap = false;
            EnsureInventoryData();
            EnsureTeamSlotCapacity(DefaultTeamSlotCount);
            ApplyPersistentState(data);
            ApplyBossLobbyBiome(data.biomeIndex);
            RestoreBossLobbyMeta();
            return true;
        }

        public bool TryReadBossLobbyBiome(out int biomeIndex)
        {
            biomeIndex = 0;
            SaveSlotStore.EnsureMigrated();
            string json = PlayerPrefs.GetString(BossLobbySaveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            SavedPlayerRunInventoryData data = JsonUtility.FromJson<SavedPlayerRunInventoryData>(json);
            if (data == null)
            {
                return false;
            }

            biomeIndex = Mathf.Max(0, data.biomeIndex);
            return true;
        }

        private static void ApplyBossLobbyBiome(int biomeIndex)
        {
            RunProgressionManager progression = RunProgressionManager.EnsureInstance();
            progression?.SetCurrentBiomeIndex(biomeIndex);
        }

        public void RestoreBossLobbyMeta()
        {
            if (!PersistenceEnabled) return;
            string json = PlayerPrefs.GetString(BossLobbyMetaSaveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            CharacterProgressionState meta = ResolvePersistenceProgression();
            if (meta == null)
            {
                return;
            }

            meta.ImportJson(json);
            meta.MarkCommitted();
            meta.SavePersistentState();
        }

        public void PublishActiveSaveHeader()
        {
            if (!PersistenceEnabled) return;
            if (!Application.isPlaying)
            {
                return;
            }

            SaveSlotStore.EnsureMigrated();
            SaveSlotStore.WriteActiveHeader(BuildSaveHeader());
        }

        public static bool TryBuildHeaderFromBossJson(string json, out SaveSlotHeader header)
        {
            header = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            SavedPlayerRunInventoryData data;
            try
            {
                data = JsonUtility.FromJson<SavedPlayerRunInventoryData>(json);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("[PlayerRunInventory] Boss checkpoint preview is invalid. Original checkpoint data was preserved.");
                return false;
            }
            if (data == null)
            {
                return false;
            }

            header = new SaveSlotHeader
            {
                biomeIndex = Mathf.Max(0, data.biomeIndex),
                globalLevel = Mathf.Max(1, data.globalLevel),
                currency = Mathf.Max(0, data.currency),
                characterIds = new string[SaveSlotStore.PortraitCount],
                dead = new bool[SaveSlotStore.PortraitCount]
            };

            if (data.teamSlots == null)
            {
                return true;
            }

            int count = Mathf.Min(SaveSlotStore.PortraitCount, data.teamSlots.Count);
            for (int i = 0; i < count; i++)
            {
                SavedRunTeamSlotRecord slot = data.teamSlots[i];
                string characterId = slot != null ? slot.characterId : null;
                header.characterIds[i] = characterId;
                header.dead[i] = IsSavedCharacterDead(data, characterId);
            }

            return true;
        }

        private SaveSlotHeader BuildSaveHeader()
        {
            EnsureRunData();
            int biome = 0;
            if (RunProgressionManager.Instance != null)
            {
                biome = RunProgressionManager.Instance.CurrentBiomeIndex;
            }
            else if (TryReadBossLobbyBiome(out int savedBiome))
            {
                biome = savedBiome;
            }

            SaveSlotHeader header = new SaveSlotHeader
            {
                biomeIndex = Mathf.Max(0, biome),
                globalLevel = Mathf.Max(1, GlobalLevel),
                currency = Mathf.Max(0, currency),
                characterIds = new string[SaveSlotStore.PortraitCount],
                dead = new bool[SaveSlotStore.PortraitCount]
            };

            int count = Mathf.Min(SaveSlotStore.PortraitCount, TeamSlotCount);
            for (int i = 0; i < count; i++)
            {
                CharacterObject character = GetTeamCharacterAt(i);
                if (character == null || string.IsNullOrWhiteSpace(character.characterId))
                {
                    continue;
                }

                header.characterIds[i] = character.characterId;
                RunCharacterState state = GetTeamRunCharacterAt(i);
                bool hpDead = state != null && state.currentHP >= 0f && state.currentHP <= 0.01f;
                header.dead[i] = hpDead || IsCharacterDefeated(character);
            }

            return header;
        }

        private static bool IsSavedCharacterDead(SavedPlayerRunInventoryData data, string characterId)
        {
            if (data == null || data.runCharacters == null || string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            for (int i = 0; i < data.runCharacters.Count; i++)
            {
                SavedRunCharacterRecord record = data.runCharacters[i];
                if (record == null || record.characterId != characterId)
                {
                    continue;
                }

                return record.currentHP >= 0f && record.currentHP <= 0.01f;
            }

            return false;
        }

        public void ClearBossLobbyCheckpoint()
        {
            if (!PersistenceEnabled) return;
            SaveSlotStore.EnsureMigrated();
            bool changed = false;
            if (PlayerPrefs.HasKey(BossLobbySaveKey))
            {
                PlayerPrefs.DeleteKey(BossLobbySaveKey);
                changed = true;
            }

            if (PlayerPrefs.HasKey(BossLobbyMetaSaveKey))
            {
                PlayerPrefs.DeleteKey(BossLobbyMetaSaveKey);
                changed = true;
            }

            if (changed)
            {
                PlayerPrefs.Save();
            }
        }

        public void SavePersistentState()
        {
            if (!ShouldUsePersistentStorage() || isApplyingPersistentState)
            {
                return;
            }

            if (!PersistRunInventoryAcrossSessions)
            {
                return;
            }

            EnsureRunData();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(CaptureRunSnapshot()));
            PlayerPrefs.Save();
        }

        private SavedPlayerRunInventoryData CaptureRunSnapshot()
        {

            SavedPlayerRunInventoryData data = new SavedPlayerRunInventoryData
            {
                schemaVersion = 3,
                currency = currency,
                globalLevel = _globalLevel,
                biomeIndex = RunProgressionManager.Instance != null
                    ? RunProgressionManager.Instance.CurrentBiomeIndex
                    : 0,
                activeTeamSlotIndex = activeTeamSlotIndex
            };

            HashSet<BaseItem> processedItems = new HashSet<BaseItem>();
            for (int i = 0; i < ownedItems.Count; i++)
            {
                BaseItem item = ownedItems[i];
                if (item == null || processedItems.Contains(item) || string.IsNullOrWhiteSpace(item.id))
                {
                    continue;
                }

                processedItems.Add(item);
                data.items.Add(new SavedInventoryItemRecord
                {
                    itemId = item.id,
                    quantity = Mathf.Max(1, GetItemCount(item))
                });
            }

            for (int i = 0; i < runCharacters.Count; i++)
            {
                RunCharacterState state = runCharacters[i];
                if (state == null || state.character == null || string.IsNullOrWhiteSpace(state.character.characterId))
                {
                    continue;
                }

                data.runCharacters.Add(new SavedRunCharacterRecord
                {
                    characterId = state.character.characterId,
                    upgrade = state.upgrade != null ? state.upgrade.Clone() : new CharacterUpgrade(),
                    currentHP = state.currentHP,
                    currentAE = state.currentAE
                });
            }

            for (int i = 0; i < artifactInstances.Count; i++)
            {
                ArtifactInstance instance = artifactInstances[i];
                if (instance == null
                    || string.IsNullOrWhiteSpace(instance.instanceId)
                    || string.IsNullOrWhiteSpace(instance.definitionId))
                    continue;

                data.artifactInstances.Add(new SavedArtifactInstanceRecord
                {
                    instanceId = instance.instanceId,
                    definitionId = instance.definitionId,
                    rolls = instance.rolls != null ? new List<ArtifactRoll>(instance.rolls) : new List<ArtifactRoll>(),
                    isFavourite = instance.isFavourite,
                    isNew = instance.isNew,
                    acquisitionOrder = instance.acquisitionOrder,
                    collectedTimestampUtc = instance.collectedTimestampUtc,
                    generationSeed = instance.generationSeed,
                    acquisitionBiomeIndex = instance.acquisitionBiomeIndex
                });
            }

            for (int i = 0; i < teamSlots.Length; i++)
            {
                RunTeamSlot slot = teamSlots[i];
                SavedRunTeamSlotRecord savedSlot = new SavedRunTeamSlotRecord
                {
                    characterId = slot != null && slot.Character != null ? slot.Character.characterId : string.Empty
                };

                ArtifactInstanceRef[] artifactRefs = slot != null
                    ? slot.GetArtifactRefsSnapshot(DefaultArtifactSlotCount)
                    : Array.Empty<ArtifactInstanceRef>();
                savedSlot.artifactInstanceIds = new string[artifactRefs.Length];
                for (int artifactIndex = 0; artifactIndex < artifactRefs.Length; artifactIndex++)
                {
                    savedSlot.artifactInstanceIds[artifactIndex] = artifactRefs[artifactIndex].instanceId ?? string.Empty;
                }

                data.teamSlots.Add(savedSlot);
            }

            return data;
        }

        public void LoadPersistentStateIfNeeded()
        {
            EnsureTransientRuntimeStateForPlaySession();
            if (!ShouldUsePersistentStorage())
            {
                EnsureInventoryData();
                EnsureTeamSlotCapacity(DefaultTeamSlotCount);
                EnsurePlayableState();
                return;
            }

            if (hasLoadedPersistentState)
            {
                return;
            }

            hasLoadedPersistentState = true;
            EnsureInventoryData();
            EnsureTeamSlotCapacity(DefaultTeamSlotCount);

            if (!PersistRunInventoryAcrossSessions)
            {
                if (PlayerPrefs.HasKey(SaveKey))
                {
                    PlayerPrefs.DeleteKey(SaveKey);
                    PlayerPrefs.Save();
                }

                EnsurePlayableState();
                return;
            }

            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            bool loadedLegacySave = false;
            if (string.IsNullOrWhiteSpace(json))
            {
                json = PlayerPrefs.GetString(LegacySaveKey, string.Empty);
                loadedLegacySave = !string.IsNullOrWhiteSpace(json);
            }
            if (string.IsNullOrWhiteSpace(json))
            {
                EnsurePlayableState();
                SavePersistentState();
                return;
            }

            SavedPlayerRunInventoryData data = JsonUtility.FromJson<SavedPlayerRunInventoryData>(json);
            if (data == null)
            {
                EnsurePlayableState();
                SavePersistentState();
                return;
            }

            if (ShouldPreferAuthoredBootstrapOverSavedData(data))
            {
                Debug.LogWarning("PlayerRunInventory: Existing PlayerPrefs is missing authored bootstrap roster data. Restoring authored PlayerRunInventory asset data.");
                EnsurePlayableState();
                SavePersistentState();
                return;
            }

            ApplyPersistentState(data);
            if (loadedLegacySave)
            {
                Debug.Log("PlayerRunInventory: Migrated v2 Artifact definition ownership to v3 Artifact instances. The v2 save remains available for rollback.");
            }
            SavePersistentState();
        }

        private void ClearAllInventoryData()
        {
            if (ownedItems != null) ownedItems.Clear();
            if (itemStacks != null) itemStacks.Clear();
            if (artifactInstances != null) artifactInstances.Clear();
            artifactInstancesById?.Clear();
            if (runCharacters != null) runCharacters.Clear();
            if (teamSlots != null)
            {
                for (int i = 0; i < teamSlots.Length; i++)
                {
                    if (teamSlots[i] != null)
                    {
                        teamSlots[i].Character = null;
                        teamSlots[i].SetArtifacts(null, DefaultArtifactSlotCount);
                        teamSlots[i].SetArtifactRefs(null, DefaultArtifactSlotCount);
                    }
                }
            }
        }

        public void NotifyRunStateChanged()
        {
            SavePersistentState();
        }

        private RuntimeEffectContext BuildRuntimeEffectContext(
            CharacterObject targetCharacter,
            RunCharacterState state,
            RuntimeEffectInstance instance,
            float deltaTime,
            CharacterStats targetStatsOverride = null,
            CharacterStats targetBaseStatsOverride = null)
        {
            PlayerCharacter activePlayer = FindActivePlayerFor(targetCharacter);
            CharacterObject sourceCharacter = instance != null ? instance.sourceCharacter : null;
            CharacterStats resolvedTargetStats = targetStatsOverride ?? (targetCharacter != null ? GetCharacterStats(targetCharacter) : null);
            CharacterStats resolvedTargetBaseStats = targetBaseStatsOverride ?? (targetCharacter != null ? GetCharacterBaseStats(targetCharacter) : null);
            CharacterStats resolvedSourceStats = ResolveRuntimeEffectSourceStats(
                sourceCharacter,
                targetCharacter,
                resolvedTargetStats,
                instance);
            CharacterStats resolvedSourceBaseStats = ResolveRuntimeEffectSourceBaseStats(
                sourceCharacter,
                targetCharacter,
                resolvedTargetBaseStats);
            return new RuntimeEffectContext
            {
                runInventory = this,
                instance = instance,
                sourceCharacter = sourceCharacter,
                targetCharacter = targetCharacter,
                sourceStats = resolvedSourceStats,
                sourceBaseStats = resolvedSourceBaseStats,
                targetStats = resolvedTargetStats,
                targetBaseStats = resolvedTargetBaseStats,
                targetPlayer = activePlayer,
                deltaTime = deltaTime
            };
        }

        private CharacterStats ResolveRuntimeEffectSourceStats(
            CharacterObject sourceCharacter,
            CharacterObject targetCharacter,
            CharacterStats targetStats,
            RuntimeEffectInstance instance)
        {
            CharacterStats snapshot = instance != null
                ? instance.sourceStatsSnapshot
                : null;
            if (sourceCharacter == null)
            {
                return snapshot ?? targetStats;
            }

            if (sourceCharacter == targetCharacter)
            {
                return snapshot ?? targetStats;
            }

            return snapshot ?? GetCharacterBaseStats(sourceCharacter);
        }

        private CharacterStats ResolveRuntimeEffectSourceBaseStats(
            CharacterObject sourceCharacter,
            CharacterObject targetCharacter,
            CharacterStats targetBaseStats)
        {
            if (sourceCharacter == null)
            {
                return targetBaseStats;
            }

            if (sourceCharacter == targetCharacter)
            {
                return targetBaseStats;
            }

            return GetCharacterBaseStats(sourceCharacter);
        }

        private float ModifyDamageWithRuntimeEffects(
            CharacterObject character,
            RuntimeEffectDamageContext context,
            float currentDamage,
            bool outgoing)
        {
            RunCharacterState state = GetRunCharacter(character);
            if (state == null || context == null)
            {
                return currentDamage;
            }

            using RuntimeEffectMutationBatch mutationBatch = BeginRuntimeEffectMutationBatch();
            float result = currentDamage;
            expiredRuntimeEffects.Clear();
            bool shieldAmountChanged = false;
            List<RuntimeEffectInstance> activeEffects = state.MutableActiveEffects;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                RuntimeEffectInstance instance = activeEffects[i];
                if (instance == null || instance.definition == null)
                {
                    continue;
                }

                RuntimeEffectRuntimeUtility.BindDamageContextToInstance(
                    context,
                    instance,
                    out RuntimeEffectInstance previousInstance,
                    out CharacterObject previousSourceCharacter,
                    out CharacterStats previousSourceStats);
                float previousShieldAmount = instance.remainingShieldAmount;
                result = outgoing
                    ? instance.definition.ModifyOutgoingDamage(context, result)
                    : instance.definition.ModifyIncomingDamage(context, result);
                if (!outgoing && !Mathf.Approximately(previousShieldAmount, instance.remainingShieldAmount))
                {
                    shieldAmountChanged = true;
                }
                RuntimeEffectRuntimeUtility.RestoreDamageContext(
                    context,
                    previousInstance,
                    previousSourceCharacter,
                    previousSourceStats);
                if (instance.IsExpired)
                {
                    expiredRuntimeEffects.Add(instance);
                }
            }

            if (shieldAmountChanged)
            {
                RaiseRuntimeEffectsChanged(character, false);
            }

            if (expiredRuntimeEffects.Count > 0)
            {
                for (int i = 0; i < expiredRuntimeEffects.Count; i++)
                {
                    RemoveRuntimeEffect(character, state, expiredRuntimeEffects[i]);
                }

                expiredRuntimeEffects.Clear();
            }

            context.instance = null;
            return Mathf.Max(0f, result);
        }

        public void ModifyDamageInput(CharacterObject character, RuntimeEffectDamageInput input)
        {
            RunCharacterState state = GetRunCharacter(character);
            if (state == null || input == null || input.context == null)
            {
                return;
            }

            List<RuntimeEffectInstance> activeEffects = state.MutableActiveEffects;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                RuntimeEffectInstance instance = activeEffects[i];
                if (instance == null || instance.definition == null)
                {
                    continue;
                }

                RuntimeEffectRuntimeUtility.BindDamageContextToInstance(
                    input.context,
                    instance,
                    out RuntimeEffectInstance previousInstance,
                    out CharacterObject previousSourceCharacter,
                    out CharacterStats previousSourceStats);
                instance.definition.ModifyDamageInput(input);
                RuntimeEffectRuntimeUtility.RestoreDamageContext(
                    input.context,
                    previousInstance,
                    previousSourceCharacter,
                    previousSourceStats);
            }

            input.context.instance = null;
            input.baseDamage = Mathf.Max(0f, input.baseDamage);
            input.defenderDefense = Mathf.Max(0f, input.defenderDefense);
            input.defenderDamageResistance = Mathf.Clamp(input.defenderDamageResistance, -95f, 95f);
        }

        private void RemoveRuntimeEffect(
            CharacterObject targetCharacter,
            RunCharacterState state,
            RuntimeEffectInstance instance)
        {
            if (state == null || instance == null)
            {
                return;
            }

            bool affectsStats = instance.AffectsStats;
            instance.definition?.OnExpired(BuildRuntimeEffectContext(targetCharacter, state, instance, 0f));
            state.MutableActiveEffects.Remove(instance);
            if (instance.RequiresTicking)
            {
                state.RecalculateTickingEffects();
            }
            RaiseRuntimeEffectsChanged(targetCharacter, affectsStats);
        }

        private void RaiseRuntimeEffectsChanged(CharacterObject character, bool affectsStats)
        {
            if (runtimeEffectMutationBatchDepth > 0)
            {
                if (pendingRuntimeEffectCharacterSet.Add(character))
                {
                    pendingRuntimeEffectCharacters.Add(character);
                }

                if (affectsStats)
                {
                    pendingRuntimeEffectStatCharacters.Add(character);
                }

                return;
            }

            DispatchRuntimeEffectsChanged(character, affectsStats);
        }

        public RuntimeEffectMutationBatch BeginRuntimeEffectMutationBatch()
        {
            runtimeEffectMutationBatchDepth++;
            return new RuntimeEffectMutationBatch(this);
        }

        private void EndRuntimeEffectMutationBatch()
        {
            runtimeEffectMutationBatchDepth = Mathf.Max(0, runtimeEffectMutationBatchDepth - 1);
            if (runtimeEffectMutationBatchDepth > 0)
            {
                return;
            }

            while (pendingRuntimeEffectCharacters.Count > 0)
            {
                CharacterObject character = pendingRuntimeEffectCharacters[0];
                pendingRuntimeEffectCharacters.RemoveAt(0);
                pendingRuntimeEffectCharacterSet.Remove(character);
                bool affectsStats = pendingRuntimeEffectStatCharacters.Remove(character);
                DispatchRuntimeEffectsChanged(character, affectsStats);
            }

            pendingRuntimeEffectCharacterSet.Clear();
            pendingRuntimeEffectStatCharacters.Clear();
        }

        private void DispatchRuntimeEffectsChanged(CharacterObject character, bool affectsStats)
        {
            OnRuntimeEffectsChanged?.Invoke(character);
            OnRuntimeEffectsChangedDetailed?.Invoke(character, affectsStats);
            if (affectsStats)
            {
                IsDispatchingRuntimeEffectStatChange = true;
                try
                {
                    CharacterEvents.NotifyStatsChanged(character);
                }
                finally
                {
                    IsDispatchingRuntimeEffectStatChange = false;
                }
            }
        }

        private static RuntimeEffectInstance FindRuntimeEffect(
            List<RuntimeEffectInstance> activeEffects,
            RuntimeEffectDefinition effectDefinition,
            RuntimeEffectRecipe recipe)
        {
            if (activeEffects == null || effectDefinition == null)
            {
                return null;
            }

            for (int i = 0; i < activeEffects.Count; i++)
            {
                RuntimeEffectInstance instance = activeEffects[i];
                if (MatchesRuntimeEffect(instance, effectDefinition, recipe))
                {
                    return instance;
                }
            }

            return null;
        }

        private static RuntimeEffectInstance FindAndCollapseStateChannel(
            List<RuntimeEffectInstance> activeEffects,
            RuntimeEffectStateChannel channel)
        {
            RuntimeEffectInstance retained = null;
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                RuntimeEffectInstance candidate = activeEffects[i];
                if (candidate == null || candidate.StateChannel != channel)
                {
                    continue;
                }

                if (retained == null)
                {
                    retained = candidate;
                    continue;
                }

                retained.remainingShieldAmount =
                    Mathf.Max(0f, retained.remainingShieldAmount)
                    + Mathf.Max(0f, candidate.remainingShieldAmount);
                retained.appliedShieldAmount =
                    Mathf.Max(0f, retained.appliedShieldAmount)
                    + Mathf.Max(0f, candidate.appliedShieldAmount);
                activeEffects.RemoveAt(i);
            }

            return retained;
        }

        private static int CountRuntimeEffects(
            List<RuntimeEffectInstance> activeEffects,
            RuntimeEffectDefinition effectDefinition,
            RuntimeEffectRecipe recipe)
        {
            if (activeEffects == null || effectDefinition == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                RuntimeEffectInstance instance = activeEffects[i];
                if (MatchesRuntimeEffect(instance, effectDefinition, recipe))
                {
                    count++;
                }
            }

            return count;
        }

        private static RuntimeEffectInstance FindOldestRuntimeEffect(
            List<RuntimeEffectInstance> activeEffects,
            RuntimeEffectDefinition effectDefinition,
            RuntimeEffectRecipe recipe)
        {
            if (activeEffects == null || effectDefinition == null)
            {
                return null;
            }

            RuntimeEffectInstance oldest = null;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                RuntimeEffectInstance instance = activeEffects[i];
                if (!MatchesRuntimeEffect(instance, effectDefinition, recipe))
                {
                    continue;
                }

                if (oldest == null || instance.elapsedSeconds > oldest.elapsedSeconds)
                {
                    oldest = instance;
                }
            }

            return oldest;
        }

        private static RuntimeEffectInstance FindMergeableRuntimeEffect(
            List<RuntimeEffectInstance> activeEffects,
            RuntimeEffectDefinition effectDefinition,
            RuntimeEffectRecipe recipe,
            CharacterObject sourceCharacter,
            CharacterStats sourceStats,
            float resolvedDuration)
        {
            if (activeEffects == null || effectDefinition == null)
            {
                return null;
            }

            for (int i = 0; i < activeEffects.Count; i++)
            {
                RuntimeEffectInstance instance = activeEffects[i];
                if (RuntimeEffectRuntimeUtility.CanMergeStack(
                    instance,
                    effectDefinition,
                    recipe,
                    sourceCharacter,
                    sourceStats,
                    resolvedDuration))
                {
                    return instance;
                }
            }

            return null;
        }

        private static bool MatchesRuntimeEffect(
            RuntimeEffectInstance instance,
            RuntimeEffectDefinition effectDefinition,
            RuntimeEffectRecipe recipe)
        {
            return RuntimeEffectRuntimeUtility.MatchesIdentity(instance, effectDefinition, recipe);
        }

        private static bool RuntimeEffectMatchesKey(RuntimeEffectInstance instance, string effectIdOrStackKey)
        {
            if (instance == null || instance.definition == null || string.IsNullOrWhiteSpace(effectIdOrStackKey))
            {
                return false;
            }

            string stableId = RuntimeEffectRuntimeUtility.GetStableId(instance.definition, instance.recipe);
            return string.Equals(stableId, effectIdOrStackKey, StringComparison.Ordinal)
                || string.Equals(instance.StackKey, effectIdOrStackKey, StringComparison.Ordinal)
                || string.Equals(instance.definition.StableId, effectIdOrStackKey, StringComparison.Ordinal);
        }

        private static PlayerCharacter FindActivePlayerFor(CharacterObject character)
        {
            if (character == null)
            {
                return null;
            }

            PlayerCharacter player = PlayerCharacter.Instance;
            return player != null && player.characterObject == character ? player : null;
        }

        private void ApplyPersistentState(SavedPlayerRunInventoryData data)
        {
            isApplyingPersistentState = true;

            ownedItems.Clear();
            itemStacks.Clear();
            runCharacters.Clear();
            artifactInstances.Clear();
            artifactInstancesById = null;
            currency = Mathf.Max(0, data.currency);
            NotifyCurrencyChanged();
            _globalLevel = Mathf.Clamp(data.globalLevel, 1, GlobalMaxLevel);

            BuildItemIndices();

            if (data.artifactInstances != null)
            {
                for (int i = 0; i < data.artifactInstances.Count; i++)
                {
                    SavedArtifactInstanceRecord record = data.artifactInstances[i];
                    if (record == null) continue;
                    TryAddArtifactInstanceInternal(new ArtifactInstance
                    {
                        instanceId = record.instanceId,
                        definitionId = record.definitionId,
                        rolls = record.rolls != null ? new List<ArtifactRoll>(record.rolls) : new List<ArtifactRoll>(),
                        isFavourite = record.isFavourite,
                        isNew = record.isNew,
                        acquisitionOrder = record.acquisitionOrder,
                        collectedTimestampUtc = record.collectedTimestampUtc,
                        generationSeed = record.generationSeed,
                        acquisitionBiomeIndex = record.acquisitionBiomeIndex
                    });
                }
            }

            if (data.items != null)
            {
                for (int i = 0; i < data.items.Count; i++)
                {
                    SavedInventoryItemRecord record = data.items[i];
                    if (record == null || string.IsNullOrWhiteSpace(record.itemId))
                    {
                        continue;
                    }

                    BaseItem item = ResolveItemById(record.itemId);
                    if (item == null)
                    {
                        continue;
                    }

                    AddResolvedItem(item, Mathf.Max(1, record.quantity));
                }
            }

            if (data.runCharacters != null)
            {
                for (int i = 0; i < data.runCharacters.Count; i++)
                {
                    SavedRunCharacterRecord record = data.runCharacters[i];
                    CharacterObject character = ResolveCharacterById(record != null ? record.characterId : null);
                    if (character == null)
                    {
                        continue;
                    }

                    RunCharacterState state = GetOrCreateRunCharacterState(character);
                    if (state == null) continue;
                    state.upgrade = record != null && record.upgrade != null
                        ? record.upgrade.Clone()
                        : CreateRunUpgrade(character);
                    if (record != null)
                    {
                        state.currentHP = record.currentHP;
                        state.currentAE = record.currentAE;
                    }
                }
            }

            EnsureTeamSlotCapacity(data.teamSlots != null ? Mathf.Max(DefaultTeamSlotCount, data.teamSlots.Count) : DefaultTeamSlotCount);
            HashSet<string> restoredArtifactIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < teamSlots.Length; i++)
            {
                SavedRunTeamSlotRecord record = data.teamSlots != null && i < data.teamSlots.Count ? data.teamSlots[i] : null;
                CharacterObject character = ResolveCharacterById(record != null ? record.characterId : null);
                teamSlots[i].Character = character;

                ArtifactInstanceRef[] refs = null;
                if (record != null && record.artifactInstanceIds != null && record.artifactInstanceIds.Length > 0)
                {
                    refs = new ArtifactInstanceRef[record.artifactInstanceIds.Length];
                    for (int artifactIndex = 0; artifactIndex < record.artifactInstanceIds.Length; artifactIndex++)
                    {
                        string instanceId = record.artifactInstanceIds[artifactIndex];
                        EnsureArtifactIndices();
                        if (!string.IsNullOrWhiteSpace(instanceId)
                            && artifactInstancesById.ContainsKey(instanceId)
                            && restoredArtifactIds.Add(instanceId))
                            refs[artifactIndex] = new ArtifactInstanceRef(instanceId);
                    }
                }
                else if (record != null && record.artifactIds != null && record.artifactIds.Length > 0)
                {
                    refs = new ArtifactInstanceRef[record.artifactIds.Length];
                    for (int artifactIndex = 0; artifactIndex < record.artifactIds.Length; artifactIndex++)
                    {
                        ArtifactDefinition definition = ResolveItemById(record.artifactIds[artifactIndex]) as ArtifactDefinition;
                        if (definition == null) continue;
                        ArtifactInstance instance = FindArtifactInstanceByDefinition(definition.id);
                        if (instance == null)
                        {
                            instance = CreateImportedArtifactInstance(definition);
                            TryAddArtifactInstanceInternal(instance);
                        }
                        if (restoredArtifactIds.Add(instance.instanceId))
                            refs[artifactIndex] = new ArtifactInstanceRef(instance.instanceId);
                    }
                }

                teamSlots[i].SetArtifactRefs(refs, DefaultArtifactSlotCount);
                teamSlots[i].ClearLegacyArtifacts();
            }

            activeTeamSlotIndex = data.activeTeamSlotIndex;
            isApplyingPersistentState = false;
            EnsurePlayableState();
        }

        private void PostInventoryMutation(bool save)
        {
            EnsurePlayableState();
            if (save)
            {
                SavePersistentState();
            }
        }

        protected void EnsureRunData()
        {
            EnsureTransientRuntimeStateForPlaySession();
            if (ShouldUsePersistentStorage())
            {
                LoadPersistentStateIfNeeded();
            }

            EnsureInventoryData();

            if (runCharacters == null)
            {
                runCharacters = new List<RunCharacterState>();
            }

            EnsureTeamSlotCapacity(DefaultTeamSlotCount);
            MigrateLegacyArtifactsInMemory();
            EnsurePlayableState();
        }

        private void EnsureTransientRuntimeStateForPlaySession()
        {
            if (!Application.isPlaying || initializedPlaySessionId == currentPlaySessionId)
            {
                return;
            }

            initializedPlaySessionId = currentPlaySessionId;
            if (runCharacters != null)
            {
                for (int i = 0; i < runCharacters.Count; i++)
                {
                    runCharacters[i]?.ClearTransientRuntimeEffects();
                }
            }

            expiredRuntimeEffects.Clear();
            resolvingRuntimeStats.Clear();
            pendingRuntimeEffectCharacters.Clear();
            pendingRuntimeEffectCharacterSet.Clear();
            pendingRuntimeEffectStatCharacters.Clear();
            runtimeEffectMutationBatchDepth = 0;
            IsDispatchingRuntimeEffectStatChange = false;
        }

        private void EnsurePlayableState()
        {
            EnsureInventoryData();

            if (runCharacters == null)
            {
                runCharacters = new List<RunCharacterState>();
            }

            EnsureTeamSlotCapacity(DefaultTeamSlotCount);
            NormalizeRunCharacterStates();
            NormalizeTeamSlots();
            if (!suppressAutoBootstrap && !isDeathTeardown)
            {
                EnsureDefaultCharacterOwnership();
                if (CountTeamCharacters() == 0)
                {
                    FillTeamSlotsFromOwnedCharacters();
                }
            }
            activeTeamSlotIndex = ResolveActiveSlotIndex(activeTeamSlotIndex);
        }

        private bool ShouldPreferAuthoredBootstrapOverSavedData(SavedPlayerRunInventoryData data)
        {
            if (data == null || !HasAuthoredBootstrapData())
            {
                return false;
            }

            int authoredItemCount = CountOwnedItems();
            int authoredRunCharacterCount = GetKnownRunCharacterCount();
            int authoredTeamCharacterCount = CountTeamCharacters();
            int savedItemCount = CountSavedItems(data);
            int savedRunCharacterCount = CountSavedRunCharacters(data);
            int savedTeamCharacterCount = CountSavedTeamCharacters(data);

            bool defaultRegressionSave = data.currency == DefaultCurrency
                && CountSavedItems(data) <= 1
                && CountSavedRunCharacters(data) <= 1
                && CountSavedTeamCharacters(data) <= 1
                && SavedItemsAreEmptyOrDefault(data);

            bool lostAuthoredInventory = authoredItemCount > 1
                && savedItemCount <= 1
                && SavedItemsAreEmptyOrDefault(data);
            bool lostAuthoredRoster = authoredRunCharacterCount > 1
                && savedRunCharacterCount < authoredRunCharacterCount;
            bool lostAuthoredTeam = authoredTeamCharacterCount > 1
                && savedTeamCharacterCount < authoredTeamCharacterCount;

            return defaultRegressionSave || lostAuthoredInventory || lostAuthoredRoster || lostAuthoredTeam;
        }

        private bool HasAuthoredBootstrapData()
        {
            return CountOwnedItems() > 1
                || GetKnownRunCharacterCount() > 1
                || CountTeamCharacters() > 1;
        }

        private void EnsureInventoryData()
        {
            _globalMaxLevel = Mathf.Max(1, _globalMaxLevel);
            _globalLevel = Mathf.Clamp(_globalLevel, 1, _globalMaxLevel);

            if (ownedItems == null)
            {
                ownedItems = new List<BaseItem>();
            }

            defeatedCharactersThisRun ??= new List<CharacterObject>();
            artifactInstances ??= new List<ArtifactInstance>();

            if (itemStacks == null)
            {
                itemStacks = new List<InventoryStack>();
            }

            for (int i = itemStacks.Count - 1; i >= 0; i--)
            {
                InventoryStack stack = itemStacks[i];
                if (stack == null || stack.item == null || !IsStackable(stack.item) || stack.quantity <= 0)
                {
                    itemStacks.RemoveAt(i);
                    continue;
                }

                stack.quantity = Mathf.Min(stack.quantity, GetStackLimit(stack.item));
            }

            MergeDuplicateStackEntries();

            for (int i = 0; i < ownedItems.Count; i++)
            {
                BaseItem item = ownedItems[i];
                if (!IsStackable(item) || FindStack(item) != null)
                {
                    continue;
                }

                int legacyCount = CountLegacyEntries(item);
                itemStacks.Add(new InventoryStack(item, Mathf.Clamp(legacyCount, 1, GetStackLimit(item))));
            }

            NormalizeLegacyStackEntries();
            EnsureArtifactIndices();
        }

        private void EnsureTeamSlotCapacity(int minimumSlots)
        {
            int requiredCount = Mathf.Max(1, minimumSlots);
            if (teamSlots == null || teamSlots.Length < requiredCount)
            {
                RunTeamSlot[] previousSlots = teamSlots;
                teamSlots = new RunTeamSlot[requiredCount];
                if (previousSlots != null)
                {
                    Array.Copy(previousSlots, teamSlots, Mathf.Min(previousSlots.Length, teamSlots.Length));
                }
            }

            for (int i = 0; i < teamSlots.Length; i++)
            {
                teamSlots[i] ??= new RunTeamSlot();
            }
        }

        private void NormalizeRunCharacterStates()
        {
            HashSet<CharacterObject> retainedCharacters = new HashSet<CharacterObject>();
            for (int i = 0; i < ownedItems.Count; i++)
            {
                if (ownedItems[i] is CharacterItem characterItem && characterItem.characterObjectData != null)
                {
                    retainedCharacters.Add(characterItem.characterObjectData);
                }
            }

            if (teamSlots != null)
            {
                for (int i = 0; i < teamSlots.Length; i++)
                {
                    CharacterObject teamCharacter = teamSlots[i] != null ? teamSlots[i].Character : null;
                    if (teamCharacter != null)
                    {
                        retainedCharacters.Add(teamCharacter);
                    }
                }
            }

            for (int i = runCharacters.Count - 1; i >= 0; i--)
            {
                RunCharacterState state = runCharacters[i];
                if (state == null || IsCharacterUnavailable(state.character) || !retainedCharacters.Contains(state.character))
                {
                    runCharacters.RemoveAt(i);
                    continue;
                }

                if (state.upgrade == null)
                {
                    state.upgrade = CreateRunUpgrade(state.character);
                }

                state.EnsureEffectList();
                for (int effectIndex = state.MutableActiveEffects.Count - 1; effectIndex >= 0; effectIndex--)
                {
                    RuntimeEffectInstance effect = state.MutableActiveEffects[effectIndex];
                    if (effect == null || effect.definition == null)
                    {
                        state.MutableActiveEffects.RemoveAt(effectIndex);
                    }
                }
            }

            foreach (CharacterObject character in retainedCharacters)
            {
                GetOrCreateRunCharacterState(character);
            }
        }

        private void NormalizeTeamSlots()
        {
            if (teamSlots == null)
            {
                return;
            }

            HashSet<CharacterObject> assignedCharacters = new HashSet<CharacterObject>();
            for (int i = 0; i < teamSlots.Length; i++)
            {
                CharacterObject character = teamSlots[i].Character;
                if (IsCharacterUnavailable(character)
                    || (FindOwnedCharacterItem(character) == null && !IsLobbyMainCharacter(character))
                    || assignedCharacters.Contains(character))
                {
                    teamSlots[i].Character = null;
                    teamSlots[i].SetArtifacts(null, DefaultArtifactSlotCount);
                    continue;
                }

                assignedCharacters.Add(character);
                if (FindRunCharacterState(character) == null)
                {
                    GetOrCreateRunCharacterState(character);
                }
            }
        }

        private void FillTeamSlotsFromOwnedCharacters()
        {
            List<CharacterObject> ownedCharacters = GetOwnedCharactersInOrder();
            if (ownedCharacters.Count == 0)
            {
                return;
            }

            HashSet<CharacterObject> assignedCharacters = new HashSet<CharacterObject>();
            for (int i = 0; i < teamSlots.Length; i++)
            {
                CharacterObject slotCharacter = teamSlots[i].Character;
                if (slotCharacter != null)
                {
                    assignedCharacters.Add(slotCharacter);
                }
            }

            int ownedIndex = 0;
            for (int slotIndex = 0; slotIndex < teamSlots.Length && ownedIndex < ownedCharacters.Count; slotIndex++)
            {
                if (teamSlots[slotIndex].Character != null)
                {
                    continue;
                }

                while (ownedIndex < ownedCharacters.Count && assignedCharacters.Contains(ownedCharacters[ownedIndex]))
                {
                    ownedIndex++;
                }

                if (ownedIndex >= ownedCharacters.Count)
                {
                    break;
                }

                CharacterObject nextCharacter = ownedCharacters[ownedIndex++];
                teamSlots[slotIndex].Character = nextCharacter;
                teamSlots[slotIndex].SetArtifacts(null, DefaultArtifactSlotCount);
                assignedCharacters.Add(nextCharacter);
                GetOrCreateRunCharacterState(nextCharacter);
            }
        }

        private void EnsureDefaultCharacterOwnership()
        {
            EnsureMainCharacterOwned();
        }

        private void EnsureMainCharacterOwned()
        {
            if (isDeathTeardown) return;
            CharacterItem defaultCharacterItem = Resources.Load<CharacterItem>(DefaultCharacterItemResourcePath);
            if (defaultCharacterItem == null)
            {
                Debug.LogWarning("PlayerRunInventory: Missing default character item at Resources/ItemSO/char_0_Main.");
                return;
            }

            AddResolvedItem(defaultCharacterItem, 1);
        }

        private int ResolveActiveSlotIndex(int preferredIndex)
        {
            if (teamSlots == null || teamSlots.Length == 0)
            {
                return 0;
            }

            if (preferredIndex >= 0
                && preferredIndex < teamSlots.Length
                && teamSlots[preferredIndex].Character != null)
            {
                return preferredIndex;
            }

            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i].Character != null)
                {
                    return i;
                }
            }

            return Mathf.Clamp(preferredIndex, 0, teamSlots.Length - 1);
        }

        private int CountTeamCharacters()
        {
            if (teamSlots == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < teamSlots.Length; i++)
            {
                if (teamSlots[i] != null && teamSlots[i].Character != null)
                {
                    count++;
                }
            }

            return count;
        }

        private ArtifactInstanceRef[][] BuildTeamArtifactSnapshot(int slotCount)
        {
            int resolvedSlotCount = Mathf.Max(DefaultTeamSlotCount, slotCount, teamSlots != null ? teamSlots.Length : 0);
            ArtifactInstanceRef[][] snapshot = new ArtifactInstanceRef[resolvedSlotCount][];
            for (int i = 0; i < resolvedSlotCount; i++)
            {
                snapshot[i] = GetEquippedArtifactRefsAt(i);
            }

            return snapshot;
        }

        private int CountOwnedItems()
        {
            if (ownedItems == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < ownedItems.Count; i++)
            {
                if (ownedItems[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountSavedItems(SavedPlayerRunInventoryData data)
        {
            return data != null && data.items != null ? data.items.Count : 0;
        }

        private static int CountSavedRunCharacters(SavedPlayerRunInventoryData data)
        {
            return data != null && data.runCharacters != null ? data.runCharacters.Count : 0;
        }

        private static int CountSavedTeamCharacters(SavedPlayerRunInventoryData data)
        {
            if (data == null || data.teamSlots == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < data.teamSlots.Count; i++)
            {
                SavedRunTeamSlotRecord slot = data.teamSlots[i];
                if (slot != null && !string.IsNullOrWhiteSpace(slot.characterId))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool SavedItemsAreEmptyOrDefault(SavedPlayerRunInventoryData data)
        {
            if (data == null || data.items == null || data.items.Count == 0)
            {
                return true;
            }

            if (data.items.Count > 1)
            {
                return false;
            }

            SavedInventoryItemRecord item = data.items[0];
            return item == null
                || string.IsNullOrWhiteSpace(item.itemId)
                || string.Equals(item.itemId, DefaultCharacterItemId, StringComparison.Ordinal);
        }

        private int GetOwnedCharacterCount()
        {
            int count = 0;
            for (int i = 0; i < ownedItems.Count; i++)
            {
                if (ownedItems[i] is CharacterItem)
                {
                    count++;
                }
            }

            return count;
        }

        private int GetKnownRunCharacterCount()
        {
            int count = 0;
            for (int i = 0; i < runCharacters.Count; i++)
            {
                if (runCharacters[i] != null && runCharacters[i].character != null)
                {
                    count++;
                }
            }

            return count;
        }

        private List<CharacterObject> GetOwnedCharactersInOrder()
        {
            List<CharacterObject> ownedCharacters = new List<CharacterObject>();
            HashSet<CharacterObject> seenCharacters = new HashSet<CharacterObject>();
            for (int i = 0; i < ownedItems.Count; i++)
            {
                if (ownedItems[i] is not CharacterItem characterItem || characterItem.characterObjectData == null)
                {
                    continue;
                }

                if (seenCharacters.Add(characterItem.characterObjectData))
                {
                    ownedCharacters.Add(characterItem.characterObjectData);
                }
            }

            return ownedCharacters;
        }

        private RunCharacterState FindRunCharacterState(CharacterObject character)
        {
            for (int i = 0; i < runCharacters.Count; i++)
            {
                RunCharacterState state = runCharacters[i];
                if (state != null && state.character == character)
                {
                    if (state.upgrade == null)
                    {
                        state.upgrade = CreateRunUpgrade(character);
                    }

                    return state;
                }
            }

            return null;
        }

        protected RunCharacterState GetOrCreateRunCharacterState(CharacterObject character)
        {
            if (IsCharacterUnavailable(character))
            {
                return null;
            }

            if (runCharacters == null)
            {
                runCharacters = new List<RunCharacterState>();
            }

            RunCharacterState existingState = FindRunCharacterState(character);
            if (existingState != null)
            {
                return existingState;
            }

            RunCharacterState newState = new RunCharacterState
            {
                character = character,
                upgrade = CreateRunUpgrade(character)
            };
            runCharacters.Add(newState);
            return newState;
        }

        private void AddResolvedItem(BaseItem item, int amount)
        {
            if (item == null || amount <= 0)
            {
                return;
            }

            if (item is CharacterItem blockedItem && CannotAcquireCharacter(blockedItem.characterObjectData)) return;

            if (IsStackable(item))
            {
                InventoryStack stack = FindStack(item);
                int clampedAmount = Mathf.Clamp(amount, 1, GetStackLimit(item));
                if (stack == null)
                {
                    itemStacks.Add(new InventoryStack(item, clampedAmount));
                }
                else
                {
                    stack.quantity = Mathf.Clamp(stack.quantity + clampedAmount, 1, GetStackLimit(item));
                }

                EnsureLegacyItemListed(item);
                return;
            }

            if (item is ArtifactDefinition artifactDefinition)
            {
                if (!HasArtifactDefinitionInstance(artifactDefinition.id))
                {
                    TryAddArtifactInstanceInternal(CreateImportedArtifactInstance(artifactDefinition));
                }
                return;
            }

            if (!ownedItems.Contains(item))
            {
                ownedItems.Add(item);
            }

            if (item is CharacterItem characterItem && characterItem.characterObjectData != null)
            {
                GetOrCreateRunCharacterState(characterItem.characterObjectData);
            }
        }

        protected void BuildItemIndices()
        {
            if (itemIndexById != null
                && characterItemsByCharacterId != null
                && artifactDefinitionsById != null)
            {
                return;
            }

            itemIndexById = new Dictionary<string, BaseItem>(StringComparer.Ordinal);
            characterItemsByCharacterId = new Dictionary<string, CharacterItem>(StringComparer.Ordinal);
            artifactDefinitionsById = new Dictionary<string, ArtifactDefinition>(StringComparer.Ordinal);

            BaseItem[] allItems = Resources.LoadAll<BaseItem>("ItemSO");
            for (int i = 0; i < allItems.Length; i++)
            {
                BaseItem item = allItems[i];
                if (item == null || string.IsNullOrWhiteSpace(item.id) || itemIndexById.ContainsKey(item.id))
                {
                    continue;
                }

                itemIndexById.Add(item.id, item);

                if (item is ArtifactDefinition artifactDefinition)
                {
                    artifactDefinitionsById[item.id] = artifactDefinition;
                }

                if (item is CharacterItem characterItem
                    && characterItem.characterObjectData != null
                    && !string.IsNullOrWhiteSpace(characterItem.characterObjectData.characterId)
                    && !characterItemsByCharacterId.ContainsKey(characterItem.characterObjectData.characterId))
                {
                    characterItemsByCharacterId.Add(characterItem.characterObjectData.characterId, characterItem);
                }
            }
        }

        private void EnsureArtifactDefinitionIndex()
        {
            if (artifactDefinitionsById != null) return;
            BuildItemIndices();

            artifactDefinitionsById ??= new Dictionary<string, ArtifactDefinition>(StringComparer.Ordinal);
        }

        private void EnsureArtifactIndices()
        {
            artifactInstances ??= new List<ArtifactInstance>();
            if (artifactInstancesById == null)
            {
                RebuildArtifactInstanceIndex();
            }
        }

        private void RebuildArtifactInstanceIndex()
        {
            artifactInstancesById = new Dictionary<string, ArtifactInstance>(StringComparer.Ordinal);
            long highestOrder = 0;
            for (int i = artifactInstances.Count - 1; i >= 0; i--)
            {
                ArtifactInstance instance = artifactInstances[i];
                if (instance == null || string.IsNullOrWhiteSpace(instance.instanceId))
                {
                    artifactInstances.RemoveAt(i);
                    continue;
                }

                instance.rolls ??= new List<ArtifactRoll>();
                if (artifactInstancesById.ContainsKey(instance.instanceId))
                {
                    artifactInstances.RemoveAt(i);
                    continue;
                }

                artifactInstancesById.Add(instance.instanceId, instance);
                if (instance.acquisitionOrder <= 0)
                {
                    instance.acquisitionOrder = ++highestOrder;
                }
                else
                {
                    highestOrder = Math.Max(highestOrder, instance.acquisitionOrder);
                }
            }
            nextArtifactAcquisitionOrder = Math.Max(1, highestOrder + 1);
        }

        private bool TryAddArtifactInstanceInternal(ArtifactInstance instance)
        {
            if (instance == null
                || string.IsNullOrWhiteSpace(instance.instanceId)
                || string.IsNullOrWhiteSpace(instance.definitionId))
            {
                return false;
            }

            EnsureArtifactIndices();
            if (artifactInstancesById.ContainsKey(instance.instanceId)) return false;
            instance.rolls ??= new List<ArtifactRoll>();
            artifactInstances.Add(instance);
            artifactInstancesById.Add(instance.instanceId, instance);
            nextArtifactAcquisitionOrder = Math.Max(nextArtifactAcquisitionOrder, instance.acquisitionOrder + 1);
            return true;
        }

        private bool HasArtifactDefinitionInstance(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) return false;
            EnsureArtifactIndices();
            for (int i = 0; i < artifactInstances.Count; i++)
                if (string.Equals(artifactInstances[i]?.definitionId, definitionId, StringComparison.Ordinal)) return true;
            return false;
        }

        private ArtifactInstance FindArtifactInstanceByDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) return null;
            EnsureArtifactIndices();
            for (int i = 0; i < artifactInstances.Count; i++)
                if (string.Equals(artifactInstances[i]?.definitionId, definitionId, StringComparison.Ordinal)) return artifactInstances[i];
            return null;
        }

        private ArtifactInstance CreateImportedArtifactInstance(ArtifactDefinition definition)
        {
            return ArtifactLegacyImporter.Create(definition, nextArtifactAcquisitionOrder++);
        }

        private void MigrateLegacyArtifactsInMemory()
        {
            EnsureArtifactIndices();
            int importedInstances = 0;
            int migratedLoadoutReferences = 0;
            for (int i = ownedItems.Count - 1; i >= 0; i--)
            {
                if (ownedItems[i] is not ArtifactDefinition definition) continue;
                if (!HasArtifactDefinitionInstance(definition.id))
                {
                    if (TryAddArtifactInstanceInternal(CreateImportedArtifactInstance(definition))) importedInstances++;
                }
                ownedItems.RemoveAt(i);
            }

            if (teamSlots == null) return;
            for (int slotIndex = 0; slotIndex < teamSlots.Length; slotIndex++)
            {
                RunTeamSlot slot = teamSlots[slotIndex];
                if (slot == null) continue;
                Artifact[] legacy = slot.GetArtifactsSnapshot(DefaultArtifactSlotCount);
                ArtifactInstanceRef[] refs = slot.GetArtifactRefsSnapshot(DefaultArtifactSlotCount);
                bool changed = false;
                for (int i = 0; i < legacy.Length && i < refs.Length; i++)
                {
                    ArtifactDefinition definition = legacy[i];
                    if (definition == null || !refs[i].IsEmpty) continue;
                    ArtifactInstance instance = FindArtifactInstanceByDefinition(definition.id);
                    if (instance == null)
                    {
                        instance = CreateImportedArtifactInstance(definition);
                        if (TryAddArtifactInstanceInternal(instance)) importedInstances++;
                    }
                    refs[i] = new ArtifactInstanceRef(instance.instanceId);
                    migratedLoadoutReferences++;
                    changed = true;
                }
                if (changed) slot.SetArtifactRefs(refs, DefaultArtifactSlotCount);
                slot.ClearLegacyArtifacts();
            }

            if (importedInstances > 0 || migratedLoadoutReferences > 0)
            {
                Debug.Log($"PlayerRunInventory: Imported {importedInstances} legacy Artifact instance(s) and migrated {migratedLoadoutReferences} loadout reference(s) without rerolling values.");
            }
        }

        private BaseItem ResolveItemById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            BuildItemIndices();
            itemIndexById.TryGetValue(itemId, out BaseItem item);
            return item;
        }

        private CharacterObject ResolveCharacterById(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            BuildItemIndices();
            if (characterItemsByCharacterId.TryGetValue(characterId, out CharacterItem characterItem))
            {
                return characterItem != null ? characterItem.characterObjectData : null;
            }

            return null;
        }

        private void MergeDuplicateStackEntries()
        {
            for (int i = itemStacks.Count - 1; i >= 0; i--)
            {
                InventoryStack stack = itemStacks[i];
                if (stack == null || stack.item == null)
                {
                    continue;
                }

                for (int existingIndex = 0; existingIndex < i; existingIndex++)
                {
                    InventoryStack existingStack = itemStacks[existingIndex];
                    if (existingStack == null || existingStack.item != stack.item)
                    {
                        continue;
                    }

                    existingStack.quantity = Mathf.Min(
                        existingStack.quantity + stack.quantity,
                        GetStackLimit(existingStack.item));
                    itemStacks.RemoveAt(i);
                    break;
                }
            }
        }

        private InventoryStack FindStack(BaseItem item)
        {
            for (int i = 0; i < itemStacks.Count; i++)
            {
                InventoryStack stack = itemStacks[i];
                if (stack != null && stack.item == item)
                {
                    return stack;
                }
            }

            return null;
        }

        private void NormalizeLegacyStackEntries()
        {
            HashSet<BaseItem> seenStackItems = new HashSet<BaseItem>();
            for (int i = ownedItems.Count - 1; i >= 0; i--)
            {
                BaseItem item = ownedItems[i];
                if (!IsStackable(item))
                {
                    continue;
                }

                InventoryStack stack = FindStack(item);
                if (stack == null || stack.quantity <= 0 || seenStackItems.Contains(item))
                {
                    ownedItems.RemoveAt(i);
                    continue;
                }

                seenStackItems.Add(item);
            }

            for (int i = 0; i < itemStacks.Count; i++)
            {
                InventoryStack stack = itemStacks[i];
                if (stack != null && stack.item != null && stack.quantity > 0 && !ownedItems.Contains(stack.item))
                {
                    ownedItems.Add(stack.item);
                }
            }
        }

        private void EnsureLegacyItemListed(BaseItem item)
        {
            if (!ownedItems.Contains(item))
            {
                ownedItems.Add(item);
            }
        }

        private void RemoveAllLegacyEntries(BaseItem item)
        {
            for (int i = ownedItems.Count - 1; i >= 0; i--)
            {
                if (ownedItems[i] == item)
                {
                    ownedItems.RemoveAt(i);
                }
            }
        }

        private int CountLegacyEntries(BaseItem item)
        {
            int count = 0;
            for (int i = 0; i < ownedItems.Count; i++)
            {
                if (ownedItems[i] == item)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsStackable(BaseItem item)
        {
            return item is Item;
        }

        private static int GetStackLimit(BaseItem item)
        {
            if (item is Item stackableItem)
            {
                return Mathf.Max(1, stackableItem.maxStackSize);
            }

            return 1;
        }

        private static CharacterUpgrade CreateRunUpgrade(CharacterObject character)
        {
            // HP/AE/effect records still use RunCharacterState; preserve its
            // legacy upgrade payload until serialized save migration is removed.
            CharacterUpgrade source = character != null ? character.upgrade : null;
            CharacterUpgrade upgrade = source != null ? source.Clone() : new CharacterUpgrade();
            upgrade.level = 1;
            upgrade.currentExp = 0;
            return upgrade;
        }

        protected virtual bool PersistenceEnabled => false;
        protected virtual bool IsPersistentAsset => false;
        protected virtual CharacterProgressionState ResolvePersistenceProgression() => null;

        protected void NotifyGlobalLevelChanged(int before) => OnGlobalLevelChanged?.Invoke(before, GlobalLevel);

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
