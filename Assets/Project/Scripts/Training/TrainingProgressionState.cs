using System.Collections.Generic;
using Project.Progression;

namespace Project.Training
{
    // Practice potentials start at zero and exist only for this Training scene.
    public sealed class TrainingProgressionState : CharacterProgressionState
    {
        public void SetPotentials(IReadOnlyList<TrainingConfiguration.CharacterPotential> values, bool notify = true)
        {
            if (values == null) return;
            foreach (var value in values)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.characterId)) continue;
                var progress = FindPotentialProgress(value.characterId, createIfMissing: true);
                progress.SetLevel(PotentialTrack.Skill, value.skill);
                progress.SetLevel(PotentialTrack.Ultimate, value.ultimate);
                progress.SetLevel(PotentialTrack.Passive, value.passive);
            }
            InvalidatePotentialCache();
            if (notify) NotifyChanged();
        }

        public void NotifyChanged() => NotifyPotentialLevelsChanged();
    }
}
