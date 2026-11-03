using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.TeamBuilder.UI.Interfaces;

namespace Project.TeamBuilder.UI.Panels
{
    public class CharacterSkillPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Skill Entries")]
        [SerializeField] private Transform entryContainer;
        [SerializeField] private GameObject entryTemplate;

        private readonly List<SectionView> _sections = new List<SectionView>();
        private CharacterObject _character;
        private CharacterProgressionState _progression;

        public void PopulateData(CharacterItem character, ArtifactInstance[] equippedArtifacts)
        {
            _character = character != null ? character.characterObjectData : null;
            Refresh();
        }

        private void OnEnable() { Refresh(); }
        private void OnDisable() { Unsubscribe(); }

        private void Unsubscribe()
        {
            if (_progression != null) _progression.OnPotentialChanged -= PotentialChanged;
            _progression = null;
        }

        private void PotentialChanged(string characterId)
        {
            if (_character != null && (string.IsNullOrEmpty(characterId) || characterId == _character.characterId)) Refresh();
        }

        private void Refresh()
        {
            Unsubscribe();
            var progression = Project.Training.CharacterStateContext.ResolveProgression();
            if (isActiveAndEnabled && _character != null && progression != null)
            {
                _progression = progression;
                _progression.OnPotentialChanged += PotentialChanged;
            }
            var model = CharacterKitInfoBuilder.Build(_character, new KitInfoContext(progression));
            BindModel(model);
        }

        private void BindModel(CharacterKitViewModel model)
        {
            if (entryContainer == null || entryTemplate == null) return;
            var source = entryTemplate.GetComponentInChildren<TMP_Text>(true);
            while (_sections.Count < model.Sections.Count)
                _sections.Add(new SectionView(entryContainer, entryTemplate, source));
            for (int i = 0; i < _sections.Count; i++)
            {
                _sections[i].Root.SetActive(i < model.Sections.Count);
                if (i < model.Sections.Count) _sections[i].Bind(model.Sections[i], entryTemplate);
            }
            if (entryContainer is RectTransform rect) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (string diagnostic in model.Diagnostics) Debug.LogWarning("Kit information: " + diagnostic, this);
#endif
        }

        private sealed class SectionView
        {
            public GameObject Root { get; }
            private readonly TMP_Text _title;
            private readonly TMP_Text _summary;
            private readonly TMP_Text _level;
            private readonly Transform _children;
            private readonly List<EntryView> _entries = new List<EntryView>();

