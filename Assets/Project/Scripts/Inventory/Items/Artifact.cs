using UnityEngine;

namespace Project.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Artifact", menuName = "Inventory/Artifact", order = 1)]
    public class Artifact : BaseItem
    {
        [Header("Artifact Specifics")]
        public bool isEquippable = true;
        [TextArea(3, 5)]
        public string passiveDescription = "Unique passive effect description here.";

        [Header("Stat Bonuses")]
        [Tooltip("Bonuses applied when this artifact is equipped. Can be positive or negative.")]
        public CharacterStats bonusStats;
    }
}
