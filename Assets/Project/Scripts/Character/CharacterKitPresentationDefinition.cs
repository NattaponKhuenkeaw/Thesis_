using System;
using System.Collections.Generic;

[Serializable]
public sealed class CharacterKitPresentationDefinition
{
    public List<Section> sections = new List<Section>();
    public List<Entry> entries = new List<Entry>();

    [Serializable]
    public sealed class Section
    {
        public string sectionId;
        public string title;
        public bool showPotentialLevel;
        public PotentialTrack potentialTrack;
        [UnityEngine.TextArea] public string summary;
        public int order;
    }

    [Serializable]
    public sealed class Entry
    {
        public string entryId;
        public string sectionId;
        public string actionId;
        public bool passive;
        public bool qte;
        public bool hidden;
        [UnityEngine.HideInInspector] public bool hideWhenLocked;
        [UnityEngine.Tooltip("Additional presentation unlock for actions granted through an effect rather than a direct action condition. Zero keeps the action's existing Potential requirements.")]
        [UnityEngine.Range(0, CharacterKitDefinition.MaxPotentialLevel)] public int minimumPotentialLevel;
        public PotentialTrack unlockTrack;
        [UnityEngine.HideInInspector] public bool showPotentialLevel;
        [UnityEngine.HideInInspector] public PotentialTrack potentialTrack;
        public int order;
        [UnityEngine.TextArea] public string activationDescription;
    }

    [Serializable]
    public sealed class Patch
    {
        public string entryId;
        public string noteId;
        public int priority;
        public bool appendNote;
        [UnityEngine.TextArea] public string description;
    }
}