            public SectionView(Transform parent, GameObject template, TMP_Text style)
            {
                Root = new GameObject("KitSection", typeof(RectTransform), typeof(VerticalLayoutGroup));
                Root.layer = parent.gameObject.layer;
                Root.transform.SetParent(parent, false);
                var layout = Root.GetComponent<VerticalLayoutGroup>();
                layout.spacing = 12;
                layout.padding = new RectOffset(12, 12, 12, 12);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                var background = Root.AddComponent<Image>();
                var original = template.GetComponent<Image>();
                if (original != null) { background.sprite = original.sprite; background.type = original.type; }
                background.color = new Color(1, 1, 1, .65f);
                background.raycastTarget = false;
                var header = new GameObject("SectionHeader", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                header.layer = Root.layer;
                header.transform.SetParent(Root.transform, false);
                var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
                headerLayout.childControlWidth = headerLayout.childControlHeight = true;
                headerLayout.childForceExpandWidth = headerLayout.childForceExpandHeight = false;
                headerLayout.childAlignment = TextAnchor.MiddleLeft;
                _title = Text(header.transform, "SectionTitle", style, 28);
                _title.fontStyle = FontStyles.Bold;
                _title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                _level = Text(header.transform, "SectionLevel", style, 22);
                _level.color = new Color(1, .89f, .12f);
                _level.fontStyle = FontStyles.Bold;
                _level.textWrappingMode = TextWrappingModes.NoWrap;
                _summary = Text(Root.transform, "SectionSummary", style, 21);
                var children = new GameObject("Actions", typeof(RectTransform), typeof(VerticalLayoutGroup));
                children.layer = Root.layer;
                children.transform.SetParent(Root.transform, false);
                var childLayout = children.GetComponent<VerticalLayoutGroup>();
                childLayout.padding = new RectOffset(12, 0, 0, 0);
                childLayout.spacing = 10;
                childLayout.childControlWidth = childLayout.childControlHeight = true;
                childLayout.childForceExpandWidth = true;
                childLayout.childForceExpandHeight = false;
                _children = children.transform;
            }

            public void Bind(CharacterKitViewModel.Section section, GameObject template)
            {
                Root.name = "KitSection_" + section.Id;
                bool nested = section.Entries.Count > 1;
                Root.GetComponent<Image>().enabled = nested;
                Root.GetComponent<VerticalLayoutGroup>().padding = nested
                    ? new RectOffset(12, 12, 12, 12) : new RectOffset();
                _children.GetComponent<VerticalLayoutGroup>().padding = nested
                    ? new RectOffset(12, 0, 0, 0) : new RectOffset();
                _title.text = section.Title;
                _level.text = section.LevelLabel;
                _level.gameObject.SetActive(!string.IsNullOrWhiteSpace(section.LevelLabel));
                _summary.text = section.Summary;
                _summary.gameObject.SetActive(!string.IsNullOrWhiteSpace(section.Summary));
                while (_entries.Count < section.Entries.Count)
                    _entries.Add(new EntryView(Object.Instantiate(template, _children)));
                for (int i = 0; i < _entries.Count; i++)
                {
                    _entries[i].Root.SetActive(i < section.Entries.Count);
                    if (i < section.Entries.Count) _entries[i].Bind(section.Entries[i]);
                }
            }

            public static TMP_Text Text(Transform parent, string name, TMP_Text style, float size)
            {
                var owner = new GameObject(name, typeof(RectTransform));
                owner.layer = parent.gameObject.layer;
                owner.transform.SetParent(parent, false);
                var text = owner.AddComponent<TextMeshProUGUI>();
                if (style != null) { text.font = style.font; text.fontSharedMaterial = style.fontSharedMaterial; text.color = style.color; }
                text.fontSize = size;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Overflow;
                text.raycastTarget = false;
                return text;
            }
        }

        private sealed class EntryView
        {
            public GameObject Root { get; }
            private readonly TMP_Text _title;
            private readonly TMP_Text _level;
            private readonly TMP_Text _metadata;
            private readonly TMP_Text _description;
            private readonly Image _icon;
            private readonly Transform _tagRoot;
            private readonly List<TagView> _tags = new List<TagView>();

