using Project.InventorySystem.Items;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Reward preview / payout rolled with a combat RouteOffer.
    /// </summary>
    public sealed class RouteReward
    {
        public int Coins;
        public bool GrantsArtifact;
        public Artifact Artifact;
        public int ArtifactPulls { get; private set; }
        public int RewardSeed { get; private set; }
        public int BiomeIndex { get; private set; }
        public bool IncludesBasePayout { get; private set; }
        public bool GrantsMemory { get; private set; }

        public static RouteReward Patterned(int coins, int pulls, int seed, int biomeIndex, bool grantsMemory = false) => new RouteReward
        {
            Coins = coins,
            ArtifactPulls = pulls,
            RewardSeed = seed,
            BiomeIndex = biomeIndex,
            IncludesBasePayout = true,
            GrantsMemory = grantsMemory
        };

        public string BuildLabel()
        {
            if (IncludesBasePayout)
            {
                string label = $"{Coins} Soul";
                if (ArtifactPulls > 0) label += $" + {ArtifactPulls} Artifact picks";
                if (GrantsMemory) label += " + Memory";
                return label;
            }
            if (GrantsArtifact && Artifact != null && Coins > 0)
            {
                return $"Reward [artifact + coin {Coins}]";
            }

            if (GrantsArtifact && Artifact != null)
            {
                return "Reward [artifact]";
            }

            if (Coins > 0)
            {
                return $"Reward [coin {Coins}]";
            }

            return "Reward [none]";
        }
    }
}
