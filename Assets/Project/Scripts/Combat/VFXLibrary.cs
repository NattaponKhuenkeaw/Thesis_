using System;
using UnityEngine;
using Project.Combat;

namespace Project.Combat
{
    [Serializable]
    public class VFXDefinition
    {
        public VFXType type;
        public AnimationClip clip;
        public Vector3 localScale = Vector3.one;
        public Color tint = Color.white;
        public bool flipWithCharacter = true;
        public bool isLocalSpace = false;
        
        [Tooltip("If true, the animator speed is set to 0 during hitstop (scaled time).")]
        public bool obeyHitstop = true;
    }

    [Serializable]
    public class VFXCategory : CategorizedLibrarySection<VFXDefinition>
    {
    }

    [CreateAssetMenu(fileName = "VFXLibrary", menuName = "Combat/VFX/VFX Library")]
    public class VFXLibrary : CategorizedLibraryAsset<VFXType, VFXCategory, VFXDefinition, VFXDefinition>, IVFXLibrary
    {
        protected override bool IsValidKey(VFXType key)
        {
            return key != VFXType.None;
        }

        protected override VFXType GetEntryKey(VFXDefinition entry)
        {
            return entry != null ? entry.type : VFXType.None;
        }

        protected override VFXDefinition GetEntryValue(VFXDefinition entry)
        {
            return entry;
        }

        protected override bool EntryMatchesSearch(VFXDefinition entry, string normalizedSearch)
        {
            if (entry == null)
            {
                return false;
            }

            if (entry.type.ToString().ToLowerInvariant().Contains(normalizedSearch))
            {
                return true;
            }

            return entry.clip != null
                && entry.clip.name.ToLowerInvariant().Contains(normalizedSearch);
        }
    }
}
