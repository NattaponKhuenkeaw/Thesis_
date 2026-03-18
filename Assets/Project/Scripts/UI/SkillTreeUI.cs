using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillTreeUI : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    public SkillTreeController skillTreeController;

    // =========================================================
    // UI References
    // =========================================================
    [Header("UI References")]
    public TMP_Text characterNameText;
    public TMP_Text pointsText;
    public SkillTreeNodeButton[] nodeButtons;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = FindObjectOfType<PlayerCharacter>();

        if (skillTreeController == null)
            skillTreeController = FindObjectOfType<SkillTreeController>();
    }

    void OnEnable()
    {
        RefreshUI();
    }

    void Update()
    {
        RefreshUI();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void RefreshUI()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null || skillTreeController == null)
            return;

        if (characterNameText != null)
            characterNameText.text = playerCharacter.characterObject.metaData.displayName + " Skill Tree";

        if (pointsText != null)
            pointsText.text = "Points: " + skillTreeController.GetCurrentPoints();

        if (nodeButtons == null)
            return;

        for (int i = 0; i < nodeButtons.Length; i++)
        {
            if (nodeButtons[i] != null)
                nodeButtons[i].Refresh(skillTreeController);
        }
    }
}