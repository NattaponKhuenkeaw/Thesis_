using UnityEngine;

public class SkillTreeController : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public bool UnlockNodeById(string nodeId)
    {
        CharacterObject characterObject = GetCharacterObject();
        if (characterObject == null)
            return false;

        SkillTreeNode node = GetNodeById(nodeId);
        if (node == null)
        {
            Debug.LogWarning("SkillTreeController: Node not found -> " + nodeId);
            return false;
        }

        if (node.unlocked)
        {
            Debug.Log("SkillTreeController: Node already unlocked -> " + nodeId);
            return false;
        }

        if (!CanUnlockNode(node))
            return false;

        characterObject.upgrade.skillTreePoints -= node.pointCost;
        node.unlocked = true;

        Debug.Log($"SkillTreeController: Unlocked node -> {node.nodeName} | Remaining Points = {characterObject.upgrade.skillTreePoints}");
        return true;
    }

    public bool LockNodeById(string nodeId)
    {
        SkillTreeNode node = GetNodeById(nodeId);
        if (node == null)
        {
            Debug.LogWarning("SkillTreeController: Node not found -> " + nodeId);
            return false;
        }

        node.unlocked = false;
        Debug.Log("SkillTreeController: Locked node -> " + node.nodeName);
        return true;
    }

    public bool IsNodeUnlocked(string nodeId)
    {
        SkillTreeNode node = GetNodeById(nodeId);
        if (node == null)
            return false;

        return node.unlocked;
    }

    public void ResetAllNodes()
    {
        CharacterObject characterObject = GetCharacterObject();
        SkillTreeData tree = GetSkillTreeData();

        if (characterObject == null || tree == null || tree.nodes == null)
            return;

        for (int i = 0; i < tree.nodes.Length; i++)
        {
            if (tree.nodes[i] != null)
                tree.nodes[i].unlocked = false;
        }

        characterObject.upgrade.skillTreePoints = 3;
        Debug.Log("SkillTreeController: Reset all nodes");
    }

    public SkillTreeNode GetNodeById(string nodeId)
    {
        SkillTreeData tree = GetSkillTreeData();
        if (tree == null || tree.nodes == null)
            return null;

        for (int i = 0; i < tree.nodes.Length; i++)
        {
            SkillTreeNode node = tree.nodes[i];
            if (node == null)
                continue;

            if (node.nodeId == nodeId)
                return node;
        }

        return null;
    }

    public SkillTreeData GetSkillTreeData()
    {
        CharacterObject characterObject = GetCharacterObject();
        if (characterObject == null)
            return null;

        return characterObject.skillTreeData;
    }

    public int GetCurrentPoints()
    {
        CharacterObject characterObject = GetCharacterObject();
        if (characterObject == null)
            return 0;

        return characterObject.upgrade.skillTreePoints;
    }

    public bool CanUnlockNode(SkillTreeNode node)
    {
        CharacterObject characterObject = GetCharacterObject();
        if (characterObject == null || node == null)
            return false;

        if (characterObject.upgrade.skillTreePoints < node.pointCost)
        {
            Debug.LogWarning("SkillTreeController: Not enough skill points");
            return false;
        }

        if (!string.IsNullOrEmpty(node.requiredNodeId))
        {
            SkillTreeNode requiredNode = GetNodeById(node.requiredNodeId);
            if (requiredNode == null)
            {
                Debug.LogWarning("SkillTreeController: Required node not found -> " + node.requiredNodeId);
                return false;
            }

            if (!requiredNode.unlocked)
            {
                Debug.LogWarning($"SkillTreeController: Need to unlock [{requiredNode.nodeName}] first");
                return false;
            }
        }

        return true;
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