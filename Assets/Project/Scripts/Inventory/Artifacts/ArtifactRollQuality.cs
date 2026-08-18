using UnityEngine;

namespace Project.InventorySystem.Artifacts
{
    public static class ArtifactRollQuality
    {
        public static bool TryGetLineQuality(
            in ArtifactRoll roll,
            ArtifactRollTable table,
            out float quality01)
        {
            quality01 = 0f;
            if (roll.source != ArtifactRollSource.Generated
                || table == null
                || !table.TryGet(roll.statKind, out ArtifactStatRollDefinition definition)
                || definition.maximumUnits <= definition.minimumUnits
                || roll.valueUnits < definition.minimumUnits
                || roll.valueUnits > definition.maximumUnits)
            {
                return false;
            }

            quality01 = Mathf.InverseLerp(definition.minimumUnits, definition.maximumUnits, roll.valueUnits);
            return true;
        }

        public static bool TryGetAverageQuality(
            ArtifactInstance instance,
            ArtifactRollTable table,
            out float quality01)
        {
            quality01 = 0f;
            if (instance == null || instance.rolls == null) return false;

            float total = 0f;
            int count = 0;
            for (int i = 0; i < instance.rolls.Count; i++)
            {
                if (!TryGetLineQuality(instance.rolls[i], table, out float lineQuality)) continue;
                total += lineQuality;
                count++;
            }

            if (count == 0) return false;
            quality01 = total / count;
            return true;
        }

    }
}
