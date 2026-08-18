using System;
using UnityEngine;

namespace Project.InventorySystem.Items
{
    [Obsolete("Artifact is the legacy asset type. Use ArtifactDefinition for new code.")]
    public class Artifact : ArtifactDefinition
    {
        public CharacterStats bonusStats => LegacyBonusStats;
    }
}
