using UnityEngine;

public class SkillTreeController : MonoBehaviour
{
    
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    public CharacterPassiveController passiveController;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (passiveController == null)
            passiveController = GetComponent<CharacterPassiveController>();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public bool UnlockNodeById(string nodeId)
    {
        SkillTreeNode node = GetNodeById(nodeId);
        if (node == null)
        {
            Debug.LogWarning("SkillTreeController: Node not found -> " + nodeId);
            return false;
        }

        bool unlocked = passiveController != null && passiveController.UnlockNodeById(node.nodeId);
        if (unlocked)
            Debug.Log($"SkillTreeController: Unlocked node -> {node.nodeName} | Remaining Points = {GetCurrentPoints()}");

        return unlocked;
    }

    public bool LockNodeById(string nodeId)
    {
        SkillTreeNode node = GetNodeById(nodeId);
        if (node == null)
        {
            Debug.LogWarning("SkillTreeController: Node not found -> " + nodeId);
            return false;
        }

        bool locked = passiveController != null && passiveController.LockNodeById(node.nodeId);
        if (locked)
            Debug.Log("SkillTreeController: Locked node -> " + node.nodeName);

        return locked;
    }

    public bool IsNodeUnlocked(string nodeId)
    {
        SkillTreeNode node = GetNodeById(nodeId);
        if (node == null)
            return false;

        return passiveController != null && passiveController.IsNodeUnlocked(node.nodeId);
    }

    public void ResetAllNodes()
    {
        if (passiveController == null)
            return;

        passiveController.ResetProgression();
        Debug.Log("SkillTreeController: Reset all nodes");
    }

    public SkillTreeNode GetNodeById(string nodeId)
    {
        ProgressionNodeDefinition node = passiveController != null ? passiveController.GetNodeById(nodeId) : null;
        return node != null ? new SkillTreeNode(node, IsNodeUnlocked(node.nodeId)) : null;
    }

    [System.Obsolete("SkillTreeData has been removed. Use ActionResolverTable instead.", false)]
    public object GetSkillTreeData()
    {
        return null;
    }

    public int GetCurrentPoints()
    {
        return passiveController != null ? passiveController.ProgressionState.AvailablePoints : 0;
    }

    public bool CanUnlockNode(SkillTreeNode node)
    {
        if (node == null || passiveController == null)
            return false;

        ProgressionNodeDefinition definition = passiveController.GetNodeById(node.nodeId);
        return passiveController.CanUnlockNode(definition);
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private CharacterObject GetCharacterObject()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return null;

        return playerCharacter.characterObject;
    }
}
