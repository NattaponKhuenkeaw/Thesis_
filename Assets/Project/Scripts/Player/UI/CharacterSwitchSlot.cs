using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class CharacterSwitchSlot : MonoBehaviour
{
    [Header("UI References")]
    public Image characterIcon;        
    public GameObject activeIndicator; 
    public TMP_Text hotkeyText;        

    [Header("Health Bar")]
    public Slider hpSlider;
    public Image hpFill;               

    [Header("Switch Cooldown UI")]
    public Image cooldownFill;
    public TMP_Text cooldownText;
    public CanvasGroup inactiveCanvasGroup;
    [Tooltip("Swap cooldown opacity, preserved separately from the live ready alpha.")]
    [Range(0f, 1f)] public float inactiveAlpha = 0.34f;
    private bool _presentationCached;

    [Header("Extra Overlays")]
    public Image aeOverlay;
    public Image cooldownOverlay;
    public Image followUpOverlay;
    [SerializeField] private GameObject aeGaugeVisual;

    [Header("QTE Ready Prompt")]
    public TimedOpportunityGraphic qteWindowEffect;

    [Header("Portrait Exchange (starting values)")]
    [SerializeField, Min(0f)] private float exitSeconds = 0.12f;
    [SerializeField, Min(0f)] private float entrySeconds = 0.18f;
    [SerializeField, Min(0f)] private float slideWidth = 0.6f;
    private Image _outgoingPortrait;
    private RectTransform _portraitRect;
    private Vector2 _portraitRest;
    private Color _portraitColor;
    private int _exchangePhase;
    private float _exchangeElapsed;

    private string lastCooldownText = string.Empty;
    private bool isCooldownVisible;
    private bool _cooldownVisibilityKnown;

    private int slotIndex;
    private TeamSwitchHUD hudManager;
    private CharacterObject boundCharacter;

    public int SlotIndex => slotIndex;
    public CharacterObject BoundCharacter => boundCharacter;

    public void Setup(CharacterObject character, int index, TeamSwitchHUD manager, bool animate = false)
    {
        CachePresentation();
        bool exchange = animate && boundCharacter != null && boundCharacter != character
            && isActiveAndEnabled;
        Sprite outgoing = _exchangePhase == 1 && _outgoingPortrait != null
            ? _outgoingPortrait.sprite : characterIcon != null ? characterIcon.sprite : null;
        ResetPortraitMotion();
        if (qteWindowEffect != null) qteWindowEffect.Clear();
        slotIndex = index;
        hudManager = manager;
        boundCharacter = character;

        // The standby character is always reached with the shared swap key.
        if (hotkeyText != null)
        {
            hotkeyText.text = "1";
        }

        // อัปเดตรูปไอคอนจาก MetaData
        if (characterIcon != null && boundCharacter != null)
        {
            characterIcon.sprite = boundCharacter.metaData != null
                ? boundCharacter.metaData.icon != null
                    ? boundCharacter.metaData.icon
                    : boundCharacter.metaData.portrait
                : null;
        }

        if (hpSlider == null)
            hpSlider = GetComponentInChildren<Slider>(true);

        if (activeIndicator != null) activeIndicator.SetActive(false);
        RefreshFromHUD();
        TeamLoader loader = hudManager != null ? hudManager.teamLoader : null;
        RefreshCooldown(loader != null ? loader.RemainingCooldown : 0f,
            loader != null ? loader.switchCooldownDuration : 0f);
        RefreshAvailability(loader);
        if (exchange && !ReducedMotion && outgoing != null) BeginPortraitExchange(outgoing);
        if (loader != null) RefreshQtePrompt(loader, loader.CurrentCharacter == boundCharacter);
    }

    public void SetActiveStatus(bool isActive)
    {
        if (activeIndicator != null)
        {
            activeIndicator.SetActive(isActive);
        }
    }

    public void RefreshFromHUD()
    {
        if (boundCharacter != null && hudManager != null && (hpFill != null || hpSlider != null))
        {
            if (hudManager.TryGetCharacterHealth(boundCharacter, out float currentHP, out float maxHP) && maxHP > 0f)
                RefreshHealth(currentHP, maxHP);
            else
                RefreshHealth(0f, 0f);
        }

        RefreshEnergy();
    }

    public void RefreshHealth(float currentHP, float maxHP)
    {
        if (maxHP <= 0f)
        {
            if (hpSlider != null)
                hpSlider.value = 0f;

            if (hpFill != null)
                hpFill.fillAmount = 0f;

            return;
        }

        float normalized = Mathf.Clamp01(currentHP / maxHP);

        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
            hpSlider.value = normalized;
        }

        if (hpFill != null)
            hpFill.fillAmount = normalized;
    }

    public void RefreshEnergy(float? currentAE = null, float? maxAE = null)
    {
    }

    public void RefreshCooldown(float remainingCooldown, float totalCooldown)
    {
        if (totalCooldown <= 0f || remainingCooldown <= 0f)
        {
            SetCooldownVisible(false);
            return;
        }

        float normalized = Mathf.Clamp01(remainingCooldown / totalCooldown);

        if (cooldownFill != null)
        {
            if (!isCooldownVisible)
                cooldownFill.gameObject.SetActive(true);
            cooldownFill.fillAmount = normalized;
        }

        if (cooldownText != null)
        {
            if (!isCooldownVisible)
                cooldownText.gameObject.SetActive(true);

            string currentText = FormatCooldownText(remainingCooldown);
            if (currentText != lastCooldownText)
            {
                cooldownText.text = currentText;
                lastCooldownText = currentText;
            }
        }

        isCooldownVisible = true;
    }

    private void SetCooldownVisible(bool visible)
    {
        if (_cooldownVisibilityKnown && isCooldownVisible == visible)
            return;
        _cooldownVisibilityKnown = true;

        if (cooldownFill != null)
            cooldownFill.gameObject.SetActive(visible);

        if (cooldownText != null)
            cooldownText.gameObject.SetActive(visible);

        isCooldownVisible = visible;

        if (!visible)
            lastCooldownText = string.Empty;
    }

    private void Update()
    {
        TickPortraitExchange(Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f);
        if (boundCharacter == null) return;

        TeamLoader teamLoader = hudManager != null ? hudManager.teamLoader : null;
        if (teamLoader == null) return;

        bool isCurrent = teamLoader.CurrentCharacter == boundCharacter;
        RefreshQtePrompt(teamLoader, isCurrent);

        // 1. aeGaugeVisual and aeOverlay (0 -> 1)
        if (aeGaugeVisual != null)
        {
            aeGaugeVisual.SetActive(!isCurrent);
        }

        if (aeOverlay != null)
        {
            if (isCurrent && aeGaugeVisual == null)
            {
                aeOverlay.gameObject.SetActive(false);
            }
            else
            {
                if (aeGaugeVisual == null)
                {
                    aeOverlay.gameObject.SetActive(true);
                }
                
                if (!isCurrent)
                {
                    float currentAE = teamLoader.GetCharacterCurrentAE(boundCharacter);
                    float maxAE = teamLoader.GetCharacterMaxAE(boundCharacter);
                    aeOverlay.fillAmount = maxAE > 0f ? Mathf.Clamp01(currentAE / maxAE) : 0f;
                }
            }
        }

        // 2. cooldownOverlay (1 -> 0) FOR ULTIMATE COOLDOWN
        if (cooldownOverlay != null)
        {
            float ultRemaining = 0f;
            float ultDuration = 0f;
            if (boundCharacter.actionResolverTable != null && boundCharacter.actionResolverTable.ultimateActions != null && boundCharacter.actionResolverTable.ultimateActions.Count > 0)
            {
                var ultAction = boundCharacter.actionResolverTable.ultimateActions[0];
                ultDuration = ultAction.cooldown;
                if (PlayerCharacter.Instance != null && PlayerCharacter.Instance.Combat != null)
                {
                    ultRemaining = PlayerCharacter.Instance.Combat.GetRemainingCooldownForCharacter(boundCharacter, AttackCategory.Ultimate, ultDuration, ultAction.actionName, ultAction.actionId);
                }
            }
            
            if (ultDuration > 0f && ultRemaining > 0f)
            {
                cooldownOverlay.fillAmount = Mathf.Clamp01(ultRemaining / ultDuration);
            }
            else
            {
                cooldownOverlay.fillAmount = 0f;
            }
        }

        // 3. followUpOverlay (0 -> 1 representing time window remaining ratio)
        if (followUpOverlay != null)
        {
            if (teamLoader.TryGetCharacterUltimateFollowUpWindow(boundCharacter, out float remaining, out float total) && total > 0f)
            {
                if (!followUpOverlay.gameObject.activeSelf)
                {
                    followUpOverlay.gameObject.SetActive(true);
                }
                followUpOverlay.fillAmount = Mathf.Clamp01(remaining / total);
            }
            else
            {
                if (followUpOverlay.gameObject.activeSelf)
                {
                    followUpOverlay.gameObject.SetActive(false);
                }
                followUpOverlay.fillAmount = 0f;
            }
        }

        RefreshAvailability(teamLoader);
    }

    private bool ReducedMotion => hudManager != null && hudManager.teamLoader != null
        && hudManager.teamLoader.QteSwaps != null && hudManager.teamLoader.QteSwaps.reduceUiMotion;

    private void CachePresentation()
    {
        if (_presentationCached) return;
        if (inactiveCanvasGroup == null)
        {
            Transform group = transform.Find("InactiveCanvasGroup");
            if (group != null) inactiveCanvasGroup = group.GetComponent<CanvasGroup>();
        }
        if (inactiveCanvasGroup != null)
        {
            inactiveCanvasGroup.gameObject.SetActive(true);
        }
        if (characterIcon != null)
        {
            _portraitRect = characterIcon.rectTransform;
            _portraitRest = _portraitRect.anchoredPosition;
            _portraitColor = characterIcon.color;
            qteWindowEffect = TimedOpportunityGraphic.GetOrCreate(characterIcon);
        }
        _presentationCached = true;
    }

    private void RefreshAvailability(TeamLoader loader)
    {
        if (inactiveCanvasGroup == null || loader == null) return;
        // Keep the authored group alive: toggling it hides the portrait and its stats.
        inactiveCanvasGroup.gameObject.SetActive(true);
        // Opacity communicates only the shared swap cooldown. Other switch
        // restrictions still block input, but do not dim the standby portrait.
        inactiveCanvasGroup.alpha = loader.IsSwitchOnCooldown ? inactiveAlpha : 1f;
    }

    private void RefreshQtePrompt(TeamLoader loader, bool isCurrent)
    {
        bool ready = !isCurrent && loader.QteSwaps != null
            && loader.QteSwaps.TryGetWindow(boundCharacter, out _, out _);
        if (qteWindowEffect != null)
            qteWindowEffect.SetPresentation(ready, ReducedMotion, Time.timeScale <= 0f);
    }

    private void BeginPortraitExchange(Sprite outgoing)
    {
        if (_portraitRect == null) return;
        if (_outgoingPortrait == null)
        {
            var host = new GameObject("Outgoing Portrait", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            host.layer = characterIcon.gameObject.layer;
            host.transform.SetParent(characterIcon.transform.parent, false);
            host.GetComponent<LayoutElement>().ignoreLayout = true;
            _outgoingPortrait = host.GetComponent<Image>();
            _outgoingPortrait.raycastTarget = false;
            _outgoingPortrait.preserveAspect = characterIcon.preserveAspect;
            _outgoingPortrait.material = characterIcon.material;
        }
        RectTransform rect = _outgoingPortrait.rectTransform;
        rect.anchorMin = _portraitRect.anchorMin;
        rect.anchorMax = _portraitRect.anchorMax;
        rect.pivot = _portraitRect.pivot;
        rect.sizeDelta = _portraitRect.sizeDelta;
        rect.anchoredPosition = _portraitRest;
        rect.localScale = _portraitRect.localScale;
        rect.localRotation = _portraitRect.localRotation;
        _outgoingPortrait.sprite = outgoing;
        _outgoingPortrait.color = _portraitColor;
        _outgoingPortrait.gameObject.SetActive(true);
        SetPortraitAlpha(characterIcon, 0f);
        _exchangePhase = 1;
        _exchangeElapsed = 0f;
    }

    private void TickPortraitExchange(float delta)
    {
        if (_exchangePhase == 0 || _portraitRect == null) return;
        if (ReducedMotion) { ResetPortraitMotion(); return; }
        _exchangeElapsed += delta;
        float width = _portraitRect.rect.width * slideWidth;
        if (_exchangePhase == 1)
        {
            float t = exitSeconds > 0f ? Mathf.Clamp01(_exchangeElapsed / exitSeconds) : 1f;
            _outgoingPortrait.rectTransform.anchoredPosition = _portraitRest + Vector2.left * (width * t * t);
            SetPortraitAlpha(_outgoingPortrait, 1f - t);
            if (t < 1f) return;
            _outgoingPortrait.gameObject.SetActive(false);
            _exchangePhase = 2;
            _exchangeElapsed = Mathf.Max(0f, _exchangeElapsed - exitSeconds);
        }
        float entry = entrySeconds > 0f ? Mathf.Clamp01(_exchangeElapsed / entrySeconds) : 1f;
        float eased = 1f - (1f - entry) * (1f - entry);
        _portraitRect.anchoredPosition = _portraitRest + Vector2.right * (width * (1f - eased));
        SetPortraitAlpha(characterIcon, eased);
        if (entry >= 1f) ResetPortraitMotion();
    }

    private void SetPortraitAlpha(Image image, float ratio)
    {
        Color tint = _portraitColor;
        tint.a *= ratio;
        image.color = tint;
    }

    private void ResetPortraitMotion()
    {
        _exchangePhase = 0;
        _exchangeElapsed = 0f;
        if (_portraitRect != null) _portraitRect.anchoredPosition = _portraitRest;
        if (characterIcon != null && _presentationCached) characterIcon.color = _portraitColor;
        if (_outgoingPortrait != null) _outgoingPortrait.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (qteWindowEffect != null) qteWindowEffect.Clear();
        ResetPortraitMotion();
    }

    private static string FormatCooldownText(float seconds)
    {
        return seconds < 1f
            ? seconds.ToString("0.0")
            : Mathf.CeilToInt(seconds).ToString();
    }
}
