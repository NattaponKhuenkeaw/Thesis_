using System.Collections;
using Project.Core.ZoneState;
using TMPro;
using UnityEngine;

namespace Project.Core.Lobby
{
    /// <summary>
    /// Prefab-authored title for the current biome, shown briefly on lobby entry.
    /// </summary>
    public sealed class LobbyLevelBanner : MonoBehaviour
    {
        private const string PrefabResourcePath = "UI/LobbyLevelBanner";

        [Header("Prefab References")]
        [SerializeField] private CanvasGroup presentation;
        [SerializeField] private TMP_Text levelLabel;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float fadeInSeconds = .45f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.15f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = .7f;

        public static string FormatLevelLabel(int biomeIndex)
        {
            return $"Level {Mathf.Max(0, biomeIndex) + 1}";
        }

        public static void Play()
        {
            int biomeIndex = RunProgressionManager.EnsureInstance() != null
                ? RunProgressionManager.Instance.CurrentBiomeIndex
                : 0;

            var prefab = Resources.Load<LobbyLevelBanner>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[LobbyLevelBanner] Missing Resources/UI/LobbyLevelBanner.prefab. No runtime UI will be generated.");
                return;
            }

            LobbyLevelBanner banner = Instantiate(prefab);
            banner.StartCoroutine(banner.PlayRoutine(FormatLevelLabel(biomeIndex)));
        }

        private IEnumerator PlayRoutine(string label)
        {
            if (presentation == null || levelLabel == null)
            {
                Debug.LogError("[LobbyLevelBanner] The saved prefab has missing presentation references.", this);
                Destroy(gameObject);
                yield break;
            }
            levelLabel.text = label.ToUpperInvariant();
            yield return Fade(0f, 1f, fadeInSeconds);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Fade(1f, 0f, fadeOutSeconds);
            Destroy(gameObject);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            float length = Mathf.Max(0.01f, duration);
            while (elapsed < length)
            {
                elapsed += Time.unscaledDeltaTime;
                presentation.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / length));
                yield return null;
            }

            presentation.alpha = to;
        }
    }
}
