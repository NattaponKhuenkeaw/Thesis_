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

        public static RouteReward Patterned(int coins, int pulls, int seed, int biomeIndex) => new RouteReward
        {
            Coins = coins,
            ArtifactPulls = pulls,
            RewardSeed = seed,
            BiomeIndex = biomeIndex,
            IncludesBasePayout = true
        };

        public string BuildLabel()
        {
            if (IncludesBasePayout)
                return ArtifactPulls > 0 ? $"{Coins} Soul + {ArtifactPulls} Artifact picks" : $"{Coins} Soul";
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
