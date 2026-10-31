using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MaxCancelFeedback : MonoBehaviour
{
    // Presentation starting values; verify readability during the next Ultimate's own effects.
    public const float EnterDuration = 0.16f;
    public const float HoldDuration = 0.50f;
    public const float ExitDuration = 0.22f;
    public const float Duration = EnterDuration + HoldDuration + ExitDuration;
    private CanvasGroup _group;
    private RectTransform _label;
    private RectTransform _content;
    private RectTransform _aura;
    private CanvasGroup _auraGroup;
    private GameObject _root;
    private float _elapsed;
    public bool IsVisible => _root != null && _root.activeSelf;
    public string DisplayedText => _label != null ? _label.GetComponent<TextMeshProUGUI>().text : string.Empty;

    public static void Show(PlayerCharacter player)
    {
        if (player == null) return;
        MaxCancelFeedback feedback = player.GetComponent<MaxCancelFeedback>();
        if (feedback == null)
            feedback = player.gameObject.AddComponent<MaxCancelFeedback>();
        if (feedback._root == null)
        {
            // A screen overlay must not inherit the fighter's facing rotation or sprite scale.
            var root = new GameObject("MAX CANCEL", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            feedback._root = root;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            feedback._group = root.GetComponent<CanvasGroup>();
            feedback._group.blocksRaycasts = false;
            feedback._group.interactable = false;
            feedback._content = new GameObject("Animated Content", typeof(RectTransform)).GetComponent<RectTransform>();
            feedback._content.SetParent(root.transform, false);
            feedback._content.anchorMin = feedback._content.anchorMax = new Vector2(0f, 0.6f);
            feedback._content.pivot = new Vector2(0f, 0.5f);
            feedback._content.sizeDelta = new Vector2(600f, 100f);
            var streak = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            streak.transform.SetParent(feedback._content, false);
            var stripe = streak.GetComponent<Image>();
            stripe.color = new Color(0.95f, 0.17f, 0.04f, 0.65f);
            stripe.raycastTarget = false;
            var rect = streak.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f); rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(720f, 3f); rect.anchoredPosition = new Vector2(-64f, 0f);
            GameObject template = Resources.Load<GameObject>("UI/MaxCancelLabel");
            TextMeshProUGUI label = CreateLabel("Label", feedback._content, template);
            label.color = new Color(1f, 0.83f, 0.32f);
            label.outlineWidth = 0.18f;
            label.outlineColor = new Color32(65, 27, 20, 255);
            feedback._label = label.rectTransform;
            Vector2 textSize = new Vector2(Mathf.Ceil(label.preferredWidth) + 8f, 100f);
            feedback._content.sizeDelta = textSize;
            feedback._label.sizeDelta = textSize;
            feedback._aura = new GameObject("White Growing Aura", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            feedback._aura.SetParent(feedback._content, false);
            feedback._aura.anchorMin = feedback._aura.anchorMax = new Vector2(0.5f, 0.5f);
            feedback._aura.sizeDelta = feedback._content.sizeDelta;
            feedback._auraGroup = feedback._aura.GetComponent<CanvasGroup>();
            feedback._auraGroup.blocksRaycasts = false;
            TextMeshProUGUI glow = CreateLabel("White Aura", feedback._aura, template);
            glow.rectTransform.sizeDelta = textSize;
            glow.color = Color.white; glow.outlineColor = Color.white; glow.outlineWidth = 0.3f;
            TextMeshProUGUI halo = CreateLabel("Outer Halo", feedback._aura, template);
            halo.rectTransform.sizeDelta = textSize;
            halo.color = new Color(1f, 1f, 1f, 0.22f); halo.outlineColor = Color.white; halo.outlineWidth = 0.4f;
            halo.rectTransform.localScale = Vector3.one * 1.04f;
        }
        feedback._root.SetActive(true);
        feedback.Sample(0f);
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, GameObject template)
    {
        GameObject text = template != null ? Instantiate(template, parent, false)
            : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        text.name = name;
        text.transform.SetParent(parent, false);
        var label = text.GetComponent<TextMeshProUGUI>();
        label.text = "MAX CANCEL"; label.fontSize = 48f; label.fontStyle = FontStyles.Normal;
        label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(600f, 100f); rect.anchoredPosition = Vector2.zero;
        return label;
    }

    private void Update()
    {
        if (!IsVisible) return;
        Sample(_elapsed + Time.unscaledDeltaTime);
    }

    private static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

    public void Sample(float elapsed)
    {
        if (_root == null) return;
        _elapsed = Mathf.Max(0f, elapsed);
        float x;
        if (_elapsed < EnterDuration)
        {
            float enter = EaseOut(_elapsed / EnterDuration);
            x = Mathf.Lerp(-300f, 64f, enter);
            _group.alpha = enter;
        }
        else if (_elapsed < EnterDuration + HoldDuration)
        {
            x = 64f + 30f * (_elapsed - EnterDuration) / HoldDuration;
            _group.alpha = 1f;
        }
        else
        {
            float exit = EaseOut((_elapsed - EnterDuration - HoldDuration) / ExitDuration);
            x = 94f + 260f * exit;
            _group.alpha = 1f - exit;
        }
        _content.anchoredPosition = new Vector2(x, 0f);
        float aura = Mathf.Clamp01((_elapsed - 0.04f) / 0.36f);
        _aura.localScale = Vector3.one * Mathf.Lerp(1f, 1.55f, EaseOut(aura));
        _auraGroup.alpha = _elapsed < 0.04f ? 0f : 0.65f * (1f - aura) * (1f - aura);
        if (_elapsed >= Duration) _root.SetActive(false);
    }

    private void OnDisable() { if (_root != null) _root.SetActive(false); }
    private void OnDestroy()
    {
        if (_root == null) return;
        if (Application.isPlaying) Destroy(_root);
        else DestroyImmediate(_root);
    }
}