            public EntryView(GameObject root)
            {
                Root = root;
                _title = root.transform.Find("SkillHeading/Title")?.GetComponent<TMP_Text>();
                _level = root.transform.Find("SkillHeading/Level")?.GetComponent<TMP_Text>();
                _icon = root.transform.Find("SkillHeading/AbilityIcon")?.GetComponent<Image>();
                _metadata = root.transform.Find("Metadata")?.GetComponent<TMP_Text>();
                _description = root.transform.Find("Description")?.GetComponent<TMP_Text>();
                var heading = root.transform.Find("SkillHeading");
                if (heading != null)
                {
                    var layout = heading.GetComponent<HorizontalLayoutGroup>() ?? heading.gameObject.AddComponent<HorizontalLayoutGroup>();
                    layout.spacing = 14;
                    layout.childAlignment = TextAnchor.MiddleLeft;
                    layout.childControlWidth = layout.childControlHeight = true;
                    layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                    var size = heading.GetComponent<LayoutElement>();
                    if (size != null) size.preferredHeight = -1;
                    if (_icon != null)
                    {
                        var iconSize = _icon.GetComponent<LayoutElement>() ?? _icon.gameObject.AddComponent<LayoutElement>();
                        iconSize.minWidth = iconSize.preferredWidth = 64;
                        iconSize.minHeight = iconSize.preferredHeight = 64;
                    }
                    if (_title != null)
                    {
                        var textRoot = new GameObject("HeadingText", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                        textRoot.layer = root.layer;
                        textRoot.transform.SetParent(heading, false);
                        textRoot.GetComponent<LayoutElement>().flexibleWidth = 1;
                        var textLayout = textRoot.GetComponent<VerticalLayoutGroup>();
                        textLayout.spacing = 6;
                        textLayout.childControlWidth = textLayout.childControlHeight = true;
                        textLayout.childForceExpandWidth = true;
                        textLayout.childForceExpandHeight = false;
                        _title.transform.SetParent(textRoot.transform, false);
                        var tags = new GameObject("HeaderTags", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                        tags.layer = root.layer;
                        tags.transform.SetParent(textRoot.transform, false);
                        var tagLayout = tags.GetComponent<HorizontalLayoutGroup>();
                        tagLayout.spacing = 6;
                        tagLayout.childControlWidth = tagLayout.childControlHeight = true;
                        tagLayout.childForceExpandWidth = tagLayout.childForceExpandHeight = false;
                        _tagRoot = tags.transform;
                    }
                }
                // The parent layout sizes this card; a nested fitter would compete with it.
                var fitter = root.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.enabled = false;
            }

            public void Bind(CharacterKitViewModel.Entry entry)
            {
                Root.name = "KitEntry_" + entry.Id;
                if (_title != null)
                {
                    _title.text = entry.Title;
                    _title.textWrappingMode = TextWrappingModes.Normal;
                    _title.overflowMode = TextOverflowModes.Overflow;
                }
                if (_level != null) _level.gameObject.SetActive(false);
                if (_tagRoot != null)
                {
                    while (_tags.Count < entry.HeaderTags.Count) _tags.Add(new TagView(_tagRoot, _title));
                    for (int i = 0; i < _tags.Count; i++)
                    {
                        _tags[i].Root.SetActive(i < entry.HeaderTags.Count);
                        if (i < entry.HeaderTags.Count) _tags[i].Bind(entry.HeaderTags[i]);
                    }
                    _tagRoot.gameObject.SetActive(entry.HeaderTags.Count > 0);
                }
                if (_icon != null) { _icon.sprite = entry.Icon; _icon.gameObject.SetActive(entry.Icon != null); }
                if (_metadata != null) { _metadata.text = entry.Metadata; _metadata.gameObject.SetActive(!string.IsNullOrWhiteSpace(entry.Metadata)); }
                if (_description != null) { _description.richText = true; _description.text = entry.Description; _description.gameObject.SetActive(!string.IsNullOrWhiteSpace(entry.Description)); }
            }
        }

        private sealed class TagView
        {
            public GameObject Root { get; }
            private readonly TMP_Text _label;
            private readonly KitHeaderTagGraphic _symbol;

            public TagView(Transform parent, TMP_Text style)
            {
                Root = new GameObject("HeaderTag", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
                Root.layer = parent.gameObject.layer;
                Root.transform.SetParent(parent, false);
                var background = Root.GetComponent<Image>();
                background.color = new Color(.2f, .24f, .34f, .8f);
                background.raycastTarget = false;
                var layout = Root.GetComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(6, 6, 3, 3);
                layout.spacing = 4;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                var symbol = new GameObject("TagSymbol", typeof(RectTransform), typeof(CanvasRenderer), typeof(LayoutElement), typeof(KitHeaderTagGraphic));
                symbol.layer = Root.layer;
                symbol.transform.SetParent(Root.transform, false);
                var size = symbol.GetComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = 16;
                size.minHeight = size.preferredHeight = 16;
                _symbol = symbol.GetComponent<KitHeaderTagGraphic>();
                _symbol.raycastTarget = false;
                _label = SectionView.Text(Root.transform, "TagLabel", style, 18);
                _label.textWrappingMode = TextWrappingModes.NoWrap;
            }

            public void Bind(CharacterKitViewModel.HeaderTag tag)
            {
                Root.name = "HeaderTag_" + tag.Kind;
                _label.text = tag.Label;
                _label.color = tag.Kind == CharacterKitViewModel.TagKind.Cooldown ? new Color(1, .89f, .12f) : Color.white;
                _symbol.color = _label.color;
                _symbol.Bind(tag.Kind);
            }
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
