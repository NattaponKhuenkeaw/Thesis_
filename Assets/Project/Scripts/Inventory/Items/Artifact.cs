using UnityEngine;

namespace Project.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Artifact", menuName = "Inventory/Artifact", order = 1)]
    public class Artifact : BaseItem
    {
        [Header("Artifact Specifics")]
        public bool isEquippable = true;
        // Add specific modifier data or logic later based on requirements
    }
}
