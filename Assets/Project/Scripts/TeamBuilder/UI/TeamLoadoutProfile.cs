using UnityEngine;
using Project.InventorySystem.Items;

namespace Project.TeamBuilder.UI
{
    [CreateAssetMenu(fileName = "TeamLoadoutProfile", menuName = "Team Builder/Team Loadout Profile")]
    public class TeamLoadoutProfile : ScriptableObject
    {
        private const int DefaultSlotCount = 3;
        private const int DefaultArtifactCount = 3;

        [System.Serializable]
        private class SlotProfile
        {
            public CharacterObject character;
            public Artifact[] artifacts = new Artifact[DefaultArtifactCount];
        }

        [SerializeField] private SlotProfile[] slots = new SlotProfile[DefaultSlotCount];
        [SerializeField] private int activeIndex;

        public int ActiveIndex => activeIndex;
        public int SlotCount => slots != null ? slots.Length : 0;

        public bool HasAnyCharacter()
        {
            if (slots == null)
                return false;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].character != null)
                    return true;
            }

            return false;
        }

        public void SaveFrom(CharacterObject[] team, Artifact[][] artifactsBySlot, int selectedIndex)
        {
            int slotCount = Mathf.Max(DefaultSlotCount, team != null ? team.Length : 0);
            EnsureSlotCapacity(slotCount);

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].character = team != null && i < team.Length ? team[i] : null;
                EnsureArtifactCapacity(slots[i], DefaultArtifactCount);

                Artifact[] sourceArtifacts = artifactsBySlot != null && i < artifactsBySlot.Length
                    ? artifactsBySlot[i]
                    : null;

                for (int artifactIndex = 0; artifactIndex < slots[i].artifacts.Length; artifactIndex++)
                {
                    slots[i].artifacts[artifactIndex] = sourceArtifacts != null && artifactIndex < sourceArtifacts.Length
                        ? sourceArtifacts[artifactIndex]
                        : null;
                }
            }

            activeIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, slots.Length - 1));
        }

        public CharacterObject[] GetTeamSnapshot(int minimumSlots = DefaultSlotCount)
        {
            EnsureSlotCapacity(Mathf.Max(DefaultSlotCount, minimumSlots));

            CharacterObject[] snapshot = new CharacterObject[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                snapshot[i] = slots[i] != null ? slots[i].character : null;
            }

            return snapshot;
        }

        public Artifact[] GetArtifactsSnapshot(int slotIndex, int minimumArtifacts = DefaultArtifactCount)
        {
            EnsureSlotCapacity(DefaultSlotCount);

            int artifactCount = Mathf.Max(DefaultArtifactCount, minimumArtifacts);
            Artifact[] snapshot = new Artifact[artifactCount];
            if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
                return snapshot;

            EnsureArtifactCapacity(slots[slotIndex], artifactCount);
            for (int i = 0; i < snapshot.Length && i < slots[slotIndex].artifacts.Length; i++)
            {
                snapshot[i] = slots[slotIndex].artifacts[i];
            }

            return snapshot;
        }

        private void EnsureSlotCapacity(int minimumSlots)
        {
            int requiredCount = Mathf.Max(1, minimumSlots);
            if (slots != null && slots.Length >= requiredCount)
            {
                EnsureSlotInstances();
                return;
            }

            SlotProfile[] previousSlots = slots;
            slots = new SlotProfile[requiredCount];

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = previousSlots != null && i < previousSlots.Length && previousSlots[i] != null
                    ? previousSlots[i]
                    : new SlotProfile();
                EnsureArtifactCapacity(slots[i], DefaultArtifactCount);
            }
        }

        private void EnsureSlotInstances()
        {
            if (slots == null)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] ??= new SlotProfile();
                EnsureArtifactCapacity(slots[i], DefaultArtifactCount);
            }
        }

        private static void EnsureArtifactCapacity(SlotProfile slot, int minimumArtifacts)
        {
            if (slot == null)
                return;

            int requiredCount = Mathf.Max(1, minimumArtifacts);
            if (slot.artifacts != null && slot.artifacts.Length >= requiredCount)
                return;

            Artifact[] previousArtifacts = slot.artifacts;
            slot.artifacts = new Artifact[requiredCount];
            if (previousArtifacts == null)
                return;

            for (int i = 0; i < previousArtifacts.Length && i < slot.artifacts.Length; i++)
            {
                slot.artifacts[i] = previousArtifacts[i];
            }
        }
    }
}
