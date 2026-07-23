using UnityEngine;

namespace Project.InventorySystem.Items
{
    public enum ItemRetentionPolicy
    {
        UseTypeDefault,
        LoseOnDeath,
        KeepPermanent,
        ConvertOnDeath
    }

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

        [Header("Run Retention")]
        [Tooltip("Controls what happens to this item when the player dies. UseTypeDefault keeps existing type behavior.")]
        public ItemRetentionPolicy retentionPolicy = ItemRetentionPolicy.UseTypeDefault;

        [Tooltip("Fragments gained per item when Retention Policy is Convert On Death.")]
        [Min(1)] public int deathConversionFragmentAmount = 1;

        public ItemRetentionPolicy ResolvedRetentionPolicy
        {
            get
            {
                if (retentionPolicy != ItemRetentionPolicy.UseTypeDefault)
                {
                    return retentionPolicy;
                }

                return this is CharacterItem
                    ? ItemRetentionPolicy.KeepPermanent
                    : ItemRetentionPolicy.LoseOnDeath;
            }
        }
    }
}
