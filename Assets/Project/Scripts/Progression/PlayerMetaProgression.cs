using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using UnityEngine;

namespace Project.Progression
{
    public enum ProgressionFragmentType
    {
        CharacterPotential,
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

    [CreateAssetMenu(fileName = "New Player Meta Progression", menuName = "Progression/Player Meta Progression", order = 20)]
    public class PlayerMetaProgression : ScriptableObject
    {
        [Header("Unused Upgrade Materials")]
        [SerializeField] private List<ProgressionFragmentStack> fragments = new List<ProgressionFragmentStack>();

        public IReadOnlyList<ProgressionFragmentStack> Fragments => fragments;

        public int GetFragmentCount(BaseItem sourceItem)
        {
            string sourceId = GetSourceId(sourceItem);
            if (string.IsNullOrEmpty(sourceId))
            {
                return 0;
            }

            ProgressionFragmentStack stack = FindFragmentStack(sourceId, GetFragmentType(sourceItem));
            return stack != null ? stack.quantity : 0;
        }

        public void AddFragments(BaseItem sourceItem, int amount)
        {
            if (sourceItem == null || amount <= 0)
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
        }

        public bool TrySpendFragments(BaseItem sourceItem, int amount)
        {
            if (sourceItem == null || amount <= 0)
            {
                return false;
            }

            ProgressionFragmentStack stack = FindFragmentStack(GetSourceId(sourceItem), GetFragmentType(sourceItem));
            if (stack == null || stack.quantity < amount)
            {
                return false;
            }

            stack.quantity -= amount;
            return true;
        }

        public bool TryRemoveFragments(BaseItem sourceItem, int amount)
        {
            return TrySpendFragments(sourceItem, amount);
        }

        public void ClearFragments()
        {
            fragments.Clear();
        }

        private ProgressionFragmentStack FindFragmentStack(string sourceId, ProgressionFragmentType fragmentType)
        {
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

        private static ProgressionFragmentType GetFragmentType(BaseItem sourceItem)
        {
            return sourceItem is Artifact
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
    }
}
