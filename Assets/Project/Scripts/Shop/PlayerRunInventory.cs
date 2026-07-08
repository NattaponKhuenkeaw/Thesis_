using System;
using System.Collections.Generic;
using UnityEngine;
using Project.InventorySystem.Items;

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
        public CharacterUpgrade upgrade = new CharacterUpgrade();
        public float currentHP = -1f;
        public float currentAE = -1f;
        [SerializeField] private List<RuntimeEffectInstance> activeEffects = new List<RuntimeEffectInstance>();
        [SerializeField] private bool hasTickingEffects;

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
        private const int DefaultArtifactSlotCount = 3;

        [SerializeField] private CharacterObject character;
        [SerializeField] private Artifact[] artifacts = new Artifact[DefaultArtifactSlotCount];

        public CharacterObject Character
        {
            get => character;
            set => character = value;
        }

        public Artifact[] GetArtifactsSnapshot(int minimumSlots = DefaultArtifactSlotCount)
        {
            EnsureArtifactCapacity(minimumSlots);
            Artifact[] snapshot = new Artifact[artifacts.Length];
            Array.Copy(artifacts, snapshot, artifacts.Length);
            return snapshot;
        }

        public void SetArtifacts(Artifact[] source, int minimumSlots = DefaultArtifactSlotCount)
        {
            int artifactCount = Mathf.Max(minimumSlots, source != null ? source.Length : 0);
            artifacts = new Artifact[artifactCount];
            if (source != null)
            {
                Array.Copy(source, artifacts, Mathf.Min(source.Length, artifacts.Length));
            }
        }

        private void EnsureArtifactCapacity(int minimumSlots)
        {
            int requiredCount = Mathf.Max(1, minimumSlots);
            if (artifacts != null && artifacts.Length >= requiredCount)
            {
                return;
            }

            Artifact[] previousArtifacts = artifacts;
            artifacts = new Artifact[requiredCount];
            if (previousArtifacts != null)
            {
                Array.Copy(previousArtifacts, artifacts, Mathf.Min(previousArtifacts.Length, artifacts.Length));
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
    }

    [Serializable]
    internal class SavedPlayerRunInventoryData
    {
        public int currency = 1000;
        public int activeTeamSlotIndex;
        public List<SavedInventoryItemRecord> items = new List<SavedInventoryItemRecord>();
        public List<SavedRunCharacterRecord> runCharacters = new List<SavedRunCharacterRecord>();
        public List<SavedRunTeamSlotRecord> teamSlots = new List<SavedRunTeamSlotRecord>();
    }

    [CreateAssetMenu(fileName = "New Player Run Inventory", menuName = "Inventory/Player Run Inventory", order = 12)]
    public class PlayerRunInventory : ScriptableObject
    {
        private const int DefaultTeamSlotCount = 3;
        private const int DefaultArtifactSlotCount = 3;
        private const int DefaultCurrency = 1000;
        private const string SaveKey = "project.player_run_inventory.v2";
        private const string DefaultCharacterItemId = "char-0-main";
        private const string DefaultCharacterItemResourcePath = "ItemSO/char_0_Main";
        private static readonly int RuntimeEffectStatTargetCount = Enum.GetValues(typeof(RuntimeEffectStatTarget)).Length;

        [Header("Player Owned Items")]
        [Tooltip("Runtime-owned items for the current run. Character items represent roster ownership.")]
        public List<BaseItem> ownedItems = new List<BaseItem>();

        [Header("Stacked Items")]
        [Tooltip("Runtime stack quantities for stackable consumables.")]
        [SerializeField] private List<InventoryStack> itemStacks = new List<InventoryStack>();

        [Header("Currency")]
        public int currency = DefaultCurrency;

        [Header("Run Character State")]
        [Tooltip("Run-scoped character level and EXP. Cleared with the current run inventory.")]
        [SerializeField] private List<RunCharacterState> runCharacters = new List<RunCharacterState>();

        [Header("Deployed Team")]
        [Tooltip("Run-owned team slots. Slots resolve to RunCharacterState; CharacterObject is identity only.")]
        [SerializeField] private RunTeamSlot[] teamSlots = new RunTeamSlot[DefaultTeamSlotCount];
        [SerializeField] private int activeTeamSlotIndex;

        [NonSerialized] private bool hasLoadedPersistentState;
        [NonSerialized] private bool isApplyingPersistentState;
        [NonSerialized] private Dictionary<string, BaseItem> itemIndexById;
        [NonSerialized] private float[] runtimeEffectMultiplierSums;
        [NonSerialized] private Dictionary<string, CharacterItem> characterItemsByCharacterId;

        public event Action<CharacterObject> OnRuntimeEffectsChanged;

        public IReadOnlyList<InventoryStack> ItemStacks
        {
            get
            {
                EnsureRunData();
                return itemStacks;
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

        private void OnEnable()
        {
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

        public bool TryAddItem(BaseItem item, int amount = 1)
        {
            if (item == null || amount <= 0)
            {
                return false;
            }

            EnsureRunData();

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
            ownedItems.Clear();
            itemStacks.Clear();
            runCharacters.Clear();
            EnsureTeamSlotCapacity(DefaultTeamSlotCount);
            for (int i = 0; i < teamSlots.Length; i++)
            {
                teamSlots[i].Character = null;
                teamSlots[i].SetArtifacts(null, DefaultArtifactSlotCount);
            }

            activeTeamSlotIndex = 0;
            PostInventoryMutation(save: true);
        }

        public void ResetRunProgress()
        {
            ClearInventory();
        }

        public bool CanAfford(int amount)
        {
            return currency >= amount;
        }

        public void SpendCurrency(int amount)
        {
            currency = Mathf.Max(0, currency - Mathf.Max(0, amount));
            SavePersistentState();
        }

        public void AddCurrency(int amount)
        {
            currency += Mathf.Max(0, amount);
            SavePersistentState();
        }

        public bool HasDeployedTeam()
        {
            EnsureRunData();
            return CountTeamCharacters() > 0;
        }

        public void DeployTeam(CharacterObject[] characters, int preferredIndex, Artifact[][] artifactsBySlot = null)
        {
            EnsureRunData();

            int requestedSlotCount = Mathf.Max(
                DefaultTeamSlotCount,
                characters != null ? characters.Length : 0,
                artifactsBySlot != null ? artifactsBySlot.Length : 0);
            EnsureTeamSlotCapacity(requestedSlotCount);

            for (int i = 0; i < teamSlots.Length; i++)
            {
                CharacterObject character = characters != null && i < characters.Length ? characters[i] : null;
                teamSlots[i].Character = character;
                if (character != null)
                {
                    GetOrCreateRunCharacterState(character);
                }

                Artifact[] sourceArtifacts = artifactsBySlot != null && i < artifactsBySlot.Length
                    ? artifactsBySlot[i]
                    : null;
                teamSlots[i].SetArtifacts(sourceArtifacts, DefaultArtifactSlotCount);
            }

            activeTeamSlotIndex = ResolveActiveSlotIndex(preferredIndex);
            PostInventoryMutation(save: true);
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
            EnsureRunData();
            if (slotIndex < 0 || slotIndex >= teamSlots.Length)
            {
                return Array.Empty<Artifact>();
            }

            return teamSlots[slotIndex].GetArtifactsSnapshot(DefaultArtifactSlotCount);
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
            for (int i = 0; i < teamSlots.Length; i++)
            {
                Artifact[] equippedArtifacts = teamSlots[i].GetArtifactsSnapshot(DefaultArtifactSlotCount);
                for (int artifactIndex = 0; artifactIndex < equippedArtifacts.Length; artifactIndex++)
                {
                    if (equippedArtifacts[artifactIndex] == artifact)
                    {
                        return true;
                    }
                }
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

            return -1;
        }

        public CharacterUpgrade GetCharacterUpgrade(CharacterObject character)
        {
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

        public CharacterStats GetCharacterStats(CharacterObject character)
        {
            CharacterStats stats = GetCharacterBaseStats(character);
            int slotIndex = FindTeamSlot(character);
            if (slotIndex >= 0)
            {
                Artifact[] artifacts = GetEquippedArtifactsAt(slotIndex);
                float hpPercentBonus = 0f;
                float atkPercentBonus = 0f;
                float defPercentBonus = 0f;

                for (int i = 0; i < artifacts.Length; i++)
                {
                    Artifact artifact = artifacts[i];
                    if (artifact == null || artifact.bonusStats == null)
                    {
                        continue;
                    }

                    hpPercentBonus += artifact.bonusStats.maxHP;
                    atkPercentBonus += artifact.bonusStats.attack;
                    defPercentBonus += artifact.bonusStats.defense;

                    stats.speed += artifact.bonusStats.speed;
                    stats.elementalMastery += artifact.bonusStats.elementalMastery;
                    stats.critRate += artifact.bonusStats.critRate;
                    stats.critDamage += artifact.bonusStats.critDamage;
                    stats.cooldownRate += artifact.bonusStats.cooldownRate;
                    stats.impact += artifact.bonusStats.impact;
                    stats.damageReduction += artifact.bonusStats.damageReduction;
                    stats.aeRecharge += artifact.bonusStats.aeRecharge;
                }

                stats.maxHP = stats.maxHP * (1f + hpPercentBonus / 100f);
                stats.attack = stats.attack * (1f + atkPercentBonus / 100f);
                stats.defense = stats.defense * (1f + defPercentBonus / 100f);
            }
            ApplyRuntimeEffectsToStats(character, stats);
            return stats;
        }

        public CharacterStats GetCharacterBaseStats(CharacterObject character)
        {
            if (character == null)
            {
                return new CharacterStats();
            }

            CharacterUpgrade runUpgrade = GetCharacterUpgrade(character);
            CharacterStats leveled = runUpgrade != null
                ? runUpgrade.ApplyLevelGrowth(character.stats)
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

        public bool ApplyRuntimeEffect(
            CharacterObject targetCharacter,
            RuntimeEffectDefinition effectDefinition,
            CharacterObject sourceCharacter = null,
            CharacterStats sourceStats = null,
            float durationOverride = -1f,
            RuntimeEffectStackMode stackMode = RuntimeEffectStackMode.UseDefinition,
            int stackLimit = 1,
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
                refreshDurationOnReapply);
        }

        public bool ApplyRuntimeEffect(
            CharacterObject targetCharacter,
            RuntimeEffectRecipe recipe,
            CharacterObject sourceCharacter = null,
            CharacterStats sourceStats = null,
            float durationOverride = -1f)
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
                RuntimeEffectRuntimeUtility.GetRefreshDurationOnReapply(recipe, true));
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
            bool refreshDurationOnReapply)
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
            int resolvedStackLimit = RuntimeEffectRuntimeUtility.GetStackLimit(recipe, stackLimit);
            bool resolvedRefreshDuration = RuntimeEffectRuntimeUtility.GetRefreshDurationOnReapply(recipe, refreshDurationOnReapply);
            bool effectRequiresTicking = RuntimeEffectRuntimeUtility.RequiresTicking(effectDefinition, recipe);
            bool effectAffectsStats = RuntimeEffectRuntimeUtility.AffectsStats(effectDefinition, recipe);
            float resolvedDuration = RuntimeEffectRuntimeUtility.ResolveDuration(effectDefinition, recipe, durationOverride);
            bool shouldStack = ResolveShouldStack(effectDefinition, recipe, stackMode);
            RuntimeEffectInstance instance = !shouldStack
                ? FindRuntimeEffect(activeEffects, effectDefinition, recipe)
                : null;

            CharacterStats resolvedSourceStats = sourceStats != null
                ? sourceStats.Clone()
                : (sourceCharacter != null ? GetCharacterStats(sourceCharacter) : GetCharacterBaseStats(targetCharacter));

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
                    if (stackTarget.stackCount < resolvedStackLimit)
                    {
                        stackTarget.AddStack(durationOverride, resolvedRefreshDuration);
                    }
                    else if (resolvedRefreshDuration)
                    {
                        stackTarget.Refresh(durationOverride);
                    }

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
                else if (resolvedRefreshDuration)
                {
                    instance.Refresh(durationOverride);
                }

                if (effectRequiresTicking)
                {
                    state.HasTickingEffects = true;
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

            if (RuntimeEffectRuntimeUtility.GetDurationType(effectDefinition, recipe) == RuntimeEffectDurationType.Instant)
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

            string expectedStackKey = RuntimeEffectRuntimeUtility.GetStackKey(effectDefinition, recipe);
            bool changed = false;
            for (int i = state.MutableActiveEffects.Count - 1; i >= 0; i--)
            {
                RuntimeEffectInstance instance = state.MutableActiveEffects[i];
                if (instance == null || instance.definition == null)
                {
                    continue;
                }

                if (!string.Equals(instance.definition.StableId, effectDefinition.StableId, StringComparison.Ordinal)
                    || !string.Equals(instance.StackKey, expectedStackKey, StringComparison.Ordinal))
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
                for (int effectIndex = activeEffects.Count - 1; effectIndex >= 0; effectIndex--)
                {
                    RuntimeEffectInstance instance = activeEffects[effectIndex];
                    if (instance == null || instance.definition == null)
                    {
                        activeEffects.RemoveAt(effectIndex);
                        RaiseRuntimeEffectsChanged(state.character, true);
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
                    targetStatsSnapshot ??= GetCharacterStats(state.character);
                    instance.definition.OnUpdate(BuildRuntimeEffectContext(
                        state.character,
                        state,
                        instance,
                        resolvedDelta,
                        targetStatsSnapshot));
                    if (!instance.IsExpired)
                    {
                        continue;
                    }

                    RemoveRuntimeEffect(state.character, state, instance);
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

            float[] multiplierSums = GetRuntimeEffectMultiplierSums();

            List<RuntimeEffectInstance> activeEffects = state.MutableActiveEffects;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                RuntimeEffectInstance instance = activeEffects[i];
                if (instance == null || instance.definition == null)
                {
                    continue;
                }

                RuntimeEffectContext context = BuildRuntimeEffectContext(character, state, instance, 0f, stats);
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

        private float[] GetRuntimeEffectMultiplierSums()
        {
            if (runtimeEffectMultiplierSums == null || runtimeEffectMultiplierSums.Length != RuntimeEffectStatTargetCount)
            {
                runtimeEffectMultiplierSums = new float[RuntimeEffectStatTargetCount];
            }
            else
            {
                Array.Clear(runtimeEffectMultiplierSums, 0, runtimeEffectMultiplierSums.Length);
            }

            return runtimeEffectMultiplierSums;
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

        public void SavePersistentState()
        {
            if (!ShouldUsePersistentStorage() || isApplyingPersistentState)
            {
                return;
            }

            EnsureRunData();

            SavedPlayerRunInventoryData data = new SavedPlayerRunInventoryData
            {
                currency = currency,
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

            for (int i = 0; i < teamSlots.Length; i++)
            {
                RunTeamSlot slot = teamSlots[i];
                SavedRunTeamSlotRecord savedSlot = new SavedRunTeamSlotRecord
                {
                    characterId = slot != null && slot.Character != null ? slot.Character.characterId : string.Empty
                };

                Artifact[] artifacts = slot != null ? slot.GetArtifactsSnapshot(DefaultArtifactSlotCount) : Array.Empty<Artifact>();
                savedSlot.artifactIds = new string[artifacts.Length];
                for (int artifactIndex = 0; artifactIndex < artifacts.Length; artifactIndex++)
                {
                    savedSlot.artifactIds[artifactIndex] = artifacts[artifactIndex] != null ? artifacts[artifactIndex].id : string.Empty;
                }

                data.teamSlots.Add(savedSlot);
            }

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void LoadPersistentStateIfNeeded()
        {
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

            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
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
            SavePersistentState();
        }

        private void ClearAllInventoryData()
        {
            if (ownedItems != null) ownedItems.Clear();
            if (itemStacks != null) itemStacks.Clear();
            if (runCharacters != null) runCharacters.Clear();
            if (teamSlots != null)
            {
                for (int i = 0; i < teamSlots.Length; i++)
                {
                    if (teamSlots[i] != null)
                    {
                        teamSlots[i].Character = null;
                        teamSlots[i].SetArtifacts(null, DefaultArtifactSlotCount);
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
            CharacterStats targetStatsOverride = null)
        {
            PlayerCharacter activePlayer = FindActivePlayerFor(targetCharacter);
            return new RuntimeEffectContext
            {
                runInventory = this,
                instance = instance,
                sourceCharacter = instance != null ? instance.sourceCharacter : null,
                targetCharacter = targetCharacter,
                sourceStats = instance != null ? instance.sourceStatsSnapshot : null,
                targetStats = targetStatsOverride ?? (targetCharacter != null ? GetCharacterStats(targetCharacter) : null),
                targetPlayer = activePlayer,
                deltaTime = deltaTime
            };
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

            float result = currentDamage;
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
                result = outgoing
                    ? instance.definition.ModifyOutgoingDamage(context, result)
                    : instance.definition.ModifyIncomingDamage(context, result);
                RuntimeEffectRuntimeUtility.RestoreDamageContext(
                    context,
                    previousInstance,
                    previousSourceCharacter,
                    previousSourceStats);
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
            OnRuntimeEffectsChanged?.Invoke(character);
            if (affectsStats)
            {
                CharacterEvents.NotifyStatsChanged(character);
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
                if (!MatchesRuntimeEffect(instance, effectDefinition, recipe))
                {
                    continue;
                }

                if (instance.sourceCharacter != sourceCharacter)
                {
                    continue;
                }

                if (!StatsApproximatelyEqual(instance.sourceStatsSnapshot, sourceStats))
                {
                    continue;
                }

                if (!Mathf.Approximately(instance.appliedDurationSeconds, resolvedDuration))
                {
                    continue;
                }

                return instance;
            }

            return null;
        }

        private static bool StatsApproximatelyEqual(CharacterStats a, CharacterStats b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return a == null && b == null;
            }

            return Approximately(a.maxHP, b.maxHP)
                && Approximately(a.attack, b.attack)
                && Approximately(a.defense, b.defense)
                && Approximately(a.speed, b.speed)
                && Approximately(a.elementalMastery, b.elementalMastery)
                && Approximately(a.critRate, b.critRate)
                && Approximately(a.critDamage, b.critDamage)
                && Approximately(a.cooldownRate, b.cooldownRate)
                && Approximately(a.impact, b.impact)
                && Approximately(a.damageReduction, b.damageReduction)
                && Approximately(a.aeRecharge, b.aeRecharge);
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }

        private static bool MatchesRuntimeEffect(
            RuntimeEffectInstance instance,
            RuntimeEffectDefinition effectDefinition,
            RuntimeEffectRecipe recipe)
        {
            if (instance == null || instance.definition == null || effectDefinition == null)
            {
                return false;
            }

            return string.Equals(instance.definition.StableId, effectDefinition.StableId, StringComparison.Ordinal)
                && string.Equals(instance.StackKey, RuntimeEffectRuntimeUtility.GetStackKey(effectDefinition, recipe), StringComparison.Ordinal);
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
            currency = Mathf.Max(0, data.currency);

            BuildItemIndices();

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
            for (int i = 0; i < teamSlots.Length; i++)
            {
                SavedRunTeamSlotRecord record = data.teamSlots != null && i < data.teamSlots.Count ? data.teamSlots[i] : null;
                CharacterObject character = ResolveCharacterById(record != null ? record.characterId : null);
                teamSlots[i].Character = character;

                Artifact[] artifacts = null;
                if (record != null && record.artifactIds != null && record.artifactIds.Length > 0)
                {
                    artifacts = new Artifact[record.artifactIds.Length];
                    for (int artifactIndex = 0; artifactIndex < record.artifactIds.Length; artifactIndex++)
                    {
                        artifacts[artifactIndex] = ResolveItemById(record.artifactIds[artifactIndex]) as Artifact;
                    }
                }

                teamSlots[i].SetArtifacts(artifacts, DefaultArtifactSlotCount);
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

        private void EnsureRunData()
        {
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
            EnsurePlayableState();
        }

        private void EnsurePlayableState()
        {
            EnsureInventoryData();

            if (runCharacters == null)
            {
                runCharacters = new List<RunCharacterState>();
            }

            EnsureTeamSlotCapacity(DefaultTeamSlotCount);
            EnsureDefaultCharacterOwnership();
            NormalizeRunCharacterStates();
            NormalizeTeamSlots();
            if (CountTeamCharacters() == 0)
            {
                FillTeamSlotsFromOwnedCharacters();
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
            if (ownedItems == null)
            {
                ownedItems = new List<BaseItem>();
            }

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
                if (state == null || state.character == null || !retainedCharacters.Contains(state.character))
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

            HashSet<CharacterObject> ownedCharacters = new HashSet<CharacterObject>(GetOwnedCharactersInOrder());
            HashSet<CharacterObject> assignedCharacters = new HashSet<CharacterObject>();
            for (int i = 0; i < teamSlots.Length; i++)
            {
                CharacterObject character = teamSlots[i].Character;
                bool isKnownRunCharacter = character != null && FindRunCharacterState(character) != null;
                if (character == null || (!ownedCharacters.Contains(character) && !isKnownRunCharacter) || assignedCharacters.Contains(character))
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
            if (GetOwnedCharacterCount() > 0 || GetKnownRunCharacterCount() > 0 || CountTeamCharacters() > 0)
            {
                return;
            }

            CharacterItem defaultCharacterItem = Resources.Load<CharacterItem>(DefaultCharacterItemResourcePath);
            if (defaultCharacterItem == null)
            {
                Debug.LogWarning("PlayerRunInventory: Missing default character item at Resources/ItemSO/char_0_Main.");
                return;
            }

            if (!ownedItems.Contains(defaultCharacterItem))
            {
                ownedItems.Add(defaultCharacterItem);
            }

            if (defaultCharacterItem.characterObjectData != null)
            {
                GetOrCreateRunCharacterState(defaultCharacterItem.characterObjectData);
            }
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

        private RunCharacterState GetOrCreateRunCharacterState(CharacterObject character)
        {
            if (character == null)
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

            if (!ownedItems.Contains(item))
            {
                ownedItems.Add(item);
            }

            if (item is CharacterItem characterItem && characterItem.characterObjectData != null)
            {
                GetOrCreateRunCharacterState(characterItem.characterObjectData);
            }
        }

        private void BuildItemIndices()
        {
            if (itemIndexById != null && characterItemsByCharacterId != null)
            {
                return;
            }

            itemIndexById = new Dictionary<string, BaseItem>(StringComparer.Ordinal);
            characterItemsByCharacterId = new Dictionary<string, CharacterItem>(StringComparer.Ordinal);

            BaseItem[] allItems = Resources.LoadAll<BaseItem>("ItemSO");
            for (int i = 0; i < allItems.Length; i++)
            {
                BaseItem item = allItems[i];
                if (item == null || string.IsNullOrWhiteSpace(item.id) || itemIndexById.ContainsKey(item.id))
                {
                    continue;
                }

                itemIndexById.Add(item.id, item);

                if (item is CharacterItem characterItem
                    && characterItem.characterObjectData != null
                    && !string.IsNullOrWhiteSpace(characterItem.characterObjectData.characterId)
                    && !characterItemsByCharacterId.ContainsKey(characterItem.characterObjectData.characterId))
                {
                    characterItemsByCharacterId.Add(characterItem.characterObjectData.characterId, characterItem);
                }
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
            CharacterUpgrade source = character != null ? character.upgrade : null;
            CharacterUpgrade upgrade = source != null ? source.Clone() : new CharacterUpgrade();
            upgrade.level = 1;
            upgrade.currentExp = 0;
            return upgrade;
        }

        private bool ShouldUsePersistentStorage()
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            PlayerRunInventory persistentAsset = Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory");
            return persistentAsset == this;
        }
    }
}
