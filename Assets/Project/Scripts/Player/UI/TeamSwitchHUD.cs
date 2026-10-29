using UnityEngine;
using System.Collections.Generic;

public class TeamSwitchHUD : MonoBehaviour
{
    [Header("Dependencies")]
    public TeamLoader teamLoader;
    public PlayerStateMachine stateMachine;

    [Header("UI Configuration")]
    public Transform slotContainer;
    public GameObject slotPrefab;

    private List<CharacterSwitchSlot> activeSlots = new List<CharacterSwitchSlot>();

    void Start()
    {
        CacheDependencies();
        InitializeHUD();
    }

    void OnEnable()
    {
        CacheDependencies();
        BindTeamLoader();
    }

    void OnDisable()
    {
        UnbindTeamLoader();
    }

    void Update()
    {
        if (teamLoader != null)
        {
            RefreshAllSlotCooldown();
        }
    }

    private void CacheDependencies()
    {
        if (teamLoader == null)
            teamLoader = Object.FindFirstObjectByType<TeamLoader>();

        if (stateMachine == null)
            stateMachine = Object.FindFirstObjectByType<PlayerStateMachine>();

        if (slotContainer == null)
            slotContainer = FindSlotContainer();
    }

    private void BindTeamLoader()
    {
        if (teamLoader == null)
            return;

        teamLoader.OnTeamChanged -= HandleTeamChanged;
        teamLoader.OnCurrentCharacterChanged -= HandleCurrentCharacterChanged;
        teamLoader.OnCharacterHealthUpdated -= HandleCharacterHealthUpdated;
        teamLoader.OnCharacterAuraEnergyUpdated -= HandleCharacterAuraEnergyUpdated;

        teamLoader.OnTeamChanged += HandleTeamChanged;
        teamLoader.OnCurrentCharacterChanged += HandleCurrentCharacterChanged;
        teamLoader.OnCharacterHealthUpdated += HandleCharacterHealthUpdated;
        teamLoader.OnCharacterAuraEnergyUpdated += HandleCharacterAuraEnergyUpdated;
    }

    private void UnbindTeamLoader()
    {
        if (teamLoader == null)
            return;

        teamLoader.OnTeamChanged -= HandleTeamChanged;
        teamLoader.OnCurrentCharacterChanged -= HandleCurrentCharacterChanged;
        teamLoader.OnCharacterHealthUpdated -= HandleCharacterHealthUpdated;
        teamLoader.OnCharacterAuraEnergyUpdated -= HandleCharacterAuraEnergyUpdated;
    }

    private void HandleTeamChanged()
    {
        if (teamLoader != null && activeSlots.Count == teamLoader.CharacterCount)
        {
            bool sameRoster = true;
            foreach (CharacterSwitchSlot slot in activeSlots)
                if (slot == null || teamLoader.GetCharacterAtIndex(slot.SlotIndex) != slot.BoundCharacter)
                { sameRoster = false; break; }
            if (sameRoster)
            {
                foreach (CharacterSwitchSlot slot in activeSlots) slot.RefreshFromHUD();
                UpdateActiveIndicator(teamLoader.CurrentIndex);
                return;
            }
        }
        InitializeHUD();
    }

    private void HandleCurrentCharacterChanged()
    {
        if (teamLoader == null)
            return;

        RefreshAllSlotHealth();
        UpdateActiveIndicator(teamLoader.CurrentIndex);
    }

    private void HandleCharacterHealthUpdated(CharacterObject character, float currentHp, float maxHp)
    {
        if (character == null)
            return;

        for (int i = 0; i < activeSlots.Count; i++)
        {
            CharacterSwitchSlot slot = activeSlots[i];
            if (slot == null || slot.BoundCharacter != character)
                continue;

            slot.RefreshHealth(currentHp, maxHp);
            return;
        }
    }

