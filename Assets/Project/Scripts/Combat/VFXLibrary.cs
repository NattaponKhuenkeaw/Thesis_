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

    [CreateAssetMenu(fileName = "VFXLibrary", menuName = "Combat/VFX/VFX Library")]
    public class VFXLibrary : ScriptableObject
    {
        public VFXDefinition[] entries;

        public VFXDefinition GetDefinition(VFXType type)
        {
            if (type == VFXType.None) return null;
            
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].type == type)
                    return entries[i];
            }
            
            return null;
        }
    }
}
