using System;
using UnityEngine;
using Project.Combat;

[Serializable]
public class ProjectileLibraryEntry
{
    public ProjectileType type = ProjectileType.None;
    public ProjectileData data;
}

[Serializable]
public class ProjectileLibraryCategory : CategorizedLibrarySection<ProjectileLibraryEntry>
{
}

[CreateAssetMenu(fileName = "ProjectileLibrary", menuName = "Combat/Projectile/Projectile Library")]
public class ProjectileLibrary : CategorizedLibraryAsset<ProjectileType, ProjectileLibraryCategory, ProjectileLibraryEntry, ProjectileData>, IProjectileLibrary
{
    protected override bool IsValidKey(ProjectileType key)
    {
        return key != ProjectileType.None;
    }

    protected override ProjectileType GetEntryKey(ProjectileLibraryEntry entry)
    {
        return entry != null ? entry.type : ProjectileType.None;
    }

    protected override ProjectileData GetEntryValue(ProjectileLibraryEntry entry)
    {
        return entry != null ? entry.data : null;
    }

    protected override bool EntryMatchesSearch(ProjectileLibraryEntry entry, string normalizedSearch)
    {
        if (entry == null)
        {
            return false;
        }

        if (entry.type.ToString().ToLowerInvariant().Contains(normalizedSearch))
        {
            return true;
        }

        return entry.data != null
            && entry.data.name.ToLowerInvariant().Contains(normalizedSearch);
    }
}
