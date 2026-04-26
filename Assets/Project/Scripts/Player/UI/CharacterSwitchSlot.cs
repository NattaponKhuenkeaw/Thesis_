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
    public Image hpFill;               

    private int slotIndex;
    private TeamSwitchHUD hudManager;
    private CharacterObject boundCharacter;

    public int SlotIndex => slotIndex;

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
    }

    public void SetActiveStatus(bool isActive)
    {
        if (activeIndicator != null)
        {
            activeIndicator.SetActive(isActive);
        }
    }

    private void Update()
    {
        if (boundCharacter != null && boundCharacter.stats != null && hpFill != null)
        {
            float maxHP = boundCharacter.stats.maxHP;

            // ⚠️ อย่าลืมแก้ให้ดึงค่าเลือดปัจจุบัน
            float currentHP = maxHP;

            if (maxHP > 0)
                hpFill.fillAmount = currentHP / maxHP;
            else
                hpFill.fillAmount = 0;
        }
    }
}
