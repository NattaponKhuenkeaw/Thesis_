using System;
using System.Collections.Generic;
using Project.Progression;

namespace Project.Training
{
    [Serializable]
    public sealed class TrainingConfiguration
    {
        public int teamLevel = 1;
        public List<CharacterPotential> characters = new List<CharacterPotential>();

        [Serializable]
        public sealed class CharacterPotential
        {
            public string characterId;
            public int skill;
            public int ultimate;
            public int passive;
            public CharacterMemoryProgress memory;

            public int GetLevel(PotentialTrack track) => track switch
            {
                PotentialTrack.Skill => skill,
                PotentialTrack.Ultimate => ultimate,
                PotentialTrack.Passive => passive,
                _ => 0
            };

            public CharacterPotential Copy() => new CharacterPotential
            {
                characterId = characterId, skill = skill, ultimate = ultimate, passive = passive,
                memory = memory?.Copy()
            };
        }
    }
}
