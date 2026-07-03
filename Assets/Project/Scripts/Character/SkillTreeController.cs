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
            Debug.LogWarning("Potential compatibility controller: Node not found -> " + nodeId);
            return false;
        }

        bool unlocked = passiveController != null && passiveController.UnlockNodeById(node.nodeId);
        if (unlocked)
            Debug.Log($"Potential compatibility controller: Upgraded node -> {node.nodeName} | Remaining Fragments = {GetCurrentPoints()}");

        return unlocked;
    }

    public bool LockNodeById(string nodeId)
    {
        SkillTreeNode node = GetNodeById(nodeId);
        if (node == null)
        {
            Debug.LogWarning("Potential compatibility controller: Node not found -> " + nodeId);
            return false;
        }

        bool locked = passiveController != null && passiveController.LockNodeById(node.nodeId);
        if (locked)
            Debug.Log("Potential compatibility controller: Lowered potential node -> " + node.nodeName);

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

        passiveController.ResetPotentialLevels();
        Debug.Log("Potential compatibility controller: Reset Potential levels");
    }

    public SkillTreeNode GetNodeById(string nodeId)
    {
        PotentialNodeDefinition node = passiveController != null ? passiveController.GetNodeById(nodeId) : null;
        return node != null ? new SkillTreeNode(node, IsNodeUnlocked(node.nodeId)) : null;
    }

    [System.Obsolete("SkillTreeData has been removed. Use ActionResolverTable instead.", false)]
    public object GetSkillTreeData()
    {
        return null;
    }

    public int GetCurrentPoints()
    {
        CharacterObject character = playerCharacter != null ? playerCharacter.characterObject : null;
        return passiveController != null && passiveController.MetaProgression != null && character != null
            ? passiveController.MetaProgression.GetFragmentCount(character.characterId)
            : 0;
    }

    public bool CanUnlockNode(SkillTreeNode node)
    {
        if (node == null || passiveController == null)
            return false;

        PotentialNodeDefinition definition = passiveController.GetNodeById(node.nodeId);
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
