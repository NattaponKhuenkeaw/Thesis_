#if UNITY_EDITOR
using System;
using System.Linq;
using Project.TeamBuilder.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TeamBuilderInventoryAuthoring
{
    public const string PrefabPath = "Assets/Project/Prefab/UI/TeamCanvas.prefab";
    private const string CharacterCardPath = "Assets/Project/Prefab/UI/TeamCharacterInventoryCard.prefab";
    private const string ArtifactCardPath = "Assets/Project/Prefab/UI/TeamArtifactInventoryCard.prefab";
    private static readonly Color Background = new Color(.035f, .05f, .08f, .99f);
    private static readonly Color Surface = new Color(.065f, .085f, .13f);
    private static readonly Color Accent = new Color(1f, .9f, .15f);
    private const string UIArt = "Assets/Project/UI/MainMenu/";

    [MenuItem("Tools/Neo-JJK/UI/Author Artifact Owner Portraits")]
    public static void AuthorArtifactOwnerPortraits()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author outside Play Mode.");
        foreach (string path in new[] { ArtifactCardPath, "Assets/Project/Prefab/UI/ItemListIcon.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.GetComponent<ArtifactGridItemUI>().SetEquippedOwner(null);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    [MenuItem("Tools/Neo-JJK/UI/Apply TeamBuilder Inventory Layout")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply the layout outside Play Mode.");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var builder = root.GetComponentInChildren<TeamBuilderUI>(true);
            var inventory = Read<InventoryBuilderPanel>(builder, "inventoryPanel");
            var oldPreview = Read<ArtifactPreviewPanel>(builder, "artifactPreviewPanel");
            var statPrefab = Read<GameObject>(oldPreview, "statItemPrefab");
            var canvas = root.GetComponent<Canvas>();
            var teamSlotViews = builder.GetComponentsInChildren<TeamSlotUI>(true);
            Transform common = teamSlotViews[0].transform.parent;
            while (teamSlotViews.Any(slot => !slot.transform.IsChildOf(common))) common = common.parent;
            var slotsRoot = (RectTransform)common;
            slotsRoot.anchorMin = Vector2.zero; slotsRoot.anchorMax = Vector2.one;
            slotsRoot.offsetMin = new Vector2(24, 40); slotsRoot.offsetMax = new Vector2(-24, -100);
            var slotLayout = slotsRoot.GetComponent<HorizontalLayoutGroup>() ?? slotsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            slotLayout.childControlWidth = slotLayout.childControlHeight = true;
            slotLayout.childForceExpandWidth = slotLayout.childForceExpandHeight = false;
            slotLayout.childAlignment = TextAnchor.MiddleCenter; slotLayout.spacing = 30;
            var font = root.GetComponentsInChildren<TMP_Text>(true).First(t => t.font != null).font;
            foreach (var slot in teamSlotViews)
            {
                Transform wrapper = slot.transform;
                while (wrapper.parent != common) wrapper = wrapper.parent;
                if (PrefabUtility.IsAnyPrefabInstanceRoot(wrapper.gameObject))
                    PrefabUtility.UnpackPrefabInstance(wrapper.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                if (wrapper != slot.transform)
                    foreach (var oldLayout in wrapper.GetComponents<LayoutGroup>()) Object.DestroyImmediate(oldLayout);
                var sizing = wrapper.GetComponent<LayoutElement>() ?? wrapper.gameObject.AddComponent<LayoutElement>();
                sizing.minWidth = sizing.preferredWidth = 442; sizing.flexibleWidth = 0;
                sizing.minHeight = sizing.preferredHeight = 850; sizing.flexibleHeight = 0;
                if (wrapper != slot.transform) Stretch((RectTransform)slot.transform, 0, 0, 0, 0);
                BuildTeamSlot(slot, font, Array.IndexOf(teamSlotViews, slot));
            }
            statPrefab = CompactStatRow(statPrefab, font);
            var characterCard = CreateCard(font, true);
            var artifactCard = CreateCard(font, false);

            // Keep the inventory object and controller identity so existing prefab bindings survive.
            foreach (Transform child in inventory.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            foreach (var layout in inventory.GetComponents<LayoutGroup>()) Object.DestroyImmediate(layout);
            var inventoryRect = (RectTransform)inventory.transform;
            inventoryRect.anchorMin = Vector2.zero; inventoryRect.anchorMax = new Vector2(0f, 1f);
            inventoryRect.pivot = new Vector2(0f, .5f); inventoryRect.anchoredPosition = Vector2.zero;
            inventoryRect.sizeDelta = new Vector2(650f, 0f);
            inventory.GetComponent<Image>().sprite = Sprite("UI_BlackBox_YellowBorder.PNG");
            inventory.GetComponent<Image>().type = Image.Type.Sliced;
            inventory.GetComponent<Image>().color = Color.white;
            var edge = inventory.GetComponent<Outline>();
            if (edge != null) Object.DestroyImmediate(edge);
            var motion = builder.GetComponent<TeamBuilderInventoryMotion>() ?? builder.gameObject.AddComponent<TeamBuilderInventoryMotion>();
            Set(motion, "inventoryRoot", inventoryRect); Set(motion, "inventoryGroup", inventory.GetComponent<CanvasGroup>());
            Set(motion, "teamSlotsRoot", slotsRoot); Set(inventory, "visibilityMotion", motion);

            var title = Text(inventory.transform, "Title", "SELECT CHARACTER", font, 31, FontStyles.Bold);
            Box(title.rectTransform, 24, 16, 540, 44);
            var close = Button(inventory.transform, "CloseInventory", "X", font, false);
            Box((RectTransform)close.transform, 586, 18, 40, 40);
            var characterTab = Button(inventory.transform, "CharacterTab", "CHARACTER", font, true);
            Box((RectTransform)characterTab.transform, 24, 70, 220, 36);
            var artifactTab = Button(inventory.transform, "ArtifactTab", "ARTIFACT", font, false);
            Box((RectTransform)artifactTab.transform, 258, 70, 220, 36);
            var context = Text(inventory.transform, "SelectionContext", "TEAM SLOT 01", font, 20, FontStyles.Bold);
            Box(context.rectTransform, 24, 114, 580, 28);

            var characterRoot = Category(inventory.transform, "CharacterInventory");
            var characters = characterRoot.gameObject.AddComponent<CharacterGridInventoryUI>();
            var charScroll = Scroll(characterRoot, "CharacterGrid");
            var charPreview = CharacterPreview(characterRoot, font);
            Set(characters, "gridParent", charScroll.content); Set(characters, "gridItemPrefab", characterCard);
            Set(characters, "rootCanvas", canvas); Set(characters, "previewPanel", charPreview);

            var artifactRoot = Category(inventory.transform, "ArtifactInventory");
            var artifacts = artifactRoot.gameObject.AddComponent<ArtifactGridPanel>();
            var toolbar = Rect(artifactRoot, "ArtifactTools");
            PreferredHeight(toolbar, 92);
            var search = Search(toolbar, font); Box((RectTransform)search.transform, 0, 0, 602, 40);
            var sort = Dropdown(toolbar, font); Box((RectTransform)sort.transform, 0, 50, 602, 36);
            var sortRect = (RectTransform)sort.transform;
            sortRect.anchorMax = Vector2.one;
            sortRect.sizeDelta = new Vector2(0f, 36f);
            Set(artifacts, "searchInput", search); Set(artifacts, "sortDropdown", sort);
            var artScroll = Scroll(artifactRoot, "ArtifactGrid");
            var artPreview = ArtifactPreview(artifactRoot, font, statPrefab);
            Set(artifacts, "previewPanel", artPreview);
            Set(artifacts, "gridParent", artScroll.content); Set(artifacts, "gridItemPrefab", artifactCard); Set(artifacts, "rootCanvas", canvas);
            Set(builder, "artifactPreviewPanel", artPreview);
            Set(inventory, "characterGridPanel", characters); Set(inventory, "artifactGridPanel", artifacts);
            Set(inventory, "titleText", title); Set(inventory, "contextText", context);
            Set(inventory, "characterTab", characterTab); Set(inventory, "artifactTab", artifactTab); Set(inventory, "closeButton", close);
            Set(inventory, "activeTabSprite", Sprite("UI_Mini_YellowBox.PNG"));
            Set(inventory, "inactiveTabSprite", Sprite("UI_Mini_GreyBox.PNG"));

            characterRoot.gameObject.SetActive(false); artifactRoot.gameObject.SetActive(false);
            charPreview.gameObject.SetActive(false); artPreview.gameObject.SetActive(false);
            inventory.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("TeamBuilder inventory layout authored.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static GameObject CreateCard(TMP_FontAsset font, bool character)
    {
        var root = Rect(null, character ? "TeamCharacterInventoryCard" : "TeamArtifactInventoryCard");
        root.sizeDelta = new Vector2(180, 180);
        var frame = root.gameObject.AddComponent<Image>(); frame.color = new Color(.35f, .4f, .5f);
        frame.sprite = Sprite("UI_BlackBox_GreyBorder_NoOutLine.PNG"); frame.type = Image.Type.Sliced;
        root.gameObject.AddComponent<CanvasGroup>();
        var iconRect = Rect(root, "Portrait"); Stretch(iconRect, 12, 12, 12, 56);
        var icon = iconRect.gameObject.AddComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
        var name = Text(root, "ItemName", "", font, 17, FontStyles.Bold);
        Bottom(name.rectTransform, 7, 26, 7, 26); name.alignment = TextAlignmentOptions.Center;
        var badge = Text(root, "EquippedBadge", "IN TEAM", font, 14, FontStyles.Bold);
        Bottom(badge.rectTransform, 7, 52, 7, 22); badge.color = Accent;
        badge.alignment = TextAlignmentOptions.Right;
        if (character)
        {
            var summary = Text(root, "Summary", "", font, 15); Bottom(summary.rectTransform, 7, 6, 7, 20);
            summary.alignment = TextAlignmentOptions.Center;
            var cell = root.gameObject.AddComponent<CharacterGridItemUI>();
            Set(cell, "iconImage", icon); Set(cell, "frameImage", frame); Set(cell, "nameText", name);
            Set(cell, "levelText", summary); Set(cell, "equippedText", badge); SetFloat(cell, "inTeamAlpha", .7f);
            Set(cell, "selectedFrameSprite", Sprite("UI_BlackBox_YellowBorder_NoOutLine.PNG"));
            Set(cell, "normalFrameSprite", frame.sprite);
            SetColor(cell, "normalFrameColor", Color.white); SetColor(cell, "inTeamFrameColor", Color.white);
        }
        else
        {
            Bottom(name.rectTransform, 7, 6, 7, 46);
            name.alignment = TextAlignmentOptions.Center;
            var cell = root.gameObject.AddComponent<ArtifactGridItemUI>();
            Set(cell, "iconImage", icon); Set(cell, "frameImage", frame); Set(cell, "nameText", name);
            Set(cell, "equippedBadge", badge.gameObject);
            Set(cell, "selectedFrameSprite", Sprite("UI_BlackBox_YellowBorder_NoOutLine.PNG"));
            Set(cell, "normalFrameSprite", frame.sprite);
            SetColor(cell, "normalFrameColor", Color.white);
            cell.SetEquippedOwner(null);
            badge.text = "EQUIPPED";
        }
        badge.gameObject.SetActive(false);
        try { return PrefabUtility.SaveAsPrefabAsset(root.gameObject, character ? CharacterCardPath : ArtifactCardPath); }
        finally { Object.DestroyImmediate(root.gameObject); }
    }

    private static RectTransform Category(Transform parent, string name)
    {
        var root = Rect(parent, name); Stretch(root, 24, 150, 24, 24);
        var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14; layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        return root;
    }

    private static ScrollRect Scroll(Transform parent, string name)
    {
        var root = Rect(parent, name);
        var element = root.gameObject.AddComponent<LayoutElement>(); element.minHeight = 260; element.flexibleHeight = 1;
        var border = root.gameObject.AddComponent<Image>(); border.sprite = Sprite("UI_BlackBox_GreyBorder_NoOutLine.PNG"); border.type = Image.Type.Sliced;
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45;
        var viewport = Rect(root, "Viewport"); Stretch(viewport, 10, 10, 18, 10); viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect(viewport, "Content"); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3; grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.cellSize = new Vector2(180, 180); grid.spacing = new Vector2(10, 10); grid.padding = new RectOffset(6, 6, 6, 6);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var responsive = content.gameObject.AddComponent<TeamInventoryGridLayout>(); Set(responsive, "viewport", viewport);
        var track = Rect(root, "Scrollbar"); track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
        track.pivot = Vector2.one; track.sizeDelta = new Vector2(6, -20); track.anchoredPosition = new Vector2(-6, -10);
        track.gameObject.AddComponent<Image>().color = new Color(.12f, .16f, .22f);
        var handle = Rect(track, "Handle"); Stretch(handle, 0, 0, 0, 0); handle.gameObject.AddComponent<Image>().color = Accent;
        var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.viewport = viewport; scroll.content = content; scroll.verticalScrollbar = scrollbar;
        return scroll;
    }

    private static CharacterPreviewPanel CharacterPreview(Transform parent, TMP_FontAsset font)
    {
        var root = Preview(parent, "CharacterPreview", 310);
        var panel = root.gameObject.AddComponent<CharacterPreviewPanel>();
        var name = Text(root, "CharacterName", "", font, 28, FontStyles.Bold); Box(name.rectTransform, 14, 10, 570, 40);
        var stats = Text(root, "CharacterStats", "", font, 25); Box(stats.rectTransform, 14, 62, 570, 108);
        var assigned = Text(root, "Assignment", "", font, 20); Box(assigned.rectTransform, 14, 196, 570, 30);
        var button = Button(root, "AssignCharacter", "ASSIGN CHARACTER", font, true); Box((RectTransform)button.transform, 14, 246, 574, 48);
        Set(panel, "characterNameText", name); Set(panel, "statsText", stats); Set(panel, "assignmentText", assigned);
        Set(panel, "assignButton", button); Set(panel, "assignLabel", button.GetComponentInChildren<TMP_Text>());
        return panel;
    }

    private static ArtifactPreviewPanel ArtifactPreview(Transform parent, TMP_FontAsset font, GameObject statPrefab)
    {
        var root = Preview(parent, "ArtifactPreview", 360);
        var panel = root.gameObject.AddComponent<ArtifactPreviewPanel>();
        var name = Text(root, "ArtifactName", "", font, 28, FontStyles.Bold); Box(name.rectTransform, 14, 10, 570, 40);
        var iconRoot = Rect(root, "ArtifactIcon"); Box(iconRoot, 14, 62, 82, 82);
        var icon = iconRoot.gameObject.AddComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
        var stats = Rect(root, "Stats"); Box(stats, 110, 58, 476, 140);
        var layout = stats.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlHeight = true; layout.childForceExpandHeight = false;
        layout.childControlWidth = layout.childForceExpandWidth = true;
        var description = Text(root, "Description", "", font, 19); Box(description.rectTransform, 14, 204, 574, 90);
        var button = Button(root, "EquipArtifact", "EQUIP", font, true); Box((RectTransform)button.transform, 14, 300, 574, 46);
        Set(panel, "artifactNameText", name); Set(panel, "artifactIcon", icon); Set(panel, "statItemContainer", stats);
        Set(panel, "statItemPrefab", statPrefab); Set(panel, "descriptionText", description);
        Set(panel, "equipButton", button); Set(panel, "equipLabel", button.GetComponentInChildren<TMP_Text>());
        return panel;
    }

    private static RectTransform Preview(Transform parent, string name, float height)
    {
        var root = Rect(parent, name); PreferredHeight(root, height);
        var background = root.gameObject.AddComponent<Image>(); background.color = Surface;
        background.sprite = Sprite("UI_BlackBox_GreyBorder_NoOutLine.PNG"); background.type = Image.Type.Sliced;
        background.color = Color.white;
        var line = Rect(root, "SelectionLine"); Box(line, 0, 0, 602, 2); line.gameObject.AddComponent<Image>().color = Accent;
        return root;
    }

    private static TMP_InputField Search(Transform parent, TMP_FontAsset font)
    {
        var root = Rect(parent, "Search"); root.gameObject.AddComponent<Image>().color = Surface;
        var field = root.gameObject.AddComponent<TMP_InputField>();
        var area = Rect(root, "TextArea"); Stretch(area, 12, 2, 12, 2); area.gameObject.AddComponent<RectMask2D>();
        var text = Text(area, "Text", "", font, 21); Stretch(text.rectTransform, 0, 0, 0, 0);
        var hint = Text(area, "Placeholder", "Search artifacts...", font, 21); Stretch(hint.rectTransform, 0, 0, 0, 0);
        hint.color = new Color(.55f, .61f, .7f); field.textViewport = area; field.textComponent = text; field.placeholder = hint;
        return field;
    }

    private static TMP_Dropdown Dropdown(Transform parent, TMP_FontAsset font)
    {
        var root = Rect(parent, "Sort"); root.gameObject.AddComponent<Image>().color = Surface;
        var dropdown = root.gameObject.AddComponent<TMP_Dropdown>();
        var caption = Text(root, "Caption", "Newest", font, 20); Stretch(caption.rectTransform, 12, 0, 12, 0); dropdown.captionText = caption;
        var template = Rect(root, "Template"); template.anchorMin = template.anchorMax = new Vector2(.5f, 0);
        template.pivot = new Vector2(.5f, 1); template.sizeDelta = new Vector2(336, 240);
        template.gameObject.AddComponent<Image>().color = Surface;
        var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
        var viewport = Rect(template, "Viewport"); Stretch(viewport, 0, 0, 0, 0); viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect(viewport, "Content"); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, 36);
        var item = Rect(content, "Item"); Stretch(item, 0, 0, 0, 0);
        var toggle = item.gameObject.AddComponent<Toggle>(); var itemImage = item.gameObject.AddComponent<Image>();
        itemImage.color = new Color(.2f, .24f, .32f); toggle.targetGraphic = itemImage;
        var label = Text(item, "Label", "Sort", font, 20); Stretch(label.rectTransform, 12, 0, 8, 0);
        dropdown.template = template; dropdown.itemText = label; scroll.viewport = viewport; scroll.content = content;
        template.gameObject.SetActive(false);
        return dropdown;
    }

    private static Button Button(Transform parent, string name, string label, TMP_FontAsset font, bool accent)
    {
        var root = Rect(parent, name); var image = root.gameObject.AddComponent<Image>(); image.color = accent ? Accent : new Color(.17f, .21f, .29f);
        image.sprite = Sprite(accent ? "UI_Mini_YellowBox.PNG" : "UI_Mini_GreyBox.PNG"); image.type = Image.Type.Sliced;
        image.color = Color.white;
        var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        root.gameObject.AddComponent<UIHoverFallbackFeedback>();
        var text = Text(root, "Label", label, font, 21, FontStyles.Bold); Stretch(text.rectTransform, 4, 0, 4, 0);
        text.alignment = TextAlignmentOptions.Center; text.color = accent ? Background : Color.white;
        return button;
    }

    private static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, float size, FontStyles style = FontStyles.Normal)
    {
        var root = Rect(parent, name); var text = root.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size; text.fontStyle = style; text.color = Color.white;
        text.raycastTarget = false; text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    private static RectTransform Rect(Transform parent, string name)
    {
        var root = new GameObject(name, typeof(RectTransform)); root.layer = 5; root.transform.SetParent(parent, false);
        return (RectTransform)root.transform;
    }
    private static void PreferredHeight(RectTransform rect, float height)
    {
        var layout = rect.gameObject.AddComponent<LayoutElement>(); layout.minHeight = layout.preferredHeight = height; layout.flexibleHeight = 0;
    }
    private static void Box(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
    private static void Bottom(RectTransform rect, float left, float bottom, float right, float height)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(.5f, 0);
        rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, bottom + height);
    }
    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }
    private static T Read<T>(Object target, string field) where T : Object => (T)new SerializedObject(target).FindProperty(field).objectReferenceValue;
    private static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetColor(Object target, string field, Color value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).colorValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(UIArt + name);

    private static void BuildTeamSlot(TeamSlotUI slot, TMP_FontAsset font, int index)
    {
        foreach (Transform child in slot.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        var root = (RectTransform)slot.transform;
        var background = slot.GetComponent<Image>() ?? slot.gameObject.AddComponent<Image>();
        background.sprite = Sprite("UI_BlackBox_YellowBorder.PNG"); background.type = Image.Type.Sliced; background.color = Color.white;
        var title = Text(root, "TeamSlot", $"TEAM SLOT {index + 1:00}", font, 22, FontStyles.Bold); Box(title.rectTransform, 20, 16, 402, 30); title.color = Accent;
        var active = Text(root, "ActiveCharacter", "ACTIVE", font, 20, FontStyles.Bold); Box(active.rectTransform, 282, 16, 140, 30);
        active.alignment = TextAlignmentOptions.MidlineRight; active.color = Accent; active.gameObject.SetActive(false);
        var name = Text(root, "CharacterName", "", font, 28, FontStyles.Bold); Box(name.rectTransform, 20, 48, 402, 38);
        var portrait = Rect(root, "CharacterPortrait"); Stretch(portrait, 16, 94, 16, 338);
        var image = portrait.gameObject.AddComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
        var level = Text(root, "Level", "", font, 19); Bottom(level.rectTransform, 20, 370, 20, 26);
        var change = Button(root, "ChangeCharacterButton", "Change Character", font, true); Bottom((RectTransform)change.transform, 20, 316, 20, 44);
        var artifactSlots = new ArtifactSlotUI[3];
        for (int i = 0; i < 3; i++)
        {
            var item = Rect(root, "ArtifactSlot" + (i + 1));
            item.anchorMin = new Vector2(i / 3f, 0); item.anchorMax = new Vector2((i + 1) / 3f, 0);
            item.offsetMin = new Vector2(i == 0 ? 20 : 6, 184); item.offsetMax = new Vector2(i == 2 ? -20 : -6, 296);
            var frame = item.gameObject.AddComponent<Image>(); frame.sprite = Sprite("UI_BlackBox_YellowBorder_NoOutLine.PNG"); frame.type = Image.Type.Sliced;
            var iconRoot = Rect(item, "ArtifactIcon"); Stretch(iconRoot, 10, 10, 10, 10);
            var icon = iconRoot.gameObject.AddComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
            var artifactSlot = item.gameObject.AddComponent<ArtifactSlotUI>(); Set(artifactSlot, "artifactIcon", icon); Set(artifactSlot, "slotBackground", frame);
            SetColor(artifactSlot, "emptySlotColor", Color.white); SetColor(artifactSlot, "filledSlotColor", Color.white);
            artifactSlots[i] = artifactSlot;
        }
        var stats = Rect(root, "Stats"); Bottom(stats, 20, 20, 20, 144);
        var panel = stats.gameObject.AddComponent<Image>(); panel.sprite = Sprite("UI_BlackBox_GreyBorder_NoOutLine.PNG"); panel.type = Image.Type.Sliced;
        var atk = Text(stats, "Attack", "", font, 23); Box(atk.rectTransform, 14, 12, 374, 34);
        var def = Text(stats, "Defense", "", font, 23); Box(def.rectTransform, 14, 54, 374, 34);
        var hp = Text(stats, "Health", "", font, 23); Box(hp.rectTransform, 14, 96, 374, 34);
        atk.alignment = def.alignment = hp.alignment = TextAlignmentOptions.MidlineRight;
        foreach (var entry in new[] { ("ATK", 12f), ("DEF", 54f), ("HP", 96f) })
        {
            var label = Text(stats, entry.Item1 + "Label", entry.Item1, font, 23); Box(label.rectTransform, 14, entry.Item2, 100, 34);
        }
        Set(slot, "profileImage", image); Set(slot, "characterNameText", name); Set(slot, "levelText", level); Set(slot, "changeCharacterButton", change);
        Set(slot, "atkText", atk); Set(slot, "defText", def); Set(slot, "hpText", hp);
        Set(slot, "emptyStateOverlay", null); Set(slot, "activeIndicator", active.gameObject); Set(slot, "lockedOverlay", null); Set(slot, "padlockOverlay", null); Set(slot, "elementIcon", null);
        Set(slot, "emptyPortraitSprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/UI/UI_CharacterPF.PNG"));
        var serialized = new SerializedObject(slot); var items = serialized.FindProperty("artifactSlots"); items.arraySize = 3;
        for (int i = 0; i < 3; i++) items.GetArrayElementAtIndex(i).objectReferenceValue = artifactSlots[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CompactStatRow(GameObject source, TMP_FontAsset font)
    {
        var row = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            PrefabUtility.UnpackPrefabInstance(row, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            row.name = "TeamInventoryStatRow";
            ((RectTransform)row.transform).sizeDelta = new Vector2(476, 32);
            var layout = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 32;
            var ui = row.GetComponent<StatItemUI>();
            foreach (var image in row.GetComponentsInChildren<Image>(true))
            {
                image.sprite = Sprite("UI_BlackBox.PNG"); image.type = Image.Type.Sliced;
            }
            var name = Read<TMP_Text>(ui, "statNameText"); var value = Read<TMP_Text>(ui, "statValueText");
            foreach (var text in new[] { name, value })
            {
                text.font = font; text.fontSize = 21; text.enableAutoSizing = false;
                text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = Vector2.zero; text.rectTransform.offsetMax = Vector2.zero;
            }
            name.alignment = TextAlignmentOptions.MidlineLeft; value.alignment = TextAlignmentOptions.MidlineRight;
            ui.FitToContainer();
            ((RectTransform)row.transform).sizeDelta = new Vector2(476, 32);
            return PrefabUtility.SaveAsPrefabAsset(row, "Assets/Project/Prefab/UI/TeamInventoryStatRow.prefab");
        }
        finally { Object.DestroyImmediate(row); }
    }
}
#endif
