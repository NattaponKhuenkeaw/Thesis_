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
        public float durationSeconds;
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

    // =========================================================
    // Blackboard Accessors (Well-known keys)
    // =========================================================
    public bool HasHitTarget
    {
        get => HasTag("hit_target");
        set
        {
            if (value) SetTag("hit_target");
            else RemoveTag("hit_target");
        }
    }

    public int SkillUsageCount
    {
        get => GetCounter("skill_usage");
        set => SetCounter("skill_usage", value);
    }

    public string CurrentStance
    {
        get
        {
            // For now, stance can be inferred from a specific tag or we could add string storage.
            // Using a simple convention: any tag starting with "stance_" is the current stance.
            PruneExpiredTags();
            foreach (var entry in runtimeTags)
            {
                if (entry != null && entry.tag.StartsWith("stance_"))
                    return entry.tag.Substring("stance_".Length);
            }
            return "Default";
        }
        set
        {
            // Remove previous stance
            for (int i = runtimeTags.Count - 1; i >= 0; i--)
            {
                if (runtimeTags[i] != null && runtimeTags[i].tag.StartsWith("stance_"))
                    runtimeTags.RemoveAt(i);
            }
            SetTag("stance_" + value);
        }
    }

    public bool HasTag(string tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        PruneExpiredTags();
        return FindTagEntry(tag) != null;
    }

    public bool TryGetTagTiming(string tag, out float remainingSeconds, out float durationSeconds)
    {
        remainingSeconds = 0f;
        durationSeconds = 0f;

        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        PruneExpiredTags();
        RuntimeTagEntry entry = FindTagEntry(tag);
        if (entry == null || !entry.hasExpiry)
        {
            return false;
        }

        remainingSeconds = Mathf.Max(entry.expiresAt - Time.time, 0f);
        durationSeconds = Mathf.Max(entry.durationSeconds, remainingSeconds);
        return remainingSeconds > 0f;
    }

    public bool SetTag(string tag, float duration = 0f)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        RuntimeTagEntry entry = FindTagEntry(tag);
        bool isNew = entry == null;
        if (entry == null)
        {
            entry = new RuntimeTagEntry { tag = tag };
            runtimeTags.Add(entry);
        }

        bool nextHasExpiry = duration > 0f;
        float nextExpiresAt = nextHasExpiry ? Time.time + duration : 0f;
        float nextDurationSeconds = nextHasExpiry ? duration : 0f;
        bool changed = isNew
            || entry.hasExpiry != nextHasExpiry
            || !Mathf.Approximately(entry.durationSeconds, nextDurationSeconds)
            || (nextHasExpiry && !Mathf.Approximately(entry.expiresAt, nextExpiresAt));

        entry.hasExpiry = nextHasExpiry;
        entry.expiresAt = nextExpiresAt;
        entry.durationSeconds = nextDurationSeconds;

        if (changed)
        {
            OnStateChanged?.Invoke();
        }

        return changed;
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

    public bool TransferCounter(string sourceId, int cost, string destinationId, int reward = 1)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(destinationId)
            || sourceId == destinationId || cost <= 0 || reward <= 0 || GetCounter(sourceId) < cost)
            return false;
        ConsumeCounter(sourceId, cost);
        AddCounter(destinationId, reward);
        return true;
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
