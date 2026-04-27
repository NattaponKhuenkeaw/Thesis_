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

    private int lastCooldownSecond = -1;
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

            int currentSecond = Mathf.CeilToInt(remainingCooldown);
            if (currentSecond != lastCooldownSecond)
            {
                cooldownText.text = currentSecond.ToString();
                lastCooldownSecond = currentSecond;
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
            lastCooldownSecond = -1;
    }
}
