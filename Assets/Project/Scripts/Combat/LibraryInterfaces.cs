using System.Collections.Generic;

namespace Project.Combat
{
    public interface IDefinitionLibrary<TKey, TValue>
    {
        TValue GetDefinition(TKey key);
        bool TryGetDefinition(TKey key, out TValue value);
    }

    public interface IVFXLibrary : IDefinitionLibrary<VFXType, VFXDefinition>
    {
    }

    public interface IProjectileLibrary : IDefinitionLibrary<ProjectileType, ProjectileData>
    {
    }

    [System.Serializable]
    public abstract class CategorizedLibrarySection<TEntry>
    {
        public string categoryName = "Default";
        public List<TEntry> entries = new List<TEntry>();
    }

    public abstract class CategorizedLibraryAsset<TKey, TSection, TEntry, TValue> : UnityEngine.ScriptableObject, IDefinitionLibrary<TKey, TValue>
        where TSection : CategorizedLibrarySection<TEntry>
        where TEntry : class
    {
        [UnityEngine.SerializeField] private List<TSection> categories = new List<TSection>();

        private readonly Dictionary<TKey, TValue> cache = new Dictionary<TKey, TValue>();
        private bool cacheDirty = true;

        public IReadOnlyList<TSection> Categories => categories;

        public TValue GetDefinition(TKey key)
        {
            return TryGetDefinition(key, out TValue value) ? value : default;
        }

        public bool TryGetDefinition(TKey key, out TValue value)
        {
            RebuildCacheIfNeeded();
            return cache.TryGetValue(key, out value);
        }

        public bool CategoryMatchesSearch(int categoryIndex, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            if (categoryIndex < 0 || categoryIndex >= categories.Count)
            {
                return false;
            }

            string normalized = search.Trim().ToLowerInvariant();
            TSection category = categories[categoryIndex];
            if (category == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(category.categoryName)
                && category.categoryName.ToLowerInvariant().Contains(normalized))
            {
                return true;
            }

            if (category.entries == null)
            {
                return false;
            }

            for (int i = 0; i < category.entries.Count; i++)
            {
                if (EntryMatchesSearch(category.entries[i], normalized))
                {
                    return true;
                }
            }

            return false;
        }

        protected virtual void OnEnable()
        {
            cacheDirty = true;
        }

        protected virtual void OnValidate()
        {
            cacheDirty = true;
        }

        protected abstract bool IsValidKey(TKey key);
        protected abstract TKey GetEntryKey(TEntry entry);
        protected abstract TValue GetEntryValue(TEntry entry);
        protected abstract bool EntryMatchesSearch(TEntry entry, string normalizedSearch);

        private void RebuildCacheIfNeeded()
        {
            if (!cacheDirty)
            {
                return;
            }

            cacheDirty = false;
            cache.Clear();

            for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                TSection category = categories[categoryIndex];
                if (category == null || category.entries == null)
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < category.entries.Count; entryIndex++)
                {
                    TEntry entry = category.entries[entryIndex];
                    if (entry == null)
                    {
                        continue;
                    }

                    TKey key = GetEntryKey(entry);
                    if (!IsValidKey(key))
                    {
                        continue;
                    }

                    cache[key] = GetEntryValue(entry);
                }
            }
        }
    }
}
