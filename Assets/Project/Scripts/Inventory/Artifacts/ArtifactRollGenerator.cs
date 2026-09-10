using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using UnityEngine;

namespace Project.InventorySystem.Artifacts
{
    public interface IArtifactRandom
    {
        float Next01();
    }

    public sealed class SeededArtifactRandom : IArtifactRandom
    {
        private readonly System.Random random;

        public SeededArtifactRandom(int seed)
        {
            random = new System.Random(seed);
        }

        public float Next01()
        {
            return (float)random.NextDouble();
        }
    }

    public sealed class ArtifactRollGenerator
    {
        private readonly ArtifactRollTable table;

        public ArtifactRollGenerator(ArtifactRollTable table)
        {
            this.table = table != null ? table : throw new ArgumentNullException(nameof(table));
        }

        public ArtifactInstance Generate(
            ArtifactDefinition definition,
            long acquisitionOrder,
            int seed,
            string instanceId)
        {
            return Generate(definition, acquisitionOrder, seed, 0, instanceId);
        }

        public ArtifactInstance Generate(
            ArtifactDefinition definition,
            long acquisitionOrder,
            int seed,
            int biomeIndex = 0,
            string instanceId = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            return Generate(definition, acquisitionOrder, new SeededArtifactRandom(seed), seed, biomeIndex, instanceId);
        }

        public ArtifactInstance Generate(
            ArtifactDefinition definition,
            long acquisitionOrder,
            IArtifactRandom random,
            int generationSeed,
            string instanceId)
        {
            return Generate(definition, acquisitionOrder, random, generationSeed, 0, instanceId);
        }

        public ArtifactInstance Generate(
            ArtifactDefinition definition,
            long acquisitionOrder,
            IArtifactRandom random,
            int generationSeed = 0,
            int biomeIndex = 0,
            string instanceId = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (random == null) throw new ArgumentNullException(nameof(random));
            int safeBiome = Mathf.Max(0, biomeIndex);
            ArtifactInstance instance = new ArtifactInstance
            {
                instanceId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId,
                definitionId = definition.id,
                acquisitionOrder = acquisitionOrder,
                collectedTimestampUtc = DateTime.UtcNow.Ticks,
                generationSeed = generationSeed,
                acquisitionBiomeIndex = safeBiome,
                isNew = true
            };

            int lineCount = table.RollStatLineCount(random.Next01(), safeBiome);
            IReadOnlyList<ArtifactAmountBand> bands = table.GetAmountBandsForBiome(safeBiome);
            for (int i = 0; i < lineCount; i++)
            {
                ArtifactStatRollDefinition stat = SelectStat(random);
                ArtifactAmountBand band = SelectBand(random, bands);
                int valueUnits = RollUnits(stat, band, random.Next01());
                instance.rolls.Add(new ArtifactRoll(stat.statKind, valueUnits, ArtifactRollSource.Generated));
            }

            return instance;
        }

        private ArtifactStatRollDefinition SelectStat(IArtifactRandom random)
        {
            IReadOnlyList<ArtifactStatRollDefinition> definitions = table.Stats;
            float total = 0f;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].randomEligible)
                    total += Mathf.Max(0f, definitions[i].selectionWeight);
            }

            float roll = random.Next01() * total;
            if (total <= 0f) throw new InvalidOperationException("Artifact roll table has no eligible weighted stats.");
            ArtifactStatRollDefinition fallback = default;
            for (int i = 0; i < definitions.Count; i++)
            {
                ArtifactStatRollDefinition candidate = definitions[i];
                if (!candidate.randomEligible || candidate.selectionWeight <= 0f) continue;
                fallback = candidate;
                roll -= candidate.selectionWeight;
                if (roll <= 0f) return candidate;
            }
            return fallback;
        }

        private ArtifactAmountBand SelectBand(IArtifactRandom random)
        {
            return SelectBand(random, table.AmountBands);
        }

        private ArtifactAmountBand SelectBand(IArtifactRandom random, IReadOnlyList<ArtifactAmountBand> bands)
        {
            if (bands == null || bands.Count == 0) bands = table.AmountBands;
            float total = 0f;
            for (int i = 0; i < bands.Count; i++) total += Mathf.Max(0f, bands[i].weight);

            float roll = random.Next01() * total;
            if (total <= 0f) throw new InvalidOperationException("Artifact roll table has no weighted amount bands.");
            ArtifactAmountBand fallback = default;
            for (int i = 0; i < bands.Count; i++)
            {
                ArtifactAmountBand candidate = bands[i];
                if (candidate.weight <= 0f) continue;
                fallback = candidate;
                roll -= candidate.weight;
                if (roll <= 0f) return candidate;
            }
            return fallback;
        }

        private static int RollUnits(
            ArtifactStatRollDefinition definition,
            ArtifactAmountBand band,
            float normalizedWithinBand)
        {
            int range = definition.maximumUnits - definition.minimumUnits;
            float normalized = Mathf.Lerp(band.normalizedMin, band.normalizedMax, Mathf.Clamp01(normalizedWithinBand));
            return Mathf.Clamp(
                definition.minimumUnits + Mathf.RoundToInt(range * normalized),
                definition.minimumUnits,
                definition.maximumUnits);
        }
    }
}
