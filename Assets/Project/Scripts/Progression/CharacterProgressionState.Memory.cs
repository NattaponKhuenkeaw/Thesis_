using System;
using System.Collections.Generic;

namespace Project.Progression
{
    public partial class CharacterProgressionState
    {
        [NonSerialized] private readonly List<CharacterMemoryProgress> _characterMemories = new List<CharacterMemoryProgress>();

        public event Action<string> OnMemoryChanged;

        public CharacterMemoryProgress CaptureMemory(string characterId) =>
            FindMemory(characterId)?.Copy() ?? new CharacterMemoryProgress(characterId);

        public int GetCompletedMemorySteps(string characterId) => FindMemory(characterId)?.CompletedSteps ?? 0;
        public int GetCompletedMajorMemoryUpgrades(string characterId) => FindMemory(characterId)?.CompletedMajorUpgrades ?? 0;
        public int GetMemoryRank(string characterId, PotentialTrack track) => FindMemory(characterId)?.CountFamilyUpgrades(track) ?? 0;

        public int GetCombatPotentialLevel(CharacterObject character, PotentialTrack track) =>
            MainMemoryKit.AppliesTo(character) ? GetMemoryRank(character.characterId, track) : GetPotentialLevel(character, track);

        public void ApplyMemoryBaseStats(CharacterObject character, CharacterStats stats)
        {
            if (MainMemoryKit.AppliesTo(character))
                MainMemoryKit.ApplyBaseStats(stats, GetCompletedMemorySteps(character.characterId));
        }

        public bool TryDrawMemory(CharacterObject character, int step, int seed, out PotentialTrack choice)
        {
            choice = PotentialTrack.Skill;
            if (character == null) return false;

            var memory = FindMemory(character.characterId);
            if (step != (memory?.CompletedSteps ?? 0) + 1 || step > CharacterMemoryProgress.StepCount) return false;
            if (CharacterMemoryProgress.IsChoiceStep(step))
            {
                memory ??= new CharacterMemoryProgress(character.characterId);
                choice = memory.DrawAvailableChoice(seed);
            }

            return TryGrantMemory(character, step, choice);
        }

        public bool TryGrantMemory(CharacterObject character, int step, PotentialTrack choice)
        {
            if (character == null || character.kitDefinition == null || string.IsNullOrWhiteSpace(character.characterId))
                return false;

            var memory = FindMemory(character.characterId);
            if (step != (memory?.CompletedSteps ?? 0) + 1) return false;
            memory ??= new CharacterMemoryProgress(character.characterId);
            if (CharacterMemoryProgress.IsChoiceStep(step) && !memory.HasRoomInFamily(choice)) return false;
            if (!memory.TrySelectStep(step, choice)) return false;

            if (step == 1) _characterMemories.Add(memory);
            InvalidatePotentialCache(character.characterId);
            SavePersistentState();
            OnMemoryChanged?.Invoke(character.characterId);
            return true;
        }

        public void RestoreRunCheckpoint(string json) => ImportJsonInternal(json, preserveMemory: true);

        protected void SetPracticeMemory(CharacterMemoryProgress memory)
        {
            if (memory == null || string.IsNullOrWhiteSpace(memory.CharacterId)) return;
            _characterMemories.RemoveAll(entry => entry.CharacterId == memory.CharacterId);
            if (memory.CompletedSteps > 0) _characterMemories.Add(memory.Copy());
            InvalidatePotentialCache(memory.CharacterId);
            OnMemoryChanged?.Invoke(memory.CharacterId);
        }

        private CharacterMemoryProgress FindMemory(string characterId)
        {
            foreach (CharacterMemoryProgress memory in _characterMemories)
                if (string.Equals(memory.CharacterId, characterId, StringComparison.Ordinal)) return memory;
            return null;
        }

        private void ImportMemory(List<CharacterMemoryProgress> saved)
        {
            if (saved == null) return;
            foreach (CharacterMemoryProgress memory in saved)
            {
                if (memory == null || string.IsNullOrWhiteSpace(memory.CharacterId) || FindMemory(memory.CharacterId) != null)
                    continue;
                var copy = memory.Copy();
                if (copy.CompletedSteps > 0) _characterMemories.Add(copy);
            }
        }
    }
}
