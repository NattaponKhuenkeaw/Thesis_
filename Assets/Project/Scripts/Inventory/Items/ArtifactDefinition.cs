using System.Collections.Generic;
using Project.InventorySystem.Artifacts;
using UnityEngine;
using UnityEngine.Serialization;

namespace Project.InventorySystem.Items
{
    public enum ArtifactRarity
    {
        Unassigned,
        R,
        SR,
        SSR
    }

    [CreateAssetMenu(fileName = "New Artifact", menuName = "Inventory/Artifact Definition", order = 1)]
    public class ArtifactDefinition : BaseItem
    {
        [Header("Artifact Specifics")]
        public bool isEquippable = true;
        public ArtifactRarity rarity;

        [Tooltip("Fixed stat bonus in roll units. Zero means the definition has no fixed bonus.")]
        public ArtifactRoll fixedStatBonus;

        [TextArea(3, 5)]
        public string passiveDescription = "Unique passive effect description here.";

        [Header("Artifact Passive")]
        [Tooltip("Immutable authored rules. Mutable cooldowns and stacks live on the equipped instance runtime.")]
        public ArtifactPassiveSetDefinition passiveSet;

        [Header("Passive Damage")]
        [Tooltip("Optional separate on-hit damage ticks. These cannot critically hit.")]
        public List<AdditionalDamageEffectDefinition> additionalDamageEffects = new List<AdditionalDamageEffectDefinition>();

        [FormerlySerializedAs("bonusStats")]
        [SerializeField, HideInInspector]
        private CharacterStats legacyBonusStats;

        public CharacterStats LegacyBonusStats => legacyBonusStats;

        public void SetLegacyBonusStats(CharacterStats value)
        {
            legacyBonusStats = value;
        }
    }
}
