using UnityEngine;

namespace Project.Progression
{
    internal static class CharacterMemoryKit
    {
        internal static bool AppliesTo(CharacterObject character) => character?.kitDefinition?.useMemoryRanks == true;

        internal static void ApplyBaseStats(CharacterObject character, CharacterStats stats, int completedSteps)
        {
            if (character == null || stats == null) return;
            CharacterStats baseStats = stats.Clone();
            for (int step = 1; step <= completedSteps; step++)
            {
                if (CharacterMemoryProgress.IsChoiceStep(step)) continue;
                CharacterGenericMemoryNodeDefinition node = GetGenericNode(character, step);
                if (node == null) continue;
                float current = stats.Get(node.stat);
                float gain = node.percentOfBase ? baseStats.Get(node.stat) * node.value / 100f : node.value;
                stats.Set(node.stat, current + gain);
            }
        }

        internal static CharacterGenericMemoryNodeDefinition GetGenericNode(CharacterObject character, int step)
        {
            if (character?.kitDefinition?.genericMemoryPages == null
                || step < 1 || step > CharacterMemoryProgress.StepCount
                || CharacterMemoryProgress.IsChoiceStep(step)) return null;

            int pageIndex = (step - 1) / CharacterMemoryProgress.StepsPerPage;
            if (pageIndex >= character.kitDefinition.genericMemoryPages.Count) return null;
            CharacterMemoryPageDefinition page = character.kitDefinition.genericMemoryPages[pageIndex];
            if (page?.genericNodes == null) return null;

            int firstStep = pageIndex * CharacterMemoryProgress.StepsPerPage + 1;
            int localIndex = 0;
            for (int currentStep = firstStep; currentStep < step; currentStep++)
                if (!CharacterMemoryProgress.IsChoiceStep(currentStep)) localIndex++;
            return localIndex < page.genericNodes.Count ? page.genericNodes[localIndex] : null;
        }

        internal static Sprite GetGenericPageIcon(CharacterObject character, int step)
        {
            if (character?.kitDefinition?.genericMemoryPages == null || step < 1
                || step > CharacterMemoryProgress.StepCount) return null;
            int pageIndex = (step - 1) / CharacterMemoryProgress.StepsPerPage;
            if (pageIndex >= character.kitDefinition.genericMemoryPages.Count) return null;
            return character.kitDefinition.genericMemoryPages[pageIndex]?.genericNodeIcon;
        }

        internal static string DescribeGeneric(CharacterObject character, int step)
        {
            CharacterGenericMemoryNodeDefinition node = GetGenericNode(character, step);
            if (node == null) return "No generic Memory effect is configured for this character.";
            if (!string.IsNullOrWhiteSpace(node.description)) return node.description;

            bool showPercent = node.percentOfBase || CombatStatCatalog.Get(node.stat).IsPercentage;
            string amount = showPercent ? $"{node.value:0.##}%" : $"{node.value:0.##}";
            return $"Base {CombatStatCatalog.Get(node.stat).Label} {Highlight($"+{amount}")}.";
        }

        private static string Highlight(string value) => $"<b><color=#a382ed>{value}</color></b>";

        internal static PotentialNodeDefinition GetNode(CharacterObject character, PotentialTrack track, int rank)
        {
            var nodes = character?.kitDefinition?.potentialNodes;
            if (nodes == null) return null;
            foreach (PotentialNodeDefinition node in nodes)
                if (node != null && node.track == track && node.level == rank) return node;
            return null;
        }
    }
}
