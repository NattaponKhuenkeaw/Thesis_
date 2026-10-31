using UnityEngine;
using Project.Progression;

namespace Project.ShopSystem
{
    [CreateAssetMenu(fileName = "New Player Run Inventory", menuName = "Inventory/Player Run Inventory", order = 12)]
    public partial class PlayerRunInventory : CharacterInventoryState
    {
        protected override bool PersistenceEnabled => !Project.Training.TrainingSession.IsTrainingContext;
        protected override bool IsPersistentAsset => Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory") == this;
        protected override CharacterProgressionState ResolvePersistenceProgression() =>
            Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
    }
}
