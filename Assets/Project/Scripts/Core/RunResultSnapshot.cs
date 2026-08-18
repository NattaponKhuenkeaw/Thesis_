using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.RunEnd
{
    [Serializable]
    public class RunResultItemLine
    {
        public string itemId;
        public string displayName;
        [Min(0)] public int quantity;

        public RunResultItemLine(string itemId, string displayName, int quantity)
        {
            this.itemId = itemId;
            this.displayName = string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName;
            this.quantity = Mathf.Max(0, quantity);
        }
    }

    [Serializable]
    public class RunResultSnapshot
    {
        [Min(0f)] public float elapsedSeconds;
        [Min(0)] public int roomsCleared;
        [Min(0)] public int lostCurrency;

        public List<RunResultItemLine> lostItems = new List<RunResultItemLine>();
        public List<RunResultItemLine> lostArtifacts = new List<RunResultItemLine>();
        public List<RunResultItemLine> permanentRewards = new List<RunResultItemLine>();
        public List<RunResultItemLine> unlockedCharacters = new List<RunResultItemLine>();

        public string FormatElapsedTime()
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }

        public void AddLostItem(string itemId, string displayName, int quantity)
        {
            AddOrMerge(lostItems, itemId, displayName, quantity);
        }

        public void AddLostArtifact(string itemId, string displayName, int quantity)
        {
            AddOrMerge(lostArtifacts, itemId, displayName, quantity);
        }

        public void AddPermanentReward(string itemId, string displayName, int quantity)
        {
            AddOrMerge(permanentRewards, itemId, displayName, quantity);
        }

        public void AddUnlockedCharacter(string itemId, string displayName)
        {
            AddOrMerge(unlockedCharacters, itemId, displayName, 1);
        }

        private static void AddOrMerge(List<RunResultItemLine> lines, string itemId, string displayName, int quantity)
        {
            if (lines == null || quantity <= 0)
            {
                return;
            }

            string key = !string.IsNullOrWhiteSpace(itemId) ? itemId : displayName;
            for (int i = 0; i < lines.Count; i++)
            {
                RunResultItemLine line = lines[i];
                if (line == null)
                {
                    continue;
                }

                string lineKey = !string.IsNullOrWhiteSpace(line.itemId) ? line.itemId : line.displayName;
                if (string.Equals(lineKey, key, StringComparison.Ordinal))
                {
                    line.quantity += quantity;
                    return;
                }
            }

            lines.Add(new RunResultItemLine(itemId, displayName, quantity));
        }
    }
}
