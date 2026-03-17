using UnityEngine;

namespace Project.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Artifact", menuName = "Inventory/Artifact", order = 1)]
    public class Artifact : BaseItem
    {
        [Header("Artifact Specifics")]
        public bool isEquippable = true;

        [Header("Stat Bonuses")]
        public float bonusATK = 0f;
        public float bonusDEF = 0f;
        public float bonusHP = 0f;
    }
}
