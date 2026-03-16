using UnityEngine;

namespace Project.InventorySystem.Items
{
    public abstract class BaseItem : ScriptableObject
    {
        [Header("Basic Information")]
        public string id;
        public string itemName;
        [TextArea(3, 10)]
        public string description;
        public Sprite icon;
        
        [Header("Shop Settings")]
        public int price;
    }
}
