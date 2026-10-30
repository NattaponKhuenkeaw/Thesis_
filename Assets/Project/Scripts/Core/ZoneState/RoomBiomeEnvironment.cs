using Project.Parallax;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Core.ZoneState
{
    [DisallowMultipleComponent]
    public class RoomBiomeEnvironment : MonoBehaviour
    {
        [SerializeField] private GameObject biomeOneEnvironment;
        [SerializeField] private SpriteRenderer[] biomeOneArtwork = System.Array.Empty<SpriteRenderer>();
        [SerializeField] private ParallaxBackground[] biomeOneParallax = System.Array.Empty<ParallaxBackground>();
        [SerializeField] private GameObject biomeTwoEnvironment;

        private bool[] artworkEnabled;
        private bool[] parallaxEnabled;
        private bool hasApplied;

        public int ActiveThemeIndex { get; private set; }
        public GameObject BiomeOneEnvironment => biomeOneEnvironment;
        public GameObject BiomeTwoEnvironment => biomeTwoEnvironment;

        protected virtual void Start()
        {
            // Direct scene tests also need the current theme; transitions apply earlier, under the fade.
            if (!hasApplied)
                ApplyCurrentBiome();
        }

        public void ApplyCurrentBiome()
        {
            var progression = RunProgressionManager.EnsureInstance();
            Apply(Resources.Load<MapSO>("MapSO/Test_MapSO"), progression != null ? progression.CurrentBiomeIndex : 0);
        }

        public static void ApplyInScene(Scene scene, MapSO catalog, int progressionBiomeIndex)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (RoomBiomeEnvironment environment in root.GetComponentsInChildren<RoomBiomeEnvironment>())
                    environment.Apply(catalog, progressionBiomeIndex);
        }

        public void Apply(MapSO catalog, int progressionBiomeIndex)
        {
            if (artworkEnabled == null)
            {
                artworkEnabled = new bool[biomeOneArtwork.Length];
                for (int i = 0; i < biomeOneArtwork.Length; i++)
                    artworkEnabled[i] = biomeOneArtwork[i] != null && biomeOneArtwork[i].enabled;
                parallaxEnabled = new bool[biomeOneParallax.Length];
                for (int i = 0; i < biomeOneParallax.Length; i++)
                    parallaxEnabled[i] = biomeOneParallax[i] != null && biomeOneParallax[i].enabled;
            }

            ActiveThemeIndex = catalog != null ? catalog.ResolveAuthoredBiomeIndex(progressionBiomeIndex) : 0;
            bool showFirstBiome = ActiveThemeIndex != 1 || biomeTwoEnvironment == null;
            if (biomeOneEnvironment != null)
                biomeOneEnvironment.SetActive(showFirstBiome);
            for (int i = 0; i < biomeOneArtwork.Length; i++)
                if (biomeOneArtwork[i] != null)
                    biomeOneArtwork[i].enabled = showFirstBiome && artworkEnabled[i];
            for (int i = 0; i < biomeOneParallax.Length; i++)
                if (biomeOneParallax[i] != null)
                {
                    biomeOneParallax[i].enabled = showFirstBiome && parallaxEnabled[i];
                    if (biomeOneParallax[i].enabled)
                        biomeOneParallax[i].ResetParallax();
                }
            if (biomeTwoEnvironment != null)
                biomeTwoEnvironment.SetActive(!showFirstBiome);
            hasApplied = true;
        }
    }
}
