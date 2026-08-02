namespace Project.Core.ZoneState
{
    /// <summary>
    /// One next-room option after a room clear (BlazBlue-style UI).
    /// </summary>
    public readonly struct RoomChoiceOffer
    {
        public RoomChoiceOffer(RoomData room)
        {
            Room = room;
        }

        public RoomData Room { get; }
        public bool IsValid => Room != null;
        public string RoomId => Room != null ? Room.RoomId : string.Empty;
        public RoomKind Kind => Room != null ? Room.RoomKind : RoomKind.Combat;
        public string DisplayName => Room != null ? Room.DisplayName : string.Empty;
    }
}
