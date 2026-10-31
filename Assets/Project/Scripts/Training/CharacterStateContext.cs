using Project.Progression;
using Project.ShopSystem;
using UnityEngine;

namespace Project.Training
{
    public static class CharacterStateContext
    {
        public static CharacterInventoryState ResolveInventory(CharacterInventoryState normal = null)
        {
            if (TrainingSession.IsTrainingContext) return TrainingSession.Instance != null ? TrainingSession.Instance.Inventory : null;
            return normal != null ? normal : Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory");
        }

        public static CharacterProgressionState ResolveProgression(CharacterProgressionState normal = null)
        {
            if (TrainingSession.IsTrainingContext) return TrainingSession.Instance != null ? TrainingSession.Instance.Progression : null;
            return normal != null ? normal : Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
        }
    }
}
