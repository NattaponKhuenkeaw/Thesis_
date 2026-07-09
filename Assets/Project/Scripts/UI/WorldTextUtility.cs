using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum WorldTextType
{
    Damage,
    Dot,
    Shield,
    Missed,
    Heal,
    Additional
}

public struct WorldTextRequest
{
    public Transform source;
    public Transform canvasRoot;
    public TMP_Text template;
    public WorldTextType type;
    public float amount;
    public string text;
    public DamageRoutine damageRoutine;
    public DamageImpactTier impactTier;
    public ElementType elementType;
    public bool isCritical;
    public Vector3 offset;
    public Vector3 worldPosition;
    public bool useWorldPosition;

    public Vector3 ResolveWorldPosition()
    {
        if (useWorldPosition)
        {
            return worldPosition + offset;
        }

        Transform anchor = source != null ? source : canvasRoot;
        return anchor != null ? anchor.position + offset : offset;
    }
}

public class WorldTextUtility : MonoBehaviour
{
    [System.Serializable]
    private class WorldTextStyle
    {
        public Color color = Color.white;
        public float fontSize = 50f;
        public FontStyles fontStyle = FontStyles.Bold;
        public float scaleMultiplier = 1f;
        public bool useElementColor;
    }

    private static WorldTextUtility instance;

