using System;
using System.Collections.Generic;
using Project.Training;

namespace Project.Progression
{
    public partial class PlayerMetaProgression
    {
        [NonSerialized] private object trainingOwner;
        [NonSerialized] private Dictionary<string, TrainingConfiguration.CharacterPotential> trainingPotentials;
        [NonSerialized] private string trainingEntryJson;
        [NonSerialized] private string trainingEntryCommittedJson;

        public void BeginTrainingSession(object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (trainingOwner != null && !ReferenceEquals(trainingOwner, owner))
                throw new InvalidOperationException("Meta progression already belongs to another Training session.");
            if (trainingOwner != null) return;
            // The save export intentionally omits legacy permanent items; session restoration
            // must preserve the entire authored state, including data outside that save schema.
            trainingEntryJson = UnityEngine.JsonUtility.ToJson(this);
            trainingEntryCommittedJson = committedJson;
            trainingOwner = owner;
        }

        public void SetTrainingPotentialOverrides(object owner,
            IReadOnlyList<TrainingConfiguration.CharacterPotential> values, bool notify = true)
        {
            if (owner == null || !ReferenceEquals(trainingOwner, owner))
                throw new InvalidOperationException("Only the owning Training session may configure potentials.");
            var next = new Dictionary<string, TrainingConfiguration.CharacterPotential>();
            if (values != null)
                for (int i = 0; i < values.Count; i++)
                {
                    var value = values[i];
                    if (value == null || string.IsNullOrWhiteSpace(value.characterId)) continue;
                    var copy = value.Copy();
                    copy.skill = UnityEngine.Mathf.Clamp(copy.skill, 0, CharacterKitDefinition.MaxPotentialLevel);
                    copy.ultimate = UnityEngine.Mathf.Clamp(copy.ultimate, 0, CharacterKitDefinition.MaxPotentialLevel);
                    copy.passive = UnityEngine.Mathf.Clamp(copy.passive, 0, CharacterKitDefinition.MaxPotentialLevel);
                    next[copy.characterId] = copy;
                }
            trainingPotentials = next;
            InvalidatePotentialCache();
            if (notify) NotifyTrainingPotentialsChanged();
        }

        public void NotifyTrainingPotentialsChanged()
        {
            InvalidatePotentialCache();
            OnPotentialChanged?.Invoke(null);
        }

        public void EndTrainingSession(object owner)
        {
            if (owner == null || !ReferenceEquals(trainingOwner, owner)) return;
            trainingPotentials = null;
            try
            {
                if (trainingEntryJson != null) UnityEngine.JsonUtility.FromJsonOverwrite(trainingEntryJson, this);
                committedJson = trainingEntryCommittedJson;
            }
            finally
            {
                trainingEntryJson = trainingEntryCommittedJson = null;
                trainingOwner = null;
                InvalidatePotentialCache();
            }
            OnPotentialChanged?.Invoke(null);
            OnFragmentsChanged?.Invoke();
        }

        private bool SuppressTrainingPersistence => trainingOwner != null || TrainingSession.IsTrainingContext;
    }
}
