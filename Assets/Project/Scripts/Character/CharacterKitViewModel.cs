using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterKitViewModel
{
    public IReadOnlyList<Section> Sections { get; }
    public IReadOnlyList<string> Diagnostics { get; }

    public CharacterKitViewModel(List<Section> sections, List<string> diagnostics)
    {
        Sections = sections.AsReadOnly();
        Diagnostics = diagnostics.AsReadOnly();
    }

    public sealed class Section
    {
        public string Id { get; }
        public string Title { get; }
        public string Summary { get; }
        public string LevelLabel { get; }
        public IReadOnlyList<Entry> Entries { get; }

        public Section(string id, string title, string summary, string levelLabel, List<Entry> entries)
        {
            Id = id; Title = title; Summary = summary; LevelLabel = levelLabel; Entries = entries.AsReadOnly();
        }
    }

    public sealed class Entry
    {
        public string Id { get; }
        public string ActionId { get; }
        public string Title { get; }
        public Sprite Icon { get; }
        public string Metadata { get; }
        public string Description { get; }
        public bool Locked { get; }
        public IReadOnlyList<HeaderTag> HeaderTags { get; }

        public Entry(string id, string actionId, string title, Sprite icon, string metadata, string description, bool locked,
            List<HeaderTag> headerTags = null)
        {
            Id = id; ActionId = actionId; Title = title; Icon = icon;
            Metadata = metadata; Description = description; Locked = locked;
            HeaderTags = (headerTags ?? new List<HeaderTag>()).AsReadOnly();
        }
    }

    public enum TagKind { Ground, Airborne, Both, Cooldown, Energy }

    public sealed class HeaderTag
    {
        public TagKind Kind { get; }
        public string Label { get; }
        public HeaderTag(TagKind kind, string label) { Kind = kind; Label = label; }
    }
}
