using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CharacterProgressionState
{
    [SerializeField] private int constellationRank = 0;
    [SerializeField] private int availablePoints = 0;
    [SerializeField] private List<string> unlockedNodeIds = new List<string>();

    private readonly HashSet<string> unlockedLookup = new HashSet<string>();

    public int ConstellationRank
    {
        get => constellationRank;
        set => constellationRank = Mathf.Max(0, value);
    }

    public int AvailablePoints
    {
        get => availablePoints;
        set => availablePoints = Mathf.Max(0, value);
    }

    public IEnumerable<string> UnlockedNodeIds => unlockedNodeIds;

    public void Initialize(int rank, int points, IEnumerable<string> unlockedNodes = null)
    {
        constellationRank = Mathf.Max(0, rank);
        availablePoints = Mathf.Max(0, points);
        unlockedLookup.Clear();
        unlockedNodeIds.Clear();

        if (unlockedNodes == null)
        {
            return;
        }

        foreach (string nodeId in unlockedNodes)
        {
            if (string.IsNullOrEmpty(nodeId) || !unlockedLookup.Add(nodeId))
            {
                continue;
            }

            unlockedNodeIds.Add(nodeId);
        }
    }

    public bool IsUnlocked(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            return false;
        }

        EnsureLookup();
        return unlockedLookup.Contains(nodeId);
    }

    public bool Unlock(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            return false;
        }

        EnsureLookup();
        if (!unlockedLookup.Add(nodeId))
        {
            return false;
        }

        unlockedNodeIds.Add(nodeId);
        return true;
    }

    public bool Lock(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            return false;
        }

        EnsureLookup();
        if (!unlockedLookup.Remove(nodeId))
        {
            return false;
        }

        unlockedNodeIds.RemoveAll(id => string.Equals(id, nodeId, StringComparison.Ordinal));
        return true;
    }

    public void ResetUnlockedNodes()
    {
        unlockedLookup.Clear();
        unlockedNodeIds.Clear();
    }

    private void EnsureLookup()
    {
        if (unlockedLookup.Count == unlockedNodeIds.Count)
        {
            return;
        }

        unlockedLookup.Clear();
        for (int i = 0; i < unlockedNodeIds.Count; i++)
        {
            string nodeId = unlockedNodeIds[i];
            if (!string.IsNullOrEmpty(nodeId))
            {
                unlockedLookup.Add(nodeId);
            }
        }
    }
}
