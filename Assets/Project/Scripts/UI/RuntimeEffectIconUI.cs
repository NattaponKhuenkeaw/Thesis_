using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeEffectIconUI : MonoBehaviour
{
    private static readonly string[] StackTextNames = { "QuantityText", "StackText", "AmountText", "Amount", "CountText" };

    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;

    [NonSerialized] private RuntimeEffectInstance boundEffect;
    private int boundStackCount = 1;
    private bool referencesResolved;
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
            iconImage.sprite = RuntimeEffectRuntimeUtility.GetIcon(effect);
            iconImage.enabled = iconImage.sprite != null;
        }

        RefreshPresentation();
    }

    private void Update() { if (RequiresTimerRefresh) RefreshPresentation(); }

    public void RefreshPresentation()
    {
        if (boundEffect == null || boundEffect.definition == null)
        {
            RequiresTimerRefresh = false;

            if (stackText != null)
            {
                stackText.gameObject.SetActive(false);
                stackText.text = string.Empty;
            }

            return;
        }

        bool showStackCount = boundStackCount > 1;

        float remaining = 0f;
        bool showTime = boundEffect.recipe?.presentation?.showRemainingTime == true;
        bool hasModeTime = TeamLoader.Instance?.KitRuntimes?.Find(boundEffect.sourceCharacter)
            ?.TryGetEffectRemaining(boundEffect, out remaining) == true;
        if (!hasModeTime && boundEffect.UsesTimer) remaining = boundEffect.remainingSeconds;
        showTime &= hasModeTime || boundEffect.UsesTimer;
        RequiresTimerRefresh = showTime;
        if (stackText != null)
        {
            stackText.gameObject.SetActive(showStackCount || showTime);
            string stacks = showStackCount ? $"x{boundStackCount}" : string.Empty;
            string timing = showTime ? Mathf.Max(0, Mathf.CeilToInt(remaining)).ToString() + "s" : string.Empty;
            stackText.text = showStackCount && showTime ? stacks + " " + timing : stacks + timing;
        }
    }

    private void ResolveReferences()
    {
        if (referencesResolved)
        {
            return;
        }

        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }

        TMP_Text namedStackText = FindTextByNames(StackTextNames);
        if (namedStackText != null)
        {
            stackText = namedStackText;
        }
        else if (stackText == null)
        {
            stackText = FindStackTextFallback();
        }

        referencesResolved = true;
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

    private TMP_Text FindStackTextFallback()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.name.Contains("(1)") || candidate.name.Contains("Quantity"))
            {
                return candidate;
            }
        }

        return texts.Length > 0 ? texts[texts.Length - 1] : null;
    }
}
