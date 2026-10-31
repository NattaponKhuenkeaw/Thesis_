using UnityEngine;
using UnityEngine.UI;

/// <summary>Canvas-native opportunity outline. Gameplay owns eligibility and expiry.</summary>
[DisallowMultipleComponent]
public sealed class TimedOpportunityGraphic : MaskableGraphic
{
    [Header("Opportunity Outline (starting values)")]
    [SerializeField, Min(0.1f)] private float pulseSeconds = 1.35f;
    [SerializeField, Min(1f)] private float expandedScale = 1.85f;
    [SerializeField, Min(0f)] private float padding = 6f;
    [SerializeField, Min(0.1f)] private float lineWidth = 3.5f;
    [SerializeField, Min(0f)] private float glowWidth = 14f;
    [Tooltip("Maximum soft glow overlap at the art edge, as a fraction of the smaller icon dimension.")]
    [SerializeField, Range(0f, 0.1f)] private float innerSpillFraction = 0.025f;
    [SerializeField] private Color outlineColor = new Color(0.82f, 0.97f, 1f, 1f);
    [SerializeField] private Color glowColor = new Color(0.24f, 0.78f, 1f, 1f);

    private bool _visible;
    private bool _reduceMotion;
    private bool _paused;
    private float _phase;

    public bool IsVisible => _visible;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        // The opportunity outline extends beyond the icon's authored sprite mask.
        maskable = false;
    }

    public static TimedOpportunityGraphic GetOrCreate(Image anchor)
    {
        if (anchor == null) return null;
        var existing = anchor.GetComponentInChildren<TimedOpportunityGraphic>(true);
        if (existing != null) return existing;
        var host = new GameObject("Timed Opportunity Outline", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(LayoutElement), typeof(TimedOpportunityGraphic));
        host.layer = anchor.gameObject.layer;
        host.transform.SetParent(anchor.transform, false);
        host.GetComponent<LayoutElement>().ignoreLayout = true;
        var graphic = host.GetComponent<TimedOpportunityGraphic>();
        var rect = graphic.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        graphic.raycastTarget = false;
        graphic.maskable = false;
        graphic.Clear();
        return graphic;
    }

    public void SetPresentation(bool visible, bool reduceMotion, bool paused)
    {
        bool changed = _visible != visible || _reduceMotion != reduceMotion;
        if (visible && !_visible) _phase = 0f;
        _visible = visible;
        _reduceMotion = reduceMotion;
        _paused = paused;
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        if (changed) SetVerticesDirty();
    }

    public void Clear() => SetPresentation(false, _reduceMotion, false);

    private void Update()
    {
        if (!_visible || _reduceMotion || _paused || Time.timeScale <= 0f) return;
        _phase = Mathf.Repeat(_phase + Time.unscaledDeltaTime / Mathf.Max(0.1f, pulseSeconds), 1f);
        SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        _visible = false;
        _phase = 0f;
        base.OnDisable();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!_visible) return;
        Rect bounds = rectTransform.rect;
        if (bounds.width <= 0f || bounds.height <= 0f) return;
        Vector2 halfSize = bounds.size * 0.5f + Vector2.one * padding;
        // Bound the inward feather against the artwork, independently of the
        // outer glow width. Larger impact must not wash across the icon center.
        float spill = Mathf.Min(bounds.width, bounds.height) * innerSpillFraction;
        Vector2 minimumInner = Vector2.Max(Vector2.zero, bounds.size * 0.5f - Vector2.one * spill);
        AddOutline(vh, bounds.center, halfSize, minimumInner, outlineColor.a);
        if (_reduceMotion) return;
        // Stagger two slow waves so the larger silhouette stays apparent between
        // pulses without adding a filled highlight over the artwork.
        for (int wave = 0; wave < 2; wave++)
        {
            float phase = Mathf.Repeat(_phase + wave * 0.5f, 1f);
            float eased = 1f - (1f - phase) * (1f - phase);
            AddOutline(vh, bounds.center, halfSize * Mathf.Lerp(1f, expandedScale, eased),
                minimumInner, 0.85f * (1f - phase) * Mathf.Clamp01(phase / 0.12f));
        }
    }

    private void AddOutline(VertexHelper vh, Vector2 center, Vector2 halfSize,
        Vector2 minimumInner, float alpha)
    {
        float halfLine = lineWidth * 0.5f;
        Vector2 coreInner = Vector2.Max(halfSize - Vector2.one * halfLine, minimumInner);
        Vector2 coreOuter = Vector2.Max(halfSize + Vector2.one * halfLine, coreInner);
        AddBand(vh, center, Vector2.Max(halfSize - Vector2.one * (halfLine + glowWidth), minimumInner),
            coreInner, 0f, alpha * 0.28f, glowColor);
        AddBand(vh, center, coreInner, coreOuter, alpha, alpha, outlineColor);
        // A steeper falloff near the line keeps the large halo soft instead of
        // making all fourteen units read as an opaque border.
        Vector2 nearHalo = coreOuter + Vector2.one * (glowWidth * 0.2f);
        Vector2 farHalo = coreOuter + Vector2.one * (glowWidth * 0.55f);
        AddBand(vh, center, coreOuter,
            nearHalo, alpha * 0.65f, alpha * 0.22f, glowColor);
        AddBand(vh, center, nearHalo, farHalo, alpha * 0.22f, alpha * 0.05f, glowColor);
        AddBand(vh, center, farHalo, coreOuter + Vector2.one * glowWidth,
            alpha * 0.05f, 0f, glowColor);
    }

    private void AddBand(VertexHelper vh, Vector2 center, Vector2 inner, Vector2 outer,
        float innerAlpha, float outerAlpha, Color tint)
    {
        inner.x = Mathf.Max(0f, inner.x);
        inner.y = Mathf.Max(0f, inner.y);
        int start = vh.currentVertCount;
        for (int corner = 0; corner < 4; corner++)
        {
            float x = corner == 0 || corner == 3 ? -1f : 1f;
            float y = corner < 2 ? -1f : 1f;
            AddVertex(vh, center + new Vector2(inner.x * x, inner.y * y), innerAlpha, tint);
            AddVertex(vh, center + new Vector2(outer.x * x, outer.y * y), outerAlpha, tint);
        }
        for (int side = 0; side < 4; side++)
        {
            int a = start + side * 2;
            int b = start + ((side + 1) % 4) * 2;
            vh.AddTriangle(a, b, a + 1);
            vh.AddTriangle(a + 1, b, b + 1);
        }
    }

    private void AddVertex(VertexHelper vh, Vector2 position, float alpha, Color tint)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position;
        tint *= color;
        tint.a *= alpha;
        vertex.color = tint;
        vh.AddVert(vertex);
    }
}
