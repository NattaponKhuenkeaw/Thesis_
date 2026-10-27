using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Reads the same per-character counter used by resolver availability and commit costs.
public class CombatMeterUI : MonoBehaviour
{
    [SerializeField] private Vector2 boxSize = new Vector2(72f, 16f);
    [SerializeField] private float boxSpacing = 6f;
    [SerializeField] private float readyPulseSeconds = 0.4f;
    [SerializeField] private float readyPulseScale = 1.7f;
    private readonly List<Image> fills = new List<Image>();
    private readonly List<Image> overlays = new List<Image>();
    private readonly List<float> pulseStarts = new List<float>();
    private PlayerCharacter player;
    private CharacterObject displayedCharacter;
    private int previousReady;

    public void Bind(PlayerCharacter source) => player = source;

    private void LateUpdate()
    {
        CharacterPassiveController passive = player != null ? player.PassiveController : null;
        CombatMeterDefinition meter = passive?.CombatMeter;
        int count = meter != null && meter.IsConfigured ? passive.GetMeterBoxCount() : 0;
        bool characterChanged = displayedCharacter != player?.characterObject;
        if (count != fills.Count || characterChanged)
        {
            Rebuild(count);
            displayedCharacter = player != null ? player.characterObject : null;
            previousReady = count > 0 ? passive.RuntimeState.GetCounter(meter.counterId) / Mathf.Max(1, meter.pointsPerBox) : 0;
        }
        if (count == 0) return;
        int quota = Mathf.Max(1, meter.pointsPerBox);
        int points = Mathf.Clamp(passive.RuntimeState.GetCounter(meter.counterId), 0, count * quota);
        int ready = points / quota;
        for (int i = 0; i < count; i++)
        {
            float amount = Mathf.Clamp01((points - i * quota) / (float)quota);
            fills[i].enabled = amount > 0f;
            fills[i].rectTransform.anchorMax = new Vector2(amount, 1f);
            fills[i].color = meter.fillColor;
            if (i >= previousReady && i < ready) pulseStarts[i] = Time.unscaledTime;
            float t = Mathf.Clamp01((Time.unscaledTime - pulseStarts[i]) / Mathf.Max(0.01f, readyPulseSeconds));
            float ease = 1f - (1f - t) * (1f - t);
            overlays[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, readyPulseScale, ease);
            overlays[i].color = new Color(1f, 1f, 1f, 1f - ease);
        }
        previousReady = ready;
    }

    private void Rebuild(int count)
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        fills.Clear(); overlays.Clear(); pulseStarts.Clear();
        for (int i = 0; i < count; i++)
        {
            Image box = CreateImage("Box " + (i + 1), transform);
            box.color = new Color(0.12f, 0.14f, 0.18f, 0.9f);
            RectTransform rect = box.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = boxSize;
            rect.anchoredPosition = new Vector2(i * (boxSize.x + boxSpacing), 0f);
            Image fill = CreateImage("Fill", rect);
            Stretch(fill.rectTransform, 1f);
            fills.Add(fill);
            Image overlay = CreateImage("Ready Overlay", rect);
            Stretch(overlay.rectTransform, 0f);
            overlay.color = Color.clear;
            overlays.Add(overlay);
            pulseStarts.Add(float.NegativeInfinity);
        }
        ((RectTransform)transform).sizeDelta = new Vector2(Mathf.Max(0f, count * (boxSize.x + boxSpacing) - boxSpacing), boxSize.y);
    }

    private static Image CreateImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = -Vector2.one * inset;
    }
}
