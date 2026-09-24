using System.Collections;
using Project.Core.ZoneState;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Core.Lobby
{
    /// <summary>
    /// Full-screen title that fades in with the current biome as "Level N", holds, then fades out.
    /// </summary>
    public sealed class LobbyLevelBanner : MonoBehaviour
    {
        private const float FadeInSeconds = 0.45f;
        private const float HoldSeconds = 1.15f;
        private const float FadeOutSeconds = 0.7f;

        public static string FormatLevelLabel(int biomeIndex)
        {
            return $"Level {Mathf.Max(0, biomeIndex) + 1}";
        }

        public static void Play()
        {
            int biomeIndex = RunProgressionManager.EnsureInstance() != null
                ? RunProgressionManager.Instance.CurrentBiomeIndex
                : 0;

            GameObject host = new GameObject("LobbyLevelBanner");
            LobbyLevelBanner banner = host.AddComponent<LobbyLevelBanner>();
            banner.StartCoroutine(banner.PlayRoutine(FormatLevelLabel(biomeIndex)));
        }

        private IEnumerator PlayRoutine(string label)
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8000;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject textObject = new GameObject("LevelLabel");
            textObject.transform.SetParent(transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 72f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 160f);
            rect.anchoredPosition = Vector2.zero;

            yield return Fade(text, 0f, 1f, FadeInSeconds);
            yield return new WaitForSecondsRealtime(HoldSeconds);
            yield return Fade(text, 1f, 0f, FadeOutSeconds);
            Destroy(gameObject);
        }

        private static IEnumerator Fade(TextMeshProUGUI text, float from, float to, float duration)
        {
            float elapsed = 0f;
            float length = Mathf.Max(0.01f, duration);
            while (elapsed < length)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / length));
                Color color = text.color;
                color.a = alpha;
                text.color = color;
                yield return null;
            }

            Color finalColor = text.color;
            finalColor.a = to;
            text.color = finalColor;
        }
    }
}
