using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillTreeNodeButton : MonoBehaviour
{
    // =========================================================
    // Node Info
    // =========================================================
    [Header("Node Info")]
    public string nodeId;

    // =========================================================
    // UI References
    // =========================================================
    [Header("UI References")]
    public TMP_Text nodeNameText;
    public TMP_Text descriptionText;
    public TMP_Text costText;
    public TMP_Text stateText;
    public Button unlockButton;

    // =========================================================
    // Runtime
    // =========================================================
    private SkillTreeController skillTreeController;

    // =========================================================
    // Public Methods
    // =========================================================
    public void Refresh(SkillTreeController controller)
    {
        skillTreeController = controller;

        if (skillTreeController == null)
            return;

        SkillTreeNode node = skillTreeController.GetNodeById(nodeId);
        if (node == null)
            return;

        if (nodeNameText != null)
            nodeNameText.text = node.nodeName;

        if (descriptionText != null)
            descriptionText.text = node.description;

        if (costText != null)
            costText.text = "Fragments: " + node.pointCost;

        if (stateText != null)
        {
            if (node.unlocked)
                stateText.text = "Unlocked";
            else if (skillTreeController.CanUnlockNode(node))
                stateText.text = "Upgradeable";
            else
                stateText.text = "Locked";
        }

        if (unlockButton != null)
            unlockButton.interactable = !node.unlocked && skillTreeController.CanUnlockNode(node);
    }

    public void OnClickUnlock()
    {
        if (skillTreeController == null)
            return;

        skillTreeController.UnlockNodeById(nodeId);
    }
}