    public static WorldTextUtility Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<WorldTextUtility>();
            }

            if (instance == null)
            {
                GameObject go = new GameObject(nameof(WorldTextUtility));
                instance = go.AddComponent<WorldTextUtility>();
            }

            return instance;
        }
    }

    [Header("Root")]
    [SerializeField] private Transform textRoot;
    [SerializeField] private TMP_Text defaultTextTemplate;
    [SerializeField] private int maxTextInstances = 40;
    [SerializeField] private Vector2 defaultSize = new Vector2(220f, 64f);
    [SerializeField] private float runtimeCanvasScale = 0.01f;
    [SerializeField] private int sortingOrder = 10000;

    [Header("Motion")]
    [SerializeField] private Vector2 randomRangeX = new Vector2(-0.35f, 0.35f);
    [SerializeField] private Vector2 randomRangeY = new Vector2(-0.1f, 0.25f);
    [SerializeField] private float popDownDistance = 0.28f;
    [SerializeField] private float slideUpDistance = 0.65f;
    [SerializeField] private float fadeInDuration = 0.12f;
    [SerializeField] private float holdDuration = 0.28f;
    [SerializeField] private float fadeOutDuration = 0.45f;
    [SerializeField] private float popStartScaleMultiplier = 1.2f;
    [SerializeField] private float fadeEndScaleMultiplier = 0.82f;

    [Header("Damage Scale")]
    [SerializeField] private float elementalDamageScaleMultiplier = 1.5f;
    [SerializeField] private float criticalScaleMultiplier = 1.5f;
    [SerializeField] private float lowTierScale = 1f;
    [SerializeField] private float mediumTierScale = 1.15f;
    [SerializeField] private float heavyTierScale = 1.3f;
    [SerializeField] private float ultimateTierScale = 1.6f;

    [Header("Styles")]
    [SerializeField] private WorldTextStyle damageStyle = new WorldTextStyle
    {
        color = Color.white,
        fontSize = 50f,
        fontStyle = FontStyles.Bold,
        scaleMultiplier = 1f,
        useElementColor = true
    };
    [SerializeField] private WorldTextStyle dotStyle = new WorldTextStyle
    {
        color = new Color(1f, 0.55f, 0.18f, 1f),
        fontSize = 42f,
        fontStyle = FontStyles.Bold,
        scaleMultiplier = 0.85f,
        useElementColor = true
    };
    [SerializeField] private WorldTextStyle shieldStyle = new WorldTextStyle
    {
        color = new Color(0.65f, 0.7f, 0.78f, 1f),
        fontSize = 44f,
        fontStyle = FontStyles.Bold,
        scaleMultiplier = 0.95f
    };
    [SerializeField] private WorldTextStyle missedStyle = new WorldTextStyle
    {
        color = new Color(0.85f, 0.9f, 1f, 1f),
        fontSize = 42f,
        fontStyle = FontStyles.Italic,
        scaleMultiplier = 0.85f
    };
    [SerializeField] private WorldTextStyle healStyle = new WorldTextStyle
    {
        color = new Color(0.35f, 1f, 0.45f, 1f),
        fontSize = 44f,
        fontStyle = FontStyles.Bold,
        scaleMultiplier = 0.95f
    };
    [SerializeField] private WorldTextStyle additionalStyle = new WorldTextStyle
    {
        color = new Color(0.78f, 0.72f, 1f, 1f),
        fontSize = 42f,
        fontStyle = FontStyles.Bold,
        scaleMultiplier = 0.85f,
        useElementColor = true
    };

    private readonly List<TMP_Text> textPool = new List<TMP_Text>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }

        ResolveTextRoot();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static bool Show(WorldTextRequest request)
    {
        WorldTextUtility utility = Instance;
        return utility != null && utility.TryShow(request);
    }

    public bool TryShow(WorldTextRequest request)
    {
        string displayText = ResolveDisplayText(request);
        if (string.IsNullOrWhiteSpace(displayText))
        {
            return false;
        }

        TMP_Text text = GetPooledText(request.template);
        if (text == null)
        {
            return false;
        }

        WorldTextStyle style = GetStyle(request.type);
        Color baseColor = ResolveColor(request, style);
        float targetScale = ResolveScale(request, style);
        Vector3 holdPosition = request.ResolveWorldPosition() + ResolveRandomOffset();
        Vector3 startPosition = holdPosition + Vector3.up * Mathf.Max(0f, popDownDistance);

        PrepareText(text, displayText, style, baseColor, startPosition, targetScale);
        StartCoroutine(TextRoutine(text, startPosition, holdPosition, baseColor, targetScale));
        return true;
    }

    private Transform ResolveTextRoot()
    {
        if (textRoot != null)
        {
            return textRoot;
        }

        Canvas ownCanvas = GetComponent<Canvas>();
        if (ownCanvas != null)
        {
            textRoot = ownCanvas.transform;
            return textRoot;
        }

        Canvas childCanvas = GetComponentInChildren<Canvas>(true);
        if (childCanvas != null)
        {
            textRoot = childCanvas.transform;
            return textRoot;
        }

        GameObject canvasObject = new GameObject("WorldTextCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.localScale = Vector3.one * Mathf.Max(0.0001f, runtimeCanvasScale);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f;

        textRoot = canvasObject.transform;
        return textRoot;
    }

    private TMP_Text GetPooledText(TMP_Text template)
    {
        for (int i = 0; i < textPool.Count; i++)
        {
            TMP_Text pooled = textPool[i];
            if (pooled != null && !pooled.gameObject.activeSelf)
            {
                return pooled;
            }
        }

        if (textPool.Count >= Mathf.Max(1, maxTextInstances))
        {
            return null;
        }

        TMP_Text created = CreateText(template != null ? template : defaultTextTemplate);
        if (created != null)
        {
            textPool.Add(created);
        }

        return created;
    }

    private TMP_Text CreateText(TMP_Text template)
    {
        Transform root = ResolveTextRoot();
        if (root == null)
        {
            return null;
        }

        TMP_Text text;
        if (template != null)
        {
            text = Instantiate(template, root);
            text.name = "WorldText";
        }
        else
        {
            GameObject textObject = new GameObject("WorldText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.sizeDelta = defaultSize;

            text = textObject.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
        }

        text.raycastTarget = false;
        text.gameObject.SetActive(false);
        return text;
    }

    private void PrepareText(
        TMP_Text text,
        string displayText,
        WorldTextStyle style,
        Color baseColor,
        Vector3 startPosition,
        float targetScale)
    {
        RectTransform rect = text.rectTransform;
        text.text = displayText;
        text.fontSize = Mathf.Max(1f, style.fontSize);
        text.fontStyle = style.fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;

        Color startColor = baseColor;
        startColor.a = 0f;
        text.color = startColor;

        rect.SetParent(ResolveTextRoot(), true);
        rect.position = startPosition;
        rect.rotation = Quaternion.identity;
        rect.localScale = Vector3.one * targetScale * Mathf.Max(0.01f, popStartScaleMultiplier);
        rect.SetAsLastSibling();
        text.gameObject.SetActive(true);
    }

    private IEnumerator TextRoutine(
        TMP_Text text,
        Vector3 startPosition,
        Vector3 holdPosition,
        Color baseColor,
        float targetScale)
    {
        RectTransform rect = text.rectTransform;
        float elapsed = 0f;
        float fadeInTime = Mathf.Max(0.01f, fadeInDuration);
        float startScale = targetScale * Mathf.Max(0.01f, popStartScaleMultiplier);

        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInTime);
            float eased = EaseOutCubic(t);

            rect.position = Vector3.Lerp(startPosition, holdPosition, eased);
            rect.rotation = Quaternion.identity;
            rect.localScale = Vector3.one * Mathf.Lerp(startScale, targetScale, EaseOutBack(t));

            Color color = baseColor;
            color.a = t;
            text.color = color;
            yield return null;
        }

        rect.position = holdPosition;
        rect.rotation = Quaternion.identity;
        rect.localScale = Vector3.one * targetScale;
        text.color = baseColor;

        float holdTime = Mathf.Max(0f, holdDuration);
        if (holdTime > 0f)
        {
            yield return new WaitForSeconds(holdTime);
        }

        elapsed = 0f;
        float fadeTime = Mathf.Max(0.01f, fadeOutDuration);
        Vector3 endPosition = holdPosition + Vector3.up * Mathf.Max(0f, slideUpDistance);
        float endScale = targetScale * Mathf.Max(0.01f, fadeEndScaleMultiplier);

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeTime);
            float eased = EaseOutCubic(t);

            rect.position = Vector3.Lerp(holdPosition, endPosition, eased);
            rect.rotation = Quaternion.identity;
            rect.localScale = Vector3.one * Mathf.Lerp(targetScale, endScale, t);

            Color color = baseColor;
            color.a = 1f - t;
            text.color = color;
            yield return null;
        }

        text.gameObject.SetActive(false);
    }

    private string ResolveDisplayText(WorldTextRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.text))
        {
            return request.text;
        }

        if (request.type == WorldTextType.Missed)
        {
            return "Missed";
        }

        if (request.amount <= 0f)
        {
            return string.Empty;
        }

        int rounded = Mathf.CeilToInt(request.amount);
        return request.type == WorldTextType.Heal ? $"+{rounded}" : rounded.ToString();
    }

    private Color ResolveColor(WorldTextRequest request, WorldTextStyle style)
    {
        if (style.useElementColor)
        {
            ElementDatabase database = ElementDatabase.Load();
            if (database != null)
            {
                Color elementColor = database.GetDamageColor(request.elementType);
                elementColor.a = style.color.a;
                return elementColor;
            }
        }

        return style.color;
    }

    private float ResolveScale(WorldTextRequest request, WorldTextStyle style)
    {
        float scale = Mathf.Max(0.01f, style.scaleMultiplier);
        scale *= request.impactTier switch
        {
            DamageImpactTier.Medium => mediumTierScale,
            DamageImpactTier.Heavy => heavyTierScale,
            DamageImpactTier.Ultimate => ultimateTierScale,
            _ => lowTierScale
        };

        if (request.damageRoutine == DamageRoutine.Elemental || request.elementType != ElementType.None)
        {
            scale *= Mathf.Max(0.01f, elementalDamageScaleMultiplier);
        }

        if (request.isCritical)
        {
            scale *= Mathf.Max(0.01f, criticalScaleMultiplier);
        }

        return scale;
    }

    private WorldTextStyle GetStyle(WorldTextType type)
    {
        return type switch
        {
            WorldTextType.Dot => dotStyle,
            WorldTextType.Shield => shieldStyle,
            WorldTextType.Missed => missedStyle,
            WorldTextType.Heal => healStyle,
            WorldTextType.Additional => additionalStyle,
            _ => damageStyle
        };
    }

    private Vector3 ResolveRandomOffset()
    {
        return new Vector3(
            Random.Range(randomRangeX.x, randomRangeX.y),
            Random.Range(randomRangeY.x, randomRangeY.y),
            0f);
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
