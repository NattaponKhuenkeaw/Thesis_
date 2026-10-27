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
    public GameObject unavailableOverlay;

    [Header("Extra Overlays")]
    public Image aeOverlay;
    public Image cooldownOverlay;
    public Image followUpOverlay;
    [SerializeField] private GameObject aeGaugeVisual;

    [Header("QTE Ready Prompt")]
    public Image qteReadyOverlay;
    public Image qteWindowFill;
    private RectTransform qtePulseRect;

    private string lastCooldownText = string.Empty;
    private bool isCooldownVisible;

    private int slotIndex;
    private TeamSwitchHUD hudManager;
    private CharacterObject boundCharacter;

    public int SlotIndex => slotIndex;
    public CharacterObject BoundCharacter => boundCharacter;

    public void Setup(CharacterObject character, int index, TeamSwitchHUD manager)
    {
        slotIndex = index;
        hudManager = manager;
        boundCharacter = character;

        // เปลี่ยนตัวเลขปุ่มกดอัตโนมัติ (index 0 จะแสดงเลข 1, index 1 แสดงเลข 2...)
        if (hotkeyText != null)
        {
            hotkeyText.text = (index + 1).ToString();
        }

        // อัปเดตรูปไอคอนจาก MetaData
        if (boundCharacter != null && boundCharacter.metaData != null)
        {
             characterIcon.sprite = boundCharacter.metaData.portrait; 
        }

        if (hpSlider == null)
            hpSlider = GetComponentInChildren<Slider>(true);

        RefreshCooldown(0f, 0f);
        EnsureQtePrompt();
        RefreshFromHUD();
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
        if (isCooldownVisible == visible)
            return;

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

        // 4. Switch Cooldown Visuals (unavailable panel & feedback)
        PlayerStateMachine stateMachine = hudManager != null ? hudManager.stateMachine : null;
        bool isDead = teamLoader != null && !teamLoader.IsCharacterAlive(boundCharacter);
        bool isSwitchUnavailable = isDead;
        if (!isDead)
        {
            if (stateMachine != null)
            {
                isSwitchUnavailable = !stateMachine.CanSwitchCharacterTo(slotIndex);
            }
            else
            {
                isSwitchUnavailable = teamLoader != null && teamLoader.IsSwitchOnCooldown;
            }
        }

        if (unavailableOverlay != null)
        {
            unavailableOverlay.SetActive(!isCurrent && isSwitchUnavailable);
        }
    }

    private void EnsureQtePrompt()
    {
        if (characterIcon == null) return;
        if (qteReadyOverlay == null)
        {
            var overlay = new GameObject("QTE Ready Overlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(characterIcon.transform, false);
            qteReadyOverlay = overlay.GetComponent<Image>();
            qteReadyOverlay.raycastTarget = false;
            RectTransform rect = qteReadyOverlay.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        qtePulseRect = qteReadyOverlay.rectTransform;
        if (qteWindowFill == null)
        {
            var timer = new GameObject("QTE Window Remaining", typeof(RectTransform), typeof(Image));
            timer.transform.SetParent(characterIcon.transform, false);
            qteWindowFill = timer.GetComponent<Image>();
            qteWindowFill.raycastTarget = false;
            RectTransform rect = qteWindowFill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 3f);
            rect.anchoredPosition = Vector2.zero;
            qteWindowFill.color = Color.white;
        }
        qteReadyOverlay.gameObject.SetActive(false);
        qteWindowFill.gameObject.SetActive(false);
    }

    private void RefreshQtePrompt(TeamLoader loader, bool isCurrent)
    {
        QteTeamSwapController qte = loader.QteSwaps;
        float remaining = 0f;
        float total = 0f;
        bool ready = !isCurrent && qte != null && qte.TryGetWindow(boundCharacter, out remaining, out total);
        bool actionable = ready && qte.CanHandoffTo(slotIndex);
        if (qteReadyOverlay != null)
        {
            qteReadyOverlay.gameObject.SetActive(actionable);
            if (actionable)
            {
                float pulse = qte.reduceUiMotion ? 0.5f : (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / 0.8f)) + 1f) * 0.5f;
                qteReadyOverlay.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 0.4f, pulse));
                if (qtePulseRect != null) qtePulseRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.1f, pulse);
            }
        }
        if (qteWindowFill != null)
        {
            qteWindowFill.gameObject.SetActive(ready);
            if (ready) qteWindowFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(remaining / total), 0f);
        }
    }

    private void OnDisable()
    {
        if (qteReadyOverlay != null) qteReadyOverlay.gameObject.SetActive(false);
        if (qteWindowFill != null) qteWindowFill.gameObject.SetActive(false);
        if (qtePulseRect != null) qtePulseRect.localScale = Vector3.one;
    }

    private static string FormatCooldownText(float seconds)
    {
        return seconds < 1f
            ? seconds.ToString("0.0")
            : Mathf.CeilToInt(seconds).ToString();
    }
}
