using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using UnityEngine;

namespace Project.InventorySystem.Artifacts
{
    [Serializable]
    public struct ArtifactStatRollDefinition
    {
        public ArtifactStatKind statKind;
        public int minimumUnits;
        public int maximumUnits;
        [Min(0f)] public float selectionWeight;
        public bool randomEligible;

        public ArtifactStatRollDefinition(
            ArtifactStatKind statKind,
            float minimum,
            float maximum,
            float selectionWeight = 1f,
            bool randomEligible = true)
        {
            this.statKind = statKind;
            int scale = ArtifactRollValue.ScaleFor(statKind);
            minimumUnits = Mathf.RoundToInt(minimum * scale);
            maximumUnits = Mathf.RoundToInt(maximum * scale);
            this.selectionWeight = selectionWeight;
            this.randomEligible = randomEligible;
        }
    }

    [Serializable]
    public struct ArtifactRarityRollProfile
    {
        public ArtifactRarity rarity;
        public List<ArtifactAmountBand> amountBands;
        public List<float> lineCountWeights;
    }

    [Serializable]
    public struct ArtifactPhaseRarityWeights
    {
        public Vector3 shopWeights;
        public Vector2 hardRewardWeights;
    }

    [Serializable]
    public struct ArtifactAmountBand
    {
        [Range(0f, 1f)] public float normalizedMin;
        [Range(0f, 1f)] public float normalizedMax;
        [Min(0f)] public float weight;

        public ArtifactAmountBand(float normalizedMin, float normalizedMax, float weight)
        {
            this.normalizedMin = normalizedMin;
            this.normalizedMax = normalizedMax;
            this.weight = weight;
        }
    }

    [Serializable]
    public struct ArtifactBiomeRollProfile
    {
        public string biomeName;
        public int biomeIndex;
        public List<ArtifactAmountBand> amountBands;
        public List<float> lineCountWeights;

        public ArtifactBiomeRollProfile(
            string biomeName,
            int biomeIndex,
            List<ArtifactAmountBand> amountBands,
            List<float> lineCountWeights = null)
        {
            this.biomeName = biomeName;
            this.biomeIndex = biomeIndex;
            this.amountBands = amountBands != null ? amountBands : new List<ArtifactAmountBand>();
            this.lineCountWeights = lineCountWeights != null ? lineCountWeights : new List<float>();
        }
    }

    [CreateAssetMenu(fileName = "ArtifactRollTable", menuName = "Inventory/Artifact Roll Table", order = 2)]
    public sealed class ArtifactRollTable : ScriptableObject
    {
        [SerializeField] private List<ArtifactStatRollDefinition> stats = new List<ArtifactStatRollDefinition>();
        [SerializeField] private List<ArtifactAmountBand> amountBands = new List<ArtifactAmountBand>();
        [SerializeField] private List<ArtifactBiomeRollProfile> biomeProfiles = new List<ArtifactBiomeRollProfile>();
        [SerializeField] private List<ArtifactRarityRollProfile> rarityProfiles = new List<ArtifactRarityRollProfile>();
        [SerializeField] private List<ArtifactPhaseRarityWeights> phaseRarityWeights = new List<ArtifactPhaseRarityWeights>();

        private Dictionary<ArtifactStatKind, ArtifactStatRollDefinition> definitionsByKind;

        public IReadOnlyList<ArtifactStatRollDefinition> Stats => stats;
        public IReadOnlyList<ArtifactAmountBand> AmountBands => amountBands;
        public IReadOnlyList<ArtifactBiomeRollProfile> BiomeProfiles => biomeProfiles;

        public ArtifactPhaseRarityWeights GetPhaseRarityWeights(int biomeIndex)
        {
            if (phaseRarityWeights.Count == 0)
                return new ArtifactPhaseRarityWeights { shopWeights = Vector3.one, hardRewardWeights = Vector2.right };
            return phaseRarityWeights[Mathf.Clamp(biomeIndex, 0, phaseRarityWeights.Count - 1)];
        }

        public bool TryGetRarityProfile(ArtifactRarity rarity, out ArtifactRarityRollProfile profile)
        {
            foreach (ArtifactRarityRollProfile candidate in rarityProfiles)
            {
                if (candidate.rarity != rarity || rarity == ArtifactRarity.Unassigned) continue;
                profile = candidate;
                return true;
            }

            profile = default;
            return false;
        }

        public static int RollRarityLineCount(float percentile01, IReadOnlyList<float> weights)
        {
            float total = 0f;
            foreach (float weight in weights)
                total += Mathf.Max(0f, weight);

            if (total <= 0f) throw new InvalidOperationException("Artifact rarity has no weighted line counts.");
            float remaining = Mathf.Clamp01(percentile01) * total;
            int lastEligible = 1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                lastEligible = i + 1;
                remaining -= weights[i];
                if (remaining <= 0f) return lastEligible;
            }

            return lastEligible;
        }

        private void OnEnable()
        {
            EnsureDefaults();
            RebuildIndex();
        }

