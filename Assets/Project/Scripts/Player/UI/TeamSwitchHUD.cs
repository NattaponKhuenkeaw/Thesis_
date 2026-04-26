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
    private int lastActiveIndex = -1;
    private string lastTeamSignature = string.Empty;

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
        if (teamLoader == null) return;

        string currentTeamSignature = BuildTeamSignature();
        if (currentTeamSignature != lastTeamSignature)
        {
            InitializeHUD();
        }

        if (teamLoader.CurrentIndex != lastActiveIndex)
        {
            UpdateActiveIndicator(teamLoader.CurrentIndex);
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
        teamLoader.OnTeamChanged += HandleTeamChanged;
        teamLoader.OnCurrentCharacterChanged += HandleCurrentCharacterChanged;
        teamLoader.OnCharacterHealthUpdated += HandleCharacterHealthUpdated;
    }

    private void UnbindTeamLoader()
    {
        if (teamLoader == null)
            return;

        teamLoader.OnTeamChanged -= HandleTeamChanged;
        teamLoader.OnCurrentCharacterChanged -= HandleCurrentCharacterChanged;
        teamLoader.OnCharacterHealthUpdated -= HandleCharacterHealthUpdated;
    }

    private void HandleTeamChanged()
    {
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

    private string BuildTeamSignature()
    {
        if (teamLoader == null || teamLoader.team == null)
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < teamLoader.team.Length; i++)
        {
            CharacterObject character = teamLoader.team[i];
            builder.Append(i).Append(':');
            builder.Append(character != null ? character.GetInstanceID() : 0);
            builder.Append('|');
        }
        return builder.ToString();
    }

    // เปลี่ยนให้เป็น public เผื่อในอนาคตอยากสั่ง Refresh จากสคริปต์อื่นโดยตรง
    public void InitializeHUD()
    {
        if (teamLoader == null || teamLoader.team == null || slotContainer == null || slotPrefab == null) return;

        lastTeamSignature = BuildTeamSignature();

        foreach (Transform child in slotContainer)
        {
            Destroy(child.gameObject);
        }
        activeSlots.Clear();

        for (int i = 0; i < teamLoader.team.Length; i++)
        {
            CharacterObject character = teamLoader.team[i];

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

        // เช็คกฎเปลี่ยนตัว
        if (stateMachine != null && !stateMachine.CanSwitchCharacter())
        {
            Debug.Log("ไม่สามารถเปลี่ยนตัวได้ในจังหวะนี้!");
            return;
        }

        teamLoader.SwitchToIndex(index);
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
        lastActiveIndex = activeIndex;

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
}
