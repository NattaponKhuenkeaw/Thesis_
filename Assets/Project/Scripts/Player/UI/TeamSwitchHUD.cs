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
    private int lastTeamCount = -1; // ตัวแปรใหม่สำหรับจำจำนวนคนในทีม

    void Start()
    {
        if (teamLoader == null)
            teamLoader = Object.FindFirstObjectByType<TeamLoader>();

        if (stateMachine == null)
            stateMachine = Object.FindFirstObjectByType<PlayerStateMachine>();

        InitializeHUD();
    }

    void Update()
    {
        if (teamLoader == null) return;

        // 1. ตรวจจับอัตโนมัติ: เช็คว่ามีการเพิ่ม/ลดตัวละครในทีมหรือไม่
        int currentTeamCount = CountActiveTeamMembers();
        if (currentTeamCount != lastTeamCount)
        {
            // ถ้าจำนวนคนในทีมเปลี่ยนไป ให้ล้าง UI เก่าแล้วสร้างใหม่ทั้งหมด
            InitializeHUD();
        }

        // 2. อัปเดตสัญลักษณ์ Active เมื่อเปลี่ยนตัวละคร
        if (teamLoader.CurrentIndex != lastActiveIndex)
        {
            UpdateActiveIndicator(teamLoader.CurrentIndex);
        }
    }

    // ฟังก์ชันนับว่าตอนนี้มีฮีโร่ในทีมกี่ตัว (ข้ามช่องที่ว่างเปล่า)
    private int CountActiveTeamMembers()
    {
        int count = 0;
        if (teamLoader.team != null)
        {
            for (int i = 0; i < teamLoader.team.Length; i++)
            {
                if (teamLoader.team[i] != null) count++;
            }
        }
        return count;
    }

    // เปลี่ยนให้เป็น public เผื่อในอนาคตอยากสั่ง Refresh จากสคริปต์อื่นโดยตรง
    public void InitializeHUD()
    {
        if (teamLoader == null || teamLoader.team == null) return;

        // บันทึกจำนวนคนในทีมล่าสุดไว้
        lastTeamCount = CountActiveTeamMembers();

        // ลบปุ่มเก่าทิ้ง (ถ้ามี)
        foreach (Transform child in slotContainer)
        {
            Destroy(child.gameObject);
        }
        activeSlots.Clear();

        // สร้างปุ่มใหม่ตามตัวละครที่มีอยู่จริง ณ ปัจจุบัน
        for (int i = 0; i < teamLoader.team.Length; i++)
        {
            CharacterObject character = teamLoader.team[i];

            // ข้ามช่องที่ไม่มีฮีโร่
            if (character == null) continue;

            GameObject newSlotObj = Instantiate(slotPrefab, slotContainer);
            CharacterSwitchSlot slotUI = newSlotObj.GetComponent<CharacterSwitchSlot>();

            if (slotUI != null)
            {
                slotUI.Setup(character, i, this);
                activeSlots.Add(slotUI);
            }
        }

        // อัปเดตสัญลักษณ์ช่องที่เล่นอยู่
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

    private void UpdateActiveIndicator(int activeIndex)
    {
        lastActiveIndex = activeIndex;

        for (int i = 0; i < activeSlots.Count; i++)
        {
            bool isCurrentActive = (i == activeIndex);
            activeSlots[i].SetActiveStatus(isCurrentActive);
        }
    }
}