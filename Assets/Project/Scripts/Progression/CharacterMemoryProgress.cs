using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Progression
{
    [Serializable]
    public sealed class CharacterMemoryProgress
    {
        private const int StepsPerGroup = 3;
        public const int ChoiceCount = 3;
        public const int UpgradesPerFamily = 6;
        public const int MajorUpgradeCount = ChoiceCount * UpgradesPerFamily;
        public const int StepCount = MajorUpgradeCount * StepsPerGroup;
        public const int PageCount = 3;
        public const int StepsPerPage = StepCount / PageCount;
        public const int GenericStepsPerPage = StepsPerPage - StepsPerPage / StepsPerGroup;

        [SerializeField] private string _characterId;
        [SerializeField] private int _completedSteps;
        [SerializeField] private List<PotentialTrack> _choices = new List<PotentialTrack>();

        public string CharacterId => _characterId;
        public int CompletedSteps => _completedSteps;
        public int CompletedMajorUpgrades => _choices.Count;
        public int LastUnlockedPage => Math.Min(_completedSteps / StepsPerPage, PageCount - 1);

        public CharacterMemoryProgress(string characterId) => _characterId = characterId;

        public static bool IsChoiceStep(int step) =>
            step >= 1 && step <= StepCount && (step - 1) % StepsPerGroup == 0;

        public static string DescribeStep(int step, PotentialTrack choice) => IsChoiceStep(step)
            ? $"{choice} Memory"
            : "Generic Memory";

        public PotentialTrack GetChoice(int step) => IsChoiceStep(step) && step <= _completedSteps
            ? _choices[(step - 1) / StepsPerGroup]
            : PotentialTrack.Skill;

        internal int CountFamilyUpgrades(PotentialTrack choice, int beforeStep = StepCount + 1)
        {
            int count = 0;
            int precedingChoices = Math.Min(_choices.Count, (beforeStep + 1) / StepsPerGroup);
            for (int i = 0; i < precedingChoices; i++)
                if (_choices[i] == choice) count++;
            return count;
        }

        internal bool HasRoomInFamily(PotentialTrack choice) =>
            (uint)choice < ChoiceCount && CountFamilyUpgrades(choice) < UpgradesPerFamily;

        internal int GetPreviewRank(int step, PotentialTrack family)
        {
            if (!IsChoiceStep(step)) return 0;
            int precedingFutureRows = Math.Max(0, (step - 1) / StepsPerGroup - CompletedMajorUpgrades);
            return CountFamilyUpgrades(family, step) + precedingFutureRows + 1;
        }

        internal PotentialTrack DrawAvailableChoice(int seed)
        {
            var available = new List<PotentialTrack>(ChoiceCount);
            for (int family = 0; family < ChoiceCount; family++)
                if (HasRoomInFamily((PotentialTrack)family)) available.Add((PotentialTrack)family);
            return available[new System.Random(seed).Next(available.Count)];
        }

        public bool TrySelectStep(int step, PotentialTrack choice = PotentialTrack.Skill)
        {
            if (step < 1 || step > StepCount || (IsChoiceStep(step) && (uint)choice >= ChoiceCount))
                return false;

            while (_completedSteps < step)
            {
                _completedSteps++;
                if (IsChoiceStep(_completedSteps)) _choices.Add(FirstAvailableChoice());
            }

            if (IsChoiceStep(step))
            {
                int index = (step - 1) / StepsPerGroup;
                PotentialTrack previous = _choices[index];
                if (previous != choice && !HasRoomInFamily(choice))
                    _choices[_choices.LastIndexOf(choice)] = previous;
                _choices[index] = choice;
            }
            return true;
        }

        public CharacterMemoryProgress Copy()
        {
            var copy = new CharacterMemoryProgress(_characterId)
            {
                _completedSteps = Mathf.Clamp(_completedSteps, 0, StepCount)
            };
            int count = (copy._completedSteps + StepsPerGroup - 1) / StepsPerGroup;
            const PotentialTrack missing = (PotentialTrack)(-1);
            // Keep recorded choices first so filling gaps never displaces a valid later choice.
            for (int index = 0; index < count; index++)
            {
                PotentialTrack choice = index < (_choices?.Count ?? 0) ? _choices[index] : missing;
                copy._choices.Add(copy.HasRoomInFamily(choice) ? choice : missing);
            }

            for (int index = 0; index < count; index++)
                if (copy._choices[index] == missing) copy._choices[index] = copy.FirstAvailableChoice();

            return copy;
        }

        private PotentialTrack FirstAvailableChoice()
        {
            for (int family = 0; family < ChoiceCount; family++)
                if (HasRoomInFamily((PotentialTrack)family)) return (PotentialTrack)family;
            throw new InvalidOperationException("All Memory families are complete.");
        }
    }
}
