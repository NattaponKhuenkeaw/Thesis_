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
    private CharacterObject[] _rosterSnapshot;

    void Start()
    {
        CacheDependencies();
        InitializeHUD();
    }

    void OnEnable()
    {
        CacheDependencies();
        BindTeamLoader();
        if (activeSlots.Count > 0) ReconcileSlots(false);
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
        if (teamLoader == null) return;
        CharacterObject[] roster = teamLoader.GetTeamSnapshot();
        bool sameRoster = _rosterSnapshot != null && roster.Length == _rosterSnapshot.Length;
        if (sameRoster)
            for (int i = 0; i < roster.Length; i++)
                if (roster[i] != _rosterSnapshot[i]) { sameRoster = false; break; }
        // Stat refreshes can arrive during SetCharacter, before the committed
        // current-character event. They must not silently consume the exchange.
        if (sameRoster)
        {
            RefreshAllSlotHealth();
            RefreshAllSlotCooldown();
        }
        else ReconcileSlots(false);
    }

    private void HandleCurrentCharacterChanged() => ReconcileSlots(true);

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
    public void InitializeHUD() => ReconcileSlots(false);

    private void ReconcileSlots(bool animate)
    {
        if (teamLoader == null || slotContainer == null || slotPrefab == null) return;
        CharacterObject[] roster = teamLoader.GetTeamSnapshot();
        if (roster == null) return;
        _rosterSnapshot = roster;
        int viewIndex = 0;
        for (int rosterIndex = 0; rosterIndex < roster.Length; rosterIndex++)
        {
            CharacterObject character = teamLoader.GetCharacterAtIndex(rosterIndex);
            if (character == null || rosterIndex == teamLoader.CurrentIndex) continue;
            CharacterSwitchSlot slot;
            if (viewIndex < activeSlots.Count) slot = activeSlots[viewIndex];
            else
            {
                GameObject host = Instantiate(slotPrefab, slotContainer);
                slot = host.GetComponent<CharacterSwitchSlot>();
                if (slot == null) { ReleaseSlot(host); continue; }
                activeSlots.Add(slot);
            }
            if (slot.BoundCharacter != character || slot.SlotIndex != rosterIndex)
                slot.Setup(character, rosterIndex, this, animate);
            else slot.RefreshFromHUD();
            viewIndex++;
        }
        for (int i = activeSlots.Count - 1; i >= viewIndex; i--)
        {
            CharacterSwitchSlot removed = activeSlots[i];
            activeSlots.RemoveAt(i);
            if (removed != null)
            {
                removed.gameObject.SetActive(false);
                ReleaseSlot(removed.gameObject);
            }
        }
        RefreshAllSlotCooldown();
    }

    private void ReleaseSlot(GameObject host)
    {
        if (Application.isPlaying) Destroy(host);
        else DestroyImmediate(host);
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
