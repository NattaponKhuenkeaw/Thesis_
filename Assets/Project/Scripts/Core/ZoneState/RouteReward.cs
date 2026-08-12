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

        public string BuildLabel()
        {
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
