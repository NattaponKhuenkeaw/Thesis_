namespace Project.Core.ZoneState
{
    /// <summary>
    /// One next-room option after a room clear (BlazBlue-style UI).
    /// Combat/Elite offers carry difficulty and reward. Chamber layouts spawn from palettes.
    /// </summary>
    public readonly struct RoomChoiceOffer
    {
        public RoomChoiceOffer(RoomData room)
            : this(room, RouteDifficulty.Medium, null, null)
        {
        }

        public RoomChoiceOffer(
            RoomData room,
            RouteDifficulty difficulty,
            GeneratedEncounter encounter,
            RouteReward reward)
        {
            Room = room;
            Difficulty = difficulty;
            Encounter = encounter;
            Reward = reward;
        }

        public RoomData Room { get; }
        public RouteDifficulty Difficulty { get; }
        public GeneratedEncounter Encounter { get; }
        public RouteReward Reward { get; }

        public bool IsValid => Room != null;
        public string RoomId => Room != null ? Room.RoomId : string.Empty;
        public RoomKind Kind => Room != null ? Room.RoomKind : RoomKind.Combat;
        public string DisplayName => Room != null ? Room.DisplayName : string.Empty;
        public bool HasGeneratedCombat => Encounter != null && Encounter.HasWaves;

        public string RewardLabel => Reward != null ? Reward.BuildLabel() : string.Empty;

        public bool UsesCombatPalette => IsValid && RouteOfferGenerator.NeedsGeneratedCombat(Kind);
    }
}
