using UnityEngine;
using TMPro;
using System.Collections;

namespace Project.ShopSystem.UI
{
    /// <summary>
    /// Singleton popup manager — spawns floating text messages on a screen-space Canvas.
    /// Attach this to a GameObject with a Canvas (Screen Space - Overlay recommended).
    /// Assign a TMP_Text prefab that will be instantiated for each popup.
    /// </summary>
    public class ShopPopupText : MonoBehaviour
    {
        public static ShopPopupText Instance { get; private set; }

        [Header("Popup Prefab")]
        [Tooltip("A TMP_Text prefab. It will be instantiated under this transform.")]
        [SerializeField] private GameObject popupTextPrefab;

        [Header("Animation Settings")]
        [SerializeField] private float displayDuration = 1.5f;
        [SerializeField] private float floatSpeed = 60f;    // pixels per second upward
        [SerializeField] private float fadeDuration = 0.5f;  // fade-out at the end

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Shows a popup message at the center of the screen (or at an optional screen position).
        /// </summary>
        public void Show(string message, Color color)
        {
            if (popupTextPrefab == null)
            {
                Debug.LogWarning("[ShopPopupText] No popupTextPrefab assigned.");
                return;
            }

            GameObject popupObj = Instantiate(popupTextPrefab, transform);
            TMP_Text tmpText = popupObj.GetComponent<TMP_Text>();

            if (tmpText == null)
            {
                Debug.LogWarning("[ShopPopupText] Prefab missing TMP_Text component.");
                Destroy(popupObj);
                return;
            }

            tmpText.text = message;
            tmpText.color = color;

            StartCoroutine(AnimatePopup(popupObj, tmpText));
        }

        /// <summary>
        /// Convenience overload — red text by default.
        /// </summary>
        public void Show(string message)
        {
            Show(message, new Color(1f, 0.3f, 0.3f, 1f));
        }

        private IEnumerator AnimatePopup(GameObject popupObj, TMP_Text tmpText)
        {
            RectTransform rect = popupObj.GetComponent<RectTransform>();
            Color startColor = tmpText.color;
            float elapsed = 0f;
            float totalDuration = displayDuration + fadeDuration;

            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                // Float upward
                if (rect != null)
                {
                    rect.anchoredPosition += Vector2.up * floatSpeed * Time.unscaledDeltaTime;
                }

                // Fade out during the last fadeDuration seconds
                if (elapsed > displayDuration)
                {
                    float fadeProgress = (elapsed - displayDuration) / fadeDuration;
                    Color c = startColor;
                    c.a = Mathf.Lerp(1f, 0f, fadeProgress);
                    tmpText.color = c;
                }

                yield return null;
            }

            Destroy(popupObj);
        }
    }
}
