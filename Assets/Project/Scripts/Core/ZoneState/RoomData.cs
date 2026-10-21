using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Catalog entry for one room.
    /// Legacy rooms use <see cref="sceneName"/> only (full scene load).
    /// Hybrid rooms also assign <see cref="layoutPrefab"/> so a RunShell can swap content in-place.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomData", menuName = "Project/Core/RoomData")]
    public class RoomData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable room key used by MapSO / TransitionToRoom. Defaults to sceneName when empty.")]
        [SerializeField] private string roomId;

        [Tooltip("Unity scene used for legacy full loads, or the RunShell scene that hosts layout swaps.")]
        [SerializeField] private string sceneName;

        [Header("Hybrid Layout (Optional)")]
        [Tooltip("If set and a RoomLayoutHost exists in the active shell, content swaps without loading a new scene.")]
        [SerializeField] private GameObject layoutPrefab;

        [Header("BlazBlue Exit Offer")]
        [Tooltip("Category shown on the choice UI after a room clear.")]
        [SerializeField] private RoomKind roomKind = RoomKind.Combat;

        [Tooltip("Label on the choice card. Falls back to roomId when empty.")]
        [SerializeField] private string displayName;

        [Tooltip("Optional icon shown on the choice card.")]
        [SerializeField] private Sprite offerIcon;

        [Tooltip("Relative weight when rolling random offers of this kind.")]
        [SerializeField, Min(0f)] private float offerWeight = 1f;

        [Tooltip("If false, this room is never rolled into post-clear choices.")]
        [SerializeField] private bool includeInRandomOffers = true;

        [Header("Room Setup")]
        [Tooltip("The ZoneMode (SafeZone or CombatZone) associated with this room.")]
        [SerializeField] private ZoneMode zoneMode = ZoneMode.CombatZone;

        [Tooltip("The default entrance point ID for spawning when entering this room.")]
        [SerializeField] private string defaultEntranceId = "Default";

        [Tooltip("If true, the camera will switch to a static camera with no following target in this room.")]
        [SerializeField] private bool useStaticCamera = false;

        [Header("Encounter (Optional)")]
        [Tooltip("Fallback encounter when CombatStageController / RouteOffer has no palette. Boss rooms spawn from this asset.")]
        [SerializeField] private EncounterSO encounter;

        [Header("Background Music")]
        [Tooltip("Room music. Empty fades the previous room's track out.")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;

        public string RoomId => string.IsNullOrWhiteSpace(roomId) ? sceneName : roomId;
        public string SceneName => sceneName;
        public GameObject LayoutPrefab => layoutPrefab;
        public RoomKind RoomKind => roomKind;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? RoomId : displayName;
        public Sprite OfferIcon => offerIcon;
        public float OfferWeight => offerWeight;
        public bool IncludeInRandomOffers => includeInRandomOffers;
        public ZoneMode ZoneMode => zoneMode;
        public string DefaultEntranceId => defaultEntranceId;
        public bool UseStaticCamera => useStaticCamera;
        public EncounterSO Encounter => encounter;
        public AudioClip BackgroundMusic => backgroundMusic;
        public float MusicVolume => Mathf.Clamp01(musicVolume);
        public bool UsesLayoutSwap => layoutPrefab != null;
    }
}