        private void OnValidate()
        {
            EnsureDefaults();
            RebuildIndex();
        }

        public int RollStatLineCount(float percentile01)
        {
            float roll = Mathf.Clamp01(percentile01);
            if (roll < 0.15f) return 4;
            if (roll < 0.35f) return 3;
            if (roll < 0.50f) return 2;
            return 1;
        }

        public int RollStatLineCount(float percentile01, int biomeIndex)
        {
            if (biomeIndex <= 0)
            {
                return RollStatLineCount(percentile01);
            }

            int safeBiome = Mathf.Max(0, biomeIndex);
            if (biomeProfiles != null && biomeProfiles.Count > 0)
            {
                ArtifactBiomeRollProfile matchedProfile = default;
                bool found = false;
                for (int i = 0; i < biomeProfiles.Count; i++)
                {
                    if (biomeProfiles[i].biomeIndex == safeBiome)
                    {
                        matchedProfile = biomeProfiles[i];
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    int highest = -1;
                    for (int i = 0; i < biomeProfiles.Count; i++)
                    {
                        if (biomeProfiles[i].biomeIndex > highest)
                        {
                            highest = biomeProfiles[i].biomeIndex;
                            matchedProfile = biomeProfiles[i];
                            found = true;
                        }
                    }
                }

                if (found && matchedProfile.lineCountWeights != null && matchedProfile.lineCountWeights.Count >= 4)
                {
                    float totalWeight = 0f;
                    for (int i = 0; i < 4; i++)
                    {
                        totalWeight += Mathf.Max(0f, matchedProfile.lineCountWeights[i]);
                    }

                    if (totalWeight > 0f)
                    {
                        float roll = Mathf.Clamp01(percentile01) * totalWeight;
                        for (int i = 0; i < 4; i++)
                        {
                            float w = Mathf.Max(0f, matchedProfile.lineCountWeights[i]);
                            roll -= w;
                            if (roll <= 0f) return i + 1;
                        }
                        return 4;
                    }
                }
            }

            return RollStatLineCount(percentile01);
        }

        public IReadOnlyList<ArtifactAmountBand> GetAmountBandsForBiome(int biomeIndex)
        {
            int safeBiome = Mathf.Max(0, biomeIndex);
            if (biomeProfiles != null && biomeProfiles.Count > 0)
            {
                for (int i = 0; i < biomeProfiles.Count; i++)
                {
                    if (biomeProfiles[i].biomeIndex == safeBiome)
                    {
                        if (biomeProfiles[i].amountBands != null && biomeProfiles[i].amountBands.Count > 0)
                        {
                            return biomeProfiles[i].amountBands;
                        }
                    }
                }

                int highestBiomeIndex = -1;
                int bestProfileIdx = -1;
                for (int i = 0; i < biomeProfiles.Count; i++)
                {
                    if (biomeProfiles[i].amountBands != null && biomeProfiles[i].amountBands.Count > 0)
                    {
                        if (biomeProfiles[i].biomeIndex > highestBiomeIndex)
                        {
                            highestBiomeIndex = biomeProfiles[i].biomeIndex;
                            bestProfileIdx = i;
                        }
                    }
                }

                if (bestProfileIdx >= 0)
                {
                    return biomeProfiles[bestProfileIdx].amountBands;
                }
            }

            return amountBands;
        }

        public bool TryGet(ArtifactStatKind kind, out ArtifactStatRollDefinition definition)
        {
            definitionsByKind ??= BuildIndex();
            return definitionsByKind.TryGetValue(kind, out definition);
        }

        public static ArtifactRollTable CreateRuntimeDefault()
        {
            ArtifactRollTable table = CreateInstance<ArtifactRollTable>();
            table.hideFlags = HideFlags.HideAndDontSave;
            table.EnsureDefaults();
            table.RebuildIndex();
            return table;
        }

        private void EnsureDefaults()
        {
            if (stats == null || stats.Count == 0)
            {
                stats = new List<ArtifactStatRollDefinition>
                {
                    new ArtifactStatRollDefinition(ArtifactStatKind.FlatMaxHP, 153f, 1025f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.FlatAttack, 18f, 116f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.FlatDefense, 22f, 138f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.MaxHPPercent, 7f, 34.98f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.AttackPercent, 7f, 34.98f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.DefensePercent, 8.74f, 43.74f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.CooldownMastery, 5.12f, 25.50f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.CritRate, 4.66f, 23.34f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.CritDamage, 5.12f, 25.50f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.AuraEnergyRecharge, 7.78f, 38.88f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.DotMastery, 9.32f, 56.23f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.DamageIncrease, 9.32f, 56.23f),
                    new ArtifactStatRollDefinition(ArtifactStatKind.AdditionalDamageIncrease, 9.32f, 56.23f)
                };
            }

            if (amountBands == null || amountBands.Count == 0)
            {
                amountBands = new List<ArtifactAmountBand>
                {
                    new ArtifactAmountBand(0f, 0.2f, 35f),
                    new ArtifactAmountBand(0.2f, 0.4f, 30f),
                    new ArtifactAmountBand(0.4f, 0.6f, 20f),
                    new ArtifactAmountBand(0.6f, 0.8f, 10f),
                    new ArtifactAmountBand(0.8f, 1f, 5f)
                };
            }

            if (biomeProfiles == null || biomeProfiles.Count == 0)
            {
                biomeProfiles = new List<ArtifactBiomeRollProfile>
                {
                    new ArtifactBiomeRollProfile(
                        "Biome 1",
                        0,
                        new List<ArtifactAmountBand>
                        {
                            new ArtifactAmountBand(0f, 0.2f, 35f),
                            new ArtifactAmountBand(0.2f, 0.4f, 30f),
                            new ArtifactAmountBand(0.4f, 0.6f, 20f),
                            new ArtifactAmountBand(0.6f, 0.8f, 10f),
                            new ArtifactAmountBand(0.8f, 1f, 5f)
                        },
                        new List<float> { 50f, 15f, 20f, 15f }),
                    new ArtifactBiomeRollProfile(
                        "Biome 2",
                        1,
                        new List<ArtifactAmountBand>
                        {
                            new ArtifactAmountBand(0f, 0.2f, 15f),
                            new ArtifactAmountBand(0.2f, 0.4f, 25f),
                            new ArtifactAmountBand(0.4f, 0.6f, 35f),
                            new ArtifactAmountBand(0.6f, 0.8f, 20f),
                            new ArtifactAmountBand(0.8f, 1f, 5f)
                        },
                        new List<float> { 30f, 25f, 25f, 20f }),
                    new ArtifactBiomeRollProfile(
                        "Biome 3",
                        2,
                        new List<ArtifactAmountBand>
                        {
                            new ArtifactAmountBand(0f, 0.2f, 5f),
                            new ArtifactAmountBand(0.2f, 0.4f, 10f),
                            new ArtifactAmountBand(0.4f, 0.6f, 25f),
                            new ArtifactAmountBand(0.6f, 0.8f, 35f),
                            new ArtifactAmountBand(0.8f, 1f, 25f)
                        },
                        new List<float> { 15f, 20f, 35f, 30f })
                };
            }

            if (rarityProfiles == null || rarityProfiles.Count == 0)
            {
                rarityProfiles = new List<ArtifactRarityRollProfile>
                {
                    new ArtifactRarityRollProfile
                    {
                        rarity = ArtifactRarity.R,
                        lineCountWeights = new List<float> { 100f },
                        amountBands = new List<ArtifactAmountBand>
                        {
                            new ArtifactAmountBand(0f, .2f, 70f),
                            new ArtifactAmountBand(.2f, .4f, 25f),
                            new ArtifactAmountBand(.4f, .6f, 5f)
                        }
                    },
                    new ArtifactRarityRollProfile
                    {
                        rarity = ArtifactRarity.SR,
                        lineCountWeights = new List<float> { 65f, 35f },
                        amountBands = new List<ArtifactAmountBand>(amountBands)
                    },
                    new ArtifactRarityRollProfile
                    {
                        rarity = ArtifactRarity.SSR,
                        lineCountWeights = new List<float> { 15f, 20f, 35f, 30f },
                        amountBands = new List<ArtifactAmountBand>
                        {
                            new ArtifactAmountBand(0f, .2f, 5f),
                            new ArtifactAmountBand(.2f, .4f, 10f),
                            new ArtifactAmountBand(.4f, .6f, 25f),
                            new ArtifactAmountBand(.6f, .8f, 35f),
                            new ArtifactAmountBand(.8f, 1f, 25f)
                        }
                    }
                };
            }

            if (phaseRarityWeights == null || phaseRarityWeights.Count == 0)
            {
                phaseRarityWeights = new List<ArtifactPhaseRarityWeights>
                {
                    new ArtifactPhaseRarityWeights { shopWeights = new Vector3(90, 10, 0), hardRewardWeights = new Vector2(100, 0) },
                    new ArtifactPhaseRarityWeights { shopWeights = new Vector3(55, 45, 0), hardRewardWeights = new Vector2(70, 30) },
                    new ArtifactPhaseRarityWeights { shopWeights = new Vector3(25, 45, 30), hardRewardWeights = new Vector2(35, 65) }
                };
            }
        }

        private void RebuildIndex()
        {
            definitionsByKind = BuildIndex();
        }

        private Dictionary<ArtifactStatKind, ArtifactStatRollDefinition> BuildIndex()
        {
            Dictionary<ArtifactStatKind, ArtifactStatRollDefinition> index = new Dictionary<ArtifactStatKind, ArtifactStatRollDefinition>();
            if (stats == null) return index;

            for (int i = 0; i < stats.Count; i++)
            {
                ArtifactStatRollDefinition definition = stats[i];
                index[definition.statKind] = definition;
            }
            return index;
        }
    }
}
