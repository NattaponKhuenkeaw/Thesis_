using System;
using System.Collections.Generic;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.UI;
using UnityEngine;

namespace Project.Core.ZoneState
{
    internal static class ArtifactRewardDraft
    {
        public static bool TryPrepare(RouteReward terms, ArtifactRollTable table, out ArtifactInstance[][] pulls)
        {
            pulls = null;
            var common = new List<ArtifactDefinition>();
            var rare = new List<ArtifactDefinition>();
            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ArtifactDefinition definition in Resources.LoadAll<ArtifactDefinition>("ItemSO/Artifacts"))
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id) || !definitionIds.Add(definition.id))
                    continue;
                if (definition.rarity == ArtifactRarity.R) common.Add(definition);
                if (definition.rarity == ArtifactRarity.SR) rare.Add(definition);
            }
            common.Sort(CompareId);
            rare.Sort(CompareId);
            var random = new System.Random(terms.RewardSeed);
            Vector2 weights = table.GetPhaseRarityWeights(terms.BiomeIndex).hardRewardWeights;
            float commonWeight = common.Count >= ArtifactRewardPickerUI.CardsPerPull ? Mathf.Max(0f, weights.x) : 0f;
            float rareWeight = rare.Count >= ArtifactRewardPickerUI.CardsPerPull ? Mathf.Max(0f, weights.y) : 0f;
            if (commonWeight + rareWeight <= 0f) return false;

            var generator = new ArtifactRollGenerator(table);
            pulls = new ArtifactInstance[terms.ArtifactPulls][];
            for (int pull = 0; pull < pulls.Length; pull++)
            {
                bool chooseRare = random.NextDouble() * (commonWeight + rareWeight) >= commonWeight;
                var pool = new List<ArtifactDefinition>(chooseRare ? rare : common);
                var candidates = new ArtifactInstance[ArtifactRewardPickerUI.CardsPerPull];
                for (int card = 0; card < candidates.Length; card++)
                {
                    int index = random.Next(pool.Count);
                    candidates[card] = generator.Generate(pool[index], 0, random.Next(), terms.BiomeIndex);
                    pool.RemoveAt(index);
                }
                pulls[pull] = candidates;
            }
            return true;
        }

        private static int CompareId(ArtifactDefinition left, ArtifactDefinition right) =>
            string.Compare(left.id, right.id, StringComparison.Ordinal);
    }
}
