using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeEffectIconUI : MonoBehaviour
{
    private static readonly string[] StackTextNames = { "QuantityText", "StackText", "AmountText", "Amount", "CountText" };

    [SerializeField] private Image iconImage;
    [SerializeField] private Image durationFill;
    [SerializeField] private TMP_Text stackText;

    private RuntimeEffectInstance boundEffect;
    private int boundStackCount = 1;
    public bool RequiresTimerRefresh { get; private set; }

    private void Awake()
    {
        ResolveReferences();
    }

    public void Bind(RuntimeEffectInstance effect)
    {
        Bind(effect, 1);
    }

    public void Bind(RuntimeEffectInstance effect, int stackCount)
    {
        boundEffect = effect;
        boundStackCount = Mathf.Max(1, stackCount);
        ResolveReferences();

        if (iconImage != null)
        {
            iconImage.sprite = effect != null && effect.definition != null ? effect.definition.icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        RefreshPresentation();
    }

    public void RefreshPresentation()
    {
        if (boundEffect == null || boundEffect.definition == null)
        {
            RequiresTimerRefresh = false;
            if (durationFill != null)
            {
                durationFill.gameObject.SetActive(false);
            }

            if (stackText != null)
            {
                stackText.gameObject.SetActive(false);
                stackText.text = string.Empty;
            }

            return;
        }

        float totalDuration = boundEffect.appliedDurationSeconds > 0f
            ? boundEffect.appliedDurationSeconds
            : boundEffect.definition.durationSeconds;
        bool timed = boundEffect.definition.durationType == RuntimeEffectDurationType.Seconds
            && totalDuration > 0f;
        bool showStackCount = boundEffect.definition.durationType == RuntimeEffectDurationType.Forever
            && boundStackCount > 1;
        float normalized = timed
            ? Mathf.Clamp01(boundEffect.remainingSeconds / totalDuration)
            : 0f;

        RequiresTimerRefresh = timed;

        if (durationFill != null)
        {
            durationFill.gameObject.SetActive(timed);
            durationFill.fillAmount = timed ? (1f - normalized) : 0f;
        }

        if (stackText != null)
        {
            stackText.gameObject.SetActive(showStackCount);
            stackText.text = showStackCount ? $"x{boundStackCount}" : string.Empty;
        }
    }

    private void ResolveReferences()
    {
        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }

        if (durationFill == null)
        {
            Transform fillTransform = transform.Find("DurationFill");
            durationFill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
        }

        if (stackText == null)
        {
            stackText = FindTextByNames(StackTextNames);
        }
    }

    private TMP_Text FindTextByNames(string[] names)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            string targetName = names[nameIndex];
            for (int textIndex = 0; textIndex < texts.Length; textIndex++)
            {
                TMP_Text candidate = texts[textIndex];
                if (candidate != null && candidate.name == targetName)
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
