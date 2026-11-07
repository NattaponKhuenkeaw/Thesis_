#if UNITY_EDITOR
using System;
using System.Linq;
using Project.TeamBuilder.UI;
using Project.TeamBuilder.UI.Panels;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CharacterInspectorFolderAuthoring
{
    static CharacterInspectorFolderAuthoring()
    {
        EditorApplication.delayCall += () =>
        {
            if (System.IO.File.Exists("Temp/CharacterInspectorFolder.mcp"))
                MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost.Start();
        };
    }
    public const string CanvasPath = "Assets/Project/Prefab/UI/TeamCanvas.prefab";
    public const string RailPath = "Assets/Project/Prefab/UI/InspectorCharacterCard.prefab";
    public const string SkillPath = "Assets/Project/Prefab/UI/InspectorSkillEntry.prefab";
    private const string Art = "Assets/Project/UI/";
    private static readonly Color Ink = new Color(.035f, .045f, .08f);
    private static readonly Color Muted = new Color(.64f, .67f, .76f);
    private static readonly Color Yellow = new Color(1f, .89f, .12f);

    [MenuItem("Tools/Neo-JJK/UI/Apply Character Inspector Folder Layout")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author outside Play Mode.");
        var root = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            var inspector = root.GetComponentInChildren<InspectScreenUI>(true);
            var content = Read<GameObject>(inspector, "contentPanel");
            var font = root.GetComponentsInChildren<TMP_Text>(true).First(t => t.font != null).font;
            var potential = root.GetComponentInChildren<CharacterPotentialPanel>(true);
            var page = (RectTransform)inspector.transform;
            Stretch(page);
            inspector.gameObject.SetActive(false);
            foreach (Transform child in inspector.transform.Cast<Transform>().ToArray())
                if (child != content.transform) Object.DestroyImmediate(child.gameObject);
            foreach (Transform child in content.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            foreach (var layout in content.GetComponents<LayoutGroup>()) Object.DestroyImmediate(layout);
            Stretch((RectTransform)content.transform); content.transform.localScale = Vector3.one;
            var oldImage = content.GetComponent<Image>(); if (oldImage != null) oldImage.color = Color.clear;
            var backdrop = inspector.GetComponent<Image>() ?? inspector.gameObject.AddComponent<Image>();
            backdrop.sprite = Sprite("MainMenu/UI_BlackBox.PNG"); backdrop.type = Image.Type.Sliced; backdrop.color = Color.white;
            var wash = Panel(content.transform, "CityBackdrop", null); Stretch(wash.rectTransform);
            wash.sprite = Sprite("Cover_BG.PNG"); wash.color = new Color(.35f, .31f, .48f, .24f); wash.raycastTarget = false;
            var heading = Text(content.transform, "CharacterHeading", "CHARACTER", font, 48, true);
            Box(heading.rectTransform, 90, 16, 650, 62);
            var emblem = Panel(content.transform, "IdentityEmblem", "UI_CharacterPF.PNG"); Box(emblem.rectTransform, 28, 20, 48, 48);
            Line(content.transform, "HeaderRule", .015f, .925f, .94f, .927f, false);
            var close = Button(content.transform, "CloseInspector", "", font, false);
            Anchor((RectTransform)close.transform, .955f, .929f, .985f, .985f);
            var x = Panel(close.transform, "CloseIcon", "MainMenu/UI_X.PNG"); Inset(x.rectTransform, 10, 10, 10, 10); x.raycastTarget = false;
            var footer = Text(content.transform, "BrandFooter", "N E V E R   N I G H T     /     CHARACTER RECORD", font, 19);
            Anchor(footer.rectTransform, .02f, .012f, .7f, .047f); footer.color = Muted;
            var back = Button(content.transform, "BackInspector", "ESC  BACK", font, false);
            Anchor((RectTransform)back.transform, .865f, .012f, .985f, .052f);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(back.onClick, inspector.Close);

            var rail = Scroll(content.transform, "CharacterRail", false);
            Anchor((RectTransform)rail.transform, .012f, .09f, .098f, .905f);
            var railLayout = rail.content.gameObject.AddComponent<VerticalLayoutGroup>();
            railLayout.spacing = 22; railLayout.childControlWidth = railLayout.childControlHeight = true;
            railLayout.childForceExpandWidth = true; railLayout.childForceExpandHeight = false;
            var railCard = CreateRailCard(font);

            var identity = Rect(content.transform, "IdentityCard"); Anchor(identity, .107f, .073f, .472f, .907f);
            var border = identity.gameObject.AddComponent<Image>(); border.sprite = Sprite("MainMenu/UI_BlackBox_YellowBorder.PNG"); border.type = Image.Type.Sliced;
            var visual = identity.gameObject.AddComponent<CharacterVisualPanel>();
            var clipped = Rect(identity, "PortraitWindow"); Inset(clipped, 10, 10, 10, 10); clipped.gameObject.AddComponent<RectMask2D>();
            var city = Rect(clipped, "CityArt").gameObject.AddComponent<RawImage>(); Stretch(city.rectTransform);
            city.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Cover_BG.PNG"); city.uvRect = new Rect(.43f, 0f, .57f, 1f);
            city.color = new Color(.65f, .58f, .85f); city.raycastTarget = false;
            var portrait = Rect(clipped, "CharacterPortrait").gameObject.AddComponent<Image>();
            Stretch(portrait.rectTransform); portrait.preserveAspect = true; portrait.raycastTarget = false;
            var nameplate = Panel(identity, "IdentityLabel", "MainMenu/UI_BlackBox.PNG");
            Anchor(nameplate.rectTransform, .014f, .014f, .986f, .165f); nameplate.color = new Color(1, 1, 1, .92f);
            var name = Text(nameplate.transform, "CharacterName", "", font, 44, true); Anchor(name.rectTransform, .045f, .40f, .96f, .94f);
            var role = Text(nameplate.transform, "CharacterRole", "", font, 24, true); Anchor(role.rectTransform, .05f, .08f, .96f, .4f); role.color = Yellow;
            var roleView = role.gameObject.AddComponent<Project.UI.CharacterElementRoleUI>(); Set(roleView, "elementRoleText", role);
            Set(visual, "characterPortrait", portrait); Set(visual, "characterNameText", name); Set(visual, "elementRoleUI", roleView);

            var infoTab = Button(content.transform, "InfoTab", "INFO", font, true);
            Anchor((RectTransform)infoTab.transform, .5f, .851f, .677f, .907f);
            var skillTab = Button(content.transform, "SkillsTab", "SKILLS", font, false);
            Anchor((RectTransform)skillTab.transform, .682f, .851f, .859f, .907f);
            var fileMark = Text(content.transform, "FileMark", "PERSONNEL\nRECORD", font, 16); Anchor(fileMark.rectTransform, .875f, .852f, .978f, .904f);
            fileMark.alignment = TextAlignmentOptions.MidlineRight; fileMark.color = Muted;
            var document = Rect(content.transform, "FolderDocument"); Anchor(document, .488f, .073f, .985f, .85f);
            var group = document.gameObject.AddComponent<CanvasGroup>();
            var frame = document.gameObject.AddComponent<Image>(); frame.sprite = Sprite("MainMenu/UI_BlackBox_YellowBorder_NoOutLine.PNG"); frame.type = Image.Type.Sliced;
            Set(inspector, "folderDocument", document); Set(inspector, "folderDocumentGroup", group);
            Set(inspector, "activeFolderTabSprite", Sprite("MainMenu/UI_Mini_YellowBox.PNG")); Set(inspector, "inactiveFolderTabSprite", Sprite("MainMenu/UI_Mini_GreyBox.PNG"));

            var info = BuildInfo(document, inspector, potential, font);
            var skillRoot = Rect(document, "SkillsPage"); Inset(skillRoot, 24, 24, 24, 24);
            var skillPanel = skillRoot.gameObject.AddComponent<CharacterSkillPanel>();
            var skillScroll = Scroll(skillRoot, "SkillsScroll", false); Stretch((RectTransform)skillScroll.transform);
            var entries = skillScroll.content.gameObject.AddComponent<VerticalLayoutGroup>(); entries.padding = new RectOffset(4, 4, 4, 4);
            entries.spacing = 20; entries.childControlHeight = entries.childControlWidth = true; entries.childForceExpandHeight = false; entries.childForceExpandWidth = true;
            Set(skillPanel, "entryContainer", skillScroll.content); Set(skillPanel, "entryTemplate", CreateSkillTemplate(font));
            var equipmentRoot = Rect(document, "EquipmentPage"); Inset(equipmentRoot, 28, 24, 28, 24);
            var equipment = equipmentRoot.gameObject.AddComponent<EquipmentPanel>();
            var equipmentTitle = Text(equipmentRoot, "EquipmentTitle", "EQUIPPED ARTIFACTS", font, 30, true); Box(equipmentTitle.rectTransform, 0, 0, 720, 48);
            var returnButton = Button(equipmentRoot, "ReturnToInfo", "BACK TO INFO", font, false); Anchor((RectTransform)returnButton.transform, .65f, .93f, 1f, 1f);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(returnButton.onClick, inspector.ShowInfoPage);
            var equipmentScroll = Scroll(equipmentRoot, "EquipmentScroll", false); Anchor((RectTransform)equipmentScroll.transform, 0, 0, 1, .90f);
            var description = Text(equipmentScroll.content, "ArtifactSummary", "", font, 25); Stretch(description.rectTransform);
            description.textWrappingMode = TextWrappingModes.Normal; description.overflowMode = TextOverflowModes.Overflow;
            var equipmentLayout = equipmentScroll.content.gameObject.AddComponent<VerticalLayoutGroup>();
            equipmentLayout.childControlWidth = equipmentLayout.childControlHeight = true;
            equipmentLayout.childForceExpandWidth = true; equipmentLayout.childForceExpandHeight = false;
            Set(equipment, "artifactDescriptionText", description);

            var empty = Text(content.transform, "EmptyCharacterState", "NO CHARACTERS AVAILABLE\nAcquire a character to open their record.", font, 32, true);
            Anchor(empty.rectTransform, .15f, .3f, .85f, .7f); empty.alignment = TextAlignmentOptions.Center; empty.gameObject.SetActive(false);
            Set(inspector, "emptyStatePanel", empty.gameObject); Set(inspector, "closeButton", close);
            Set(inspector, "infoTabButton", infoTab); Set(inspector, "skillTabButton", skillTab); Set(inspector, "equipmentTabButton", null);
            Set(inspector, "characterSliderGridParent", rail.content); Set(inspector, "characterSliderItemPrefab", railCard);
            Set(inspector, "pageTransitionFadeImage", null);
            Float(inspector, "slideOffset", 28f); Float(inspector, "slideDuration", .22f);
            info.gameObject.SetActive(true); skillRoot.gameObject.SetActive(false); equipmentRoot.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
            Debug.Log("Character Inspector folder layout authored.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static CharacterInfoPanel BuildInfo(RectTransform parent, InspectScreenUI inspector, CharacterPotentialPanel potential, TMP_FontAsset font)
    {
        var root = Rect(parent, "InfoPage"); Inset(root, 32, 22, 32, 22);
        var info = root.gameObject.AddComponent<CharacterInfoPanel>();
        Set(info, "inspectScreenUI", inspector); Set(info, "characterPotentialPanel", potential.gameObject);
        var scroll = Scroll(root, "StatsScroll", false); Anchor((RectTransform)scroll.transform, 0, 0, 1, 1); scroll.GetComponent<Image>().enabled = false;
        var content = scroll.content;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 10; layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var levelRow = Rect(content, "RunLevelHeader"); Height(levelRow, 80);
        var level = Text(levelRow, "RunLevel", "", font, 48, true); Anchor(level.rectTransform, 0, .08f, .62f, 1);
        var equipment = Button(levelRow, "ViewEquipment", "EQUIPMENT", font, false); Anchor((RectTransform)equipment.transform, .68f, .25f, 1, .87f);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(equipment.onClick, inspector.ShowEquipmentPage);
        Set(info, "levelText", level);
        Section(content, "CHARACTER STATS", font);
        foreach (var entry in new[] { ("atkText", "ATK"), ("defText", "DEF"), ("hpText", "HP") })
        {
            var row = StatRow(content, entry.Item2, font, 32, 56); Set(info, entry.Item1, row);
        }
        Section(content, "SECONDARY STATS", font);
        var secondary = Rect(content, "SecondaryStats"); Height(secondary, 176);
        string[] fields = { "critRateText", "AEgainText", "critDmgText", "additionalDamageIncreaseText", "dotMasteryText", "damageIncreaseText", "cooldownMasteryText" };
        string[] labels = { "CRIT RATE", "AE RECHARGE", "CRIT DMG", "AD INCREASE", "DOT MASTERY", "DAMAGE INCREASE", "COOLDOWN MASTERY" };
        for (int i = 0; i < fields.Length; i++)
        {
            int column = i % 2, row = i / 2;
            var value = StatRow(secondary, labels[i], font, 20, 40);
            var rowRect = (RectTransform)value.transform.parent;
            rowRect.anchorMin = new Vector2(column * .51f, 1); rowRect.anchorMax = new Vector2(column * .51f + .49f, 1);
            rowRect.pivot = new Vector2(.5f, 1); rowRect.anchoredPosition = new Vector2(0, -row * 44); rowRect.sizeDelta = new Vector2(0, 40);
            Set(info, fields[i], value);
        }
        var potentialRoot = Panel(content, "PotentialSummary", "MainMenu/UI_BlackBox_YellowBorder_NoOutLine.PNG"); Height(potentialRoot.rectTransform, 180);
        var title = Text(potentialRoot.transform, "PotentialTitle", "MEMORY", font, 28, true); Box(title.rectTransform, 18, 12, 500, 38);
        var count = Text(potentialRoot.transform, "PotentialCount", "", font, 32, true); Anchor(count.rectTransform, .025f, .30f, .57f, .73f);
        var upgrade = Button(potentialRoot.transform, "PotentialButton", "MEMORY", font, true); Anchor((RectTransform)upgrade.transform, .60f, .23f, .975f, .74f);
        var segmentRoot = Rect(potentialRoot.transform, "PotentialSegments"); Anchor(segmentRoot, .025f, .05f, .55f, .25f);
        const int boxesPerRow = 9;
        var segments = new Image[Project.Progression.CharacterMemoryProgress.MajorUpgradeCount];
        for (int i = 0; i < segments.Length; i++)
        {
            var segment = Panel(segmentRoot, "Potential" + (i + 1), null);
            int column = i % boxesPerRow;
            bool topRow = i < boxesPerRow;
            Anchor(segment.rectTransform, column / (float)boxesPerRow, topRow ? .55f : 0f,
                (column + 1) / (float)boxesPerRow - .012f, topRow ? 1f : .45f);
            segment.raycastTarget = false;
            segments[i] = segment;
        }
        Set(info, "potentialText", count); Set(info, "potentialPanelButton", upgrade); Array(info, "potentialSegments", segments);
        Set(info, "potentialFilledSprite", null); Set(info, "potentialEmptySprite", null);
        return info;
    }

    private static GameObject CreateRailCard(TMP_FontAsset font)
    {
        var root = Rect(null, "InspectorCharacterCard"); root.sizeDelta = new Vector2(160, 190); Height(root, 190);
        var frame = root.gameObject.AddComponent<Image>(); frame.sprite = Sprite("MainMenu/UI_BlackBox_GreyBorder_NoOutLine.PNG"); frame.type = Image.Type.Sliced;
        var cell = root.gameObject.AddComponent<CharacterGridItemUI>();
        root.gameObject.AddComponent<UIHoverFallbackFeedback>();
        var portrait = Rect(root, "Portrait").gameObject.AddComponent<Image>(); Inset(portrait.rectTransform, 10, 10, 10, 42); portrait.preserveAspect = true; portrait.raycastTarget = false;
        var label = Text(root, "CharacterName", "", font, 19, true); Anchor(label.rectTransform, .04f, .02f, .96f, .20f); label.alignment = TextAlignmentOptions.Center;
        Set(cell, "iconImage", portrait); Set(cell, "frameImage", frame); Set(cell, "nameText", label);
        Set(cell, "normalFrameSprite", frame.sprite); Set(cell, "selectedFrameSprite", Sprite("MainMenu/UI_BlackBox_YellowBorder_NoOutLine.PNG"));
        SetColor(cell, "normalFrameColor", UnityEngine.Color.white); SetColor(cell, "inTeamFrameColor", UnityEngine.Color.white);
        try { return PrefabUtility.SaveAsPrefabAsset(root.gameObject, RailPath); }
        finally { Object.DestroyImmediate(root.gameObject); }
    }

    private static GameObject CreateSkillTemplate(TMP_FontAsset font)
    {
        var root = Rect(null, "InspectorSkillEntry"); root.sizeDelta = new Vector2(830, 280);
        var frame = root.gameObject.AddComponent<Image>(); frame.sprite = Sprite("MainMenu/UI_BlackBox_GreyBorder_NoOutLine.PNG"); frame.type = Image.Type.Sliced;
        var layout = root.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(22, 22, 18, 18); layout.spacing = 10;
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var header = Rect(root, "SkillHeading"); Height(header, 74);
        var icon = Rect(header, "AbilityIcon").gameObject.AddComponent<Image>(); Box(icon.rectTransform, 0, 0, 64, 64); icon.preserveAspect = true; icon.raycastTarget = false;
        var title = Text(header, "Title", "", font, 28, true); Anchor(title.rectTransform, .10f, .34f, 1, 1);
        var level = Text(header, "Level", "", font, 20); Anchor(level.rectTransform, .10f, 0, 1, .35f); level.color = Yellow;
        var metadata = Text(root, "Metadata", "", font, 21); metadata.color = Muted; metadata.textWrappingMode = TextWrappingModes.Normal; metadata.overflowMode = TextOverflowModes.Overflow;
        var description = Text(root, "Description", "", font, 24); description.textWrappingMode = TextWrappingModes.Normal; description.overflowMode = TextOverflowModes.Overflow;
        try { return PrefabUtility.SaveAsPrefabAsset(root.gameObject, SkillPath); }
        finally { Object.DestroyImmediate(root.gameObject); }
    }

    private static TMP_Text StatRow(Transform parent, string label, TMP_FontAsset font, float size, float height)
    {
        var root = Panel(parent, label + "Row", null); Height(root.rectTransform, height);
        root.color = new Color(.12f, .14f, .20f, 1f);
        var text = Text(root.transform, "Label", label, font, size, true); Anchor(text.rectTransform, .025f, .05f, .76f, .95f);
        text.enableAutoSizing = true; text.fontSizeMin = size * .8f; text.fontSizeMax = size;
        var value = Text(root.transform, "Value", "", font, size, true); Anchor(value.rectTransform, .43f, .05f, .98f, .95f);
        value.alignment = TextAlignmentOptions.MidlineRight;
        return value;
    }
    private static void Section(Transform parent, string title, TMP_FontAsset font)
    {
        var text = Text(parent, title, title, font, 26, true); Height(text.rectTransform, 32); text.color = Muted;
    }
    private static ScrollRect Scroll(Transform parent, string name, bool bordered)
    {
        var root = Rect(parent, name); var image = root.gameObject.AddComponent<Image>(); image.color = new Color(1, 1, 1, .001f);
        if (bordered) { image.sprite = Sprite("MainMenu/UI_BlackBox_GreyBorder_NoOutLine.PNG"); image.type = Image.Type.Sliced; image.color = UnityEngine.Color.white; }
        var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
        var viewport = Rect(root, "Viewport"); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect(viewport, "Content"); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content;
        return scroll;
    }
    private static Button Button(Transform parent, string name, string title, TMP_FontAsset font, bool active)
    {
        var image = Panel(parent, name, active ? "MainMenu/UI_Mini_YellowBox.PNG" : "MainMenu/UI_Mini_GreyBox.PNG");
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; image.gameObject.AddComponent<UIHoverFallbackFeedback>();
        var label = Text(image.transform, "Label", title, font, 25, true); Inset(label.rectTransform, 12, 0, 12, 0);
        label.alignment = TextAlignmentOptions.Center; label.color = active ? Ink : UnityEngine.Color.white;
        label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 25;
        return button;
    }
    private static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, float size, bool bold = false)
    {
        var rect = Rect(parent, name); var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; text.color = UnityEngine.Color.white; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis; text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }
    private static Image Panel(Transform parent, string name, string art)
    {
        var root = Rect(parent, name); var image = root.gameObject.AddComponent<Image>(); image.color = UnityEngine.Color.white;
        if (art != null) { image.sprite = Sprite(art); image.type = Image.Type.Sliced; }
        return image;
    }
    private static void Line(Transform parent, string name, float x1, float y1, float x2, float y2, bool accent)
    {
        var image = Panel(parent, name, accent ? "MainMenu/UI_YellowLine.PNG" : "MainMenu/UI_GreyLine.PNG"); Anchor(image.rectTransform, x1, y1, x2, y2); image.raycastTarget = false;
    }
    private static Sprite Sprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + file);
    private static RectTransform Rect(Transform parent, string name)
    {
        var root = new GameObject(name, typeof(RectTransform)); root.layer = 5; root.transform.SetParent(parent, false); return (RectTransform)root.transform;
    }
    private static void Anchor(RectTransform rect, float x1, float y1, float x2, float y2)
    {
        rect.anchorMin = new Vector2(x1, y1); rect.anchorMax = new Vector2(x2, y2); rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void Stretch(RectTransform rect) => Anchor(rect, 0, 0, 1, 1);
    private static void Inset(RectTransform rect, float left, float top, float right, float bottom)
    {
        Stretch(rect); rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }
    private static void Box(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
    private static void Height(RectTransform rect, float height)
    {
        var element = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); element.minHeight = element.preferredHeight = height; element.flexibleHeight = 0;
    }
    private static T Read<T>(Object host, string field) where T : Object => (T)new SerializedObject(host).FindProperty(field).objectReferenceValue;
    private static void Set(Object host, string field, Object value)
    {
        var serialized = new SerializedObject(host); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Float(Object host, string field, float value)
    {
        var serialized = new SerializedObject(host); serialized.FindProperty(field).floatValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetColor(Object host, string field, UnityEngine.Color value)
    {
        var serialized = new SerializedObject(host); serialized.FindProperty(field).colorValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Array(Object host, string field, Object[] values)
    {
        var serialized = new SerializedObject(host); var array = serialized.FindProperty(field); array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
