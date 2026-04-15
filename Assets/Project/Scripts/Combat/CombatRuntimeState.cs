using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CombatRuntimeState
{
    [Serializable]
    private class RuntimeTagEntry
    {
        public string tag;
        public bool hasExpiry;
        public float expiresAt;
    }

    [Serializable]
    private class RuntimeCounterEntry
    {
        public string counterId;
        public int value;
    }

    [SerializeField] private List<RuntimeTagEntry> runtimeTags = new List<RuntimeTagEntry>();
    [SerializeField] private List<RuntimeCounterEntry> runtimeCounters = new List<RuntimeCounterEntry>();

    public event Action OnStateChanged;

    public bool HasTag(string tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        PruneExpiredTags();
        return FindTagEntry(tag) != null;
    }

    public void SetTag(string tag, float duration = 0f)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        RuntimeTagEntry entry = FindTagEntry(tag);
        if (entry == null)
        {
            entry = new RuntimeTagEntry { tag = tag };
            runtimeTags.Add(entry);
        }

        entry.hasExpiry = duration > 0f;
        entry.expiresAt = entry.hasExpiry ? Time.time + duration : 0f;
        OnStateChanged?.Invoke();
    }

    public bool RemoveTag(string tag)
    {
        RuntimeTagEntry entry = FindTagEntry(tag);
        if (entry == null)
        {
            return false;
        }

        runtimeTags.Remove(entry);
        OnStateChanged?.Invoke();
        return true;
    }

    public int GetCounter(string counterId)
    {
        RuntimeCounterEntry entry = FindCounterEntry(counterId);
        return entry != null ? entry.value : 0;
    }

    public void SetCounter(string counterId, int value)
    {
        if (string.IsNullOrEmpty(counterId))
        {
            return;
        }

        RuntimeCounterEntry entry = FindCounterEntry(counterId);
        if (entry == null)
        {
            entry = new RuntimeCounterEntry { counterId = counterId };
            runtimeCounters.Add(entry);
        }

        entry.value = value;
        OnStateChanged?.Invoke();
    }

    public int AddCounter(string counterId, int delta)
    {
        int result = GetCounter(counterId) + delta;
        SetCounter(counterId, result);
        return result;
    }

    public bool ConsumeCounter(string counterId, int amount = 1)
    {
        if (GetCounter(counterId) < amount)
        {
            return false;
        }

        SetCounter(counterId, GetCounter(counterId) - amount);
        return true;
    }

    public void Reset()
    {
        runtimeTags.Clear();
        runtimeCounters.Clear();
        OnStateChanged?.Invoke();
    }

    public void Tick()
    {
        PruneExpiredTags();
    }

    private void PruneExpiredTags()
    {
        float now = Time.time;
        bool removedAny = false;
        for (int i = runtimeTags.Count - 1; i >= 0; i--)
        {
            RuntimeTagEntry entry = runtimeTags[i];
            if (entry != null && entry.hasExpiry && now >= entry.expiresAt)
            {
                runtimeTags.RemoveAt(i);
                removedAny = true;
            }
        }

        if (removedAny)
        {
            OnStateChanged?.Invoke();
        }
    }

    private RuntimeTagEntry FindTagEntry(string tag)
    {
        for (int i = 0; i < runtimeTags.Count; i++)
        {
            RuntimeTagEntry entry = runtimeTags[i];
            if (entry != null && string.Equals(entry.tag, tag, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    private RuntimeCounterEntry FindCounterEntry(string counterId)
    {
        if (string.IsNullOrEmpty(counterId))
        {
            return null;
        }

        for (int i = 0; i < runtimeCounters.Count; i++)
        {
            RuntimeCounterEntry entry = runtimeCounters[i];
            if (entry != null && string.Equals(entry.counterId, counterId, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }
}
