using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeEffectIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image durationFill;
    [SerializeField] private TMP_Text durationText;

    private RuntimeEffectInstance boundEffect;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        Refresh();
    }

    public void Bind(RuntimeEffectInstance effect)
    {
        boundEffect = effect;
        ResolveReferences();

        if (iconImage != null)
        {
            iconImage.sprite = effect != null && effect.definition != null ? effect.definition.icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (boundEffect == null || boundEffect.definition == null)
        {
            return;
        }

        bool timed = boundEffect.definition.durationType == RuntimeEffectDurationType.Seconds
            && boundEffect.definition.durationSeconds > 0f;

        if (durationFill != null)
        {
            durationFill.gameObject.SetActive(timed);
            durationFill.fillAmount = timed
                ? Mathf.Clamp01(boundEffect.remainingSeconds / boundEffect.definition.durationSeconds)
                : 0f;
        }

        if (durationText != null)
        {
            durationText.gameObject.SetActive(timed);
            durationText.text = timed ? Mathf.CeilToInt(boundEffect.remainingSeconds).ToString() : string.Empty;
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

        if (durationText == null)
        {
            durationText = GetComponentInChildren<TMP_Text>(true);
        }
    }
}