    private void HandleCharacterAuraEnergyUpdated(CharacterObject character, float currentAE, float maxAE)
    {
        if (character == null)
            return;

        for (int i = 0; i < activeSlots.Count; i++)
        {
            CharacterSwitchSlot slot = activeSlots[i];
            if (slot == null || slot.BoundCharacter != character)
                continue;

            slot.RefreshEnergy(currentAE, maxAE);
            return;
        }
    }

    // เปลี่ยนให้เป็น public เผื่อในอนาคตอยากสั่ง Refresh จากสคริปต์อื่นโดยตรง
    public void InitializeHUD()
    {
        CharacterObject[] teamSnapshot = teamLoader != null ? teamLoader.GetTeamSnapshot() : null;
        if (teamLoader == null || teamSnapshot == null || slotContainer == null || slotPrefab == null) return;

        foreach (Transform child in slotContainer)
        {
            Destroy(child.gameObject);
        }
        activeSlots.Clear();

        for (int i = 0; i < teamSnapshot.Length; i++)
        {
            CharacterObject character = teamSnapshot[i];

            if (character == null) continue;

            GameObject newSlotObj = Instantiate(slotPrefab, slotContainer);
            CharacterSwitchSlot slotUI = newSlotObj.GetComponent<CharacterSwitchSlot>();

            if (slotUI != null)
            {
                slotUI.Setup(character, i, this);
                activeSlots.Add(slotUI);
            }
        }

        RefreshAllSlotHealth();
        UpdateActiveIndicator(teamLoader.CurrentIndex);
    }

    // รับคำสั่งเมื่อคลิก UI สลับตัว
    public void OnSlotClicked(int index)
    {
        if (teamLoader == null) return;

        if (!teamLoader.CanSwitchToIndex(index, ignoreCooldown: true))
        {
            Debug.Log("ไม่สามารถเปลี่ยนไปยังตัวละครนี้ได้ (ตาย / cooldown / ตัวเดิม)!");
            return;
        }

        // เช็คกฎเปลี่ยนตัว
        if (stateMachine != null && !stateMachine.CanSwitchCharacterTo(index))
        {
            Debug.Log("ไม่สามารถเปลี่ยนตัวได้ในจังหวะนี้!");
            return;
        }

        if (teamLoader.SwitchToIndex(index))
            UpdateActiveIndicator(index);
    }

    public bool TryGetCharacterHealth(CharacterObject character, out float currentHp, out float maxHp)
    {
        currentHp = 0f;
        maxHp = 0f;

        if (teamLoader == null || character == null)
            return false;

        maxHp = teamLoader.GetCharacterMaxHP(character);
        currentHp = teamLoader.GetCharacterCurrentHP(character);
        return maxHp > 0f;
    }

    private Transform FindSlotContainer()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null)
                continue;

            Transform teamSwitchPanel = FindChildRecursive(canvases[i].transform, "TeamSwitchPanel");
            if (teamSwitchPanel != null)
                return teamSwitchPanel;
        }

        return null;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        if (parent.name == childName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void UpdateActiveIndicator(int activeIndex)
    {
        for (int i = 0; i < activeSlots.Count; i++)
        {
            bool isCurrentActive = activeSlots[i] != null && activeSlots[i].SlotIndex == activeIndex;
            activeSlots[i].SetActiveStatus(isCurrentActive);
        }
    }

    private void RefreshAllSlotHealth()
    {
        for (int i = 0; i < activeSlots.Count; i++)
        {
            if (activeSlots[i] == null)
                continue;

            activeSlots[i].RefreshFromHUD();
        }
    }

    private void RefreshAllSlotCooldown()
    {
        float duration = teamLoader.switchCooldownDuration;
        float remaining = teamLoader.RemainingCooldown;

        for (int i = 0; i < activeSlots.Count; i++)
        {
            if (activeSlots[i] == null)
                continue;

            activeSlots[i].RefreshCooldown(remaining, duration);
        }
    }
}
