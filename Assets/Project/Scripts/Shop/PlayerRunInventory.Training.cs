using System;
using UnityEngine;

namespace Project.ShopSystem
{
    public partial class PlayerRunInventory
    {
        [NonSerialized] private object trainingOwner;
        [NonSerialized] private int? trainingLevelOverride;
        [NonSerialized] private SavedPlayerRunInventoryData trainingEntrySnapshot;

        public void BeginTrainingSession(object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (trainingOwner != null && !ReferenceEquals(trainingOwner, owner))
                throw new InvalidOperationException("Run inventory already belongs to another Training session.");
            if (trainingOwner != null) return;
            EnsureRunData();
            // Capture values, not references to mutable inventory/team lists.
            trainingEntrySnapshot = JsonUtility.FromJson<SavedPlayerRunInventoryData>(JsonUtility.ToJson(CaptureRunSnapshot()));
            trainingOwner = owner;
        }

        public void SetTrainingLevelOverride(object owner, int level, bool notify = true)
        {
            if (!ReferenceEquals(trainingOwner, owner) || owner == null)
                throw new InvalidOperationException("Only the owning Training session may configure its level.");
            int before = GlobalLevel;
            trainingLevelOverride = Mathf.Clamp(level, 1, GlobalMaxLevel);
            if (notify) OnGlobalLevelChanged?.Invoke(before, GlobalLevel);
        }

        public void NotifyTrainingLevelChanged(int previousLevel)
        {
            OnGlobalLevelChanged?.Invoke(previousLevel, GlobalLevel);
        }

        public void EndTrainingSession(object owner)
        {
            if (owner == null || !ReferenceEquals(trainingOwner, owner)) return;
            int before = GlobalLevel;
            trainingLevelOverride = null;
            try
            {
                if (trainingEntrySnapshot != null) ApplyPersistentState(trainingEntrySnapshot);
            }
            finally
            {
                trainingEntrySnapshot = null;
                trainingOwner = null;
            }
            OnGlobalLevelChanged?.Invoke(before, GlobalLevel);
        }

        private bool SuppressTrainingPersistence => trainingOwner != null || Project.Training.TrainingSession.IsTrainingContext;
    }
}
