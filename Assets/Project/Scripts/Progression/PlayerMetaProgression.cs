using UnityEngine;

namespace Project.Progression
{
    [CreateAssetMenu(fileName = "New Player Meta Progression", menuName = "Progression/Player Meta Progression", order = 20)]
    public partial class PlayerMetaProgression : CharacterProgressionState
    {
        protected override bool PersistenceEnabled => !Project.Training.TrainingSession.IsTrainingContext;
        protected override bool IsPersistentAsset => Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression") == this;
    }
}
