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

    [Header("Extra Overlays")]
    public Image aeOverlay;
    public Image cooldownOverlay;
    public Image followUpOverlay;

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
            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = 0f;
            }
            return;
        }

        float normalized = Mathf.Clamp01(remainingCooldown / totalCooldown);

        if (cooldownFill != null)
        {
            if (!isCooldownVisible)
                cooldownFill.gameObject.SetActive(true);
            cooldownFill.fillAmount = normalized;
        }

        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = normalized;
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

        // 1. aeOverlay (0 -> 1)
        if (aeOverlay != null)
        {
            float currentAE = teamLoader.GetCharacterCurrentAE(boundCharacter);
            float maxAE = teamLoader.GetCharacterMaxAE(boundCharacter);
            aeOverlay.fillAmount = maxAE > 0f ? Mathf.Clamp01(currentAE / maxAE) : 0f;
        }

        // 2. cooldownOverlay (1 -> 0)
        if (cooldownOverlay != null)
        {
            bool isCurrent = teamLoader.CurrentCharacter == boundCharacter;
            if (!isCurrent && teamLoader.IsSwitchOnCooldown)
            {
                float remaining = teamLoader.RemainingCooldown;
                float duration = teamLoader.switchCooldownDuration;
                cooldownOverlay.fillAmount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
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
    }

    private static string FormatCooldownText(float seconds)
    {
        return seconds < 1f
            ? seconds.ToString("0.0")
            : Mathf.CeilToInt(seconds).ToString();
    }
}
