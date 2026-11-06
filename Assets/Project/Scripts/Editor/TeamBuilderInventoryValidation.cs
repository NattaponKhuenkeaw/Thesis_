#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.Core.ZoneState;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TeamBuilderInventoryValidation
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int assertions;

#if UNITY_INCLUDE_TESTS
    [MenuItem("Tools/Neo-JJK/UI/Validate TeamBuilder Feedback Regression")]
    public static void RunFeedbackRegression()
    {
        typeof(ShadowMeterValidation).GetMethod("RunFixtures", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { new[] { "ShadowMeterTests", "AbilityUIFeedbackTests" }, "Logs/teambuilder-feedback-regression.xml", false });
    }
#endif

    [MenuItem("Tools/Neo-JJK/UI/Validate TeamBuilder Inventory Layout")]
    public static void Run()
    {
        assertions = 0;
        var root = PrefabUtility.LoadPrefabContents(TeamBuilderInventoryAuthoring.PrefabPath);
        var inventoryData = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            var builder = root.GetComponentInChildren<TeamBuilderUI>(true);
            var inventory = Read<InventoryBuilderPanel>(builder, "inventoryPanel");
            var characters = Read<CharacterGridInventoryUI>(inventory, "characterGridPanel");
            var artifacts = Read<ArtifactGridPanel>(inventory, "artifactGridPanel");
            var preview = Read<CharacterPreviewPanel>(characters, "previewPanel");
            var artifactPreview = Read<ArtifactPreviewPanel>(builder, "artifactPreviewPanel");
            var slots = Read<TeamSlotUI[]>(builder, "teamSlots");
            Check(!inventory.gameObject.activeSelf, "Inventory starts closed.");
            Check(!preview.gameObject.activeInHierarchy && !artifactPreview.gameObject.activeInHierarchy, "Both previews are hidden inside the closed inventory.");
            Check(artifactPreview.transform.IsChildOf(artifacts.transform), "Artifact preview belongs to its category.");
            foreach (var gridPanel in new Component[] { characters, artifacts })
            {
                var scroll = Read<Transform>(gridPanel, "gridParent").GetComponentInParent<ScrollRect>(true);
                Check(scroll.vertical && !scroll.horizontal, "Additional rows scroll vertically.");
                Check(scroll.viewport.GetComponent<RectMask2D>() != null, "Grid is clipped.");
                var grid = scroll.content.GetComponent<GridLayoutGroup>();
                Check(grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount == 3 && grid.startAxis == GridLayoutGroup.Axis.Horizontal, "Three cards per row.");
                Check(scroll.content.GetComponent<ContentSizeFitter>().verticalFit == ContentSizeFitter.FitMode.PreferredSize, "Grid grows vertically.");
            }
            Check(Read<GameObject>(characters, "gridItemPrefab").GetComponent<CharacterGridItemUI>() != null, "Character cards have their input component.");
            Check(Read<GameObject>(artifacts, "gridItemPrefab").GetComponent<ArtifactGridItemUI>() != null, "Artifact cards have their input component.");
            foreach (var image in inventory.GetComponentsInChildren<Image>(true)
                .Concat(Read<GameObject>(characters, "gridItemPrefab").GetComponentsInChildren<Image>(true))
                .Concat(Read<GameObject>(artifacts, "gridItemPrefab").GetComponentsInChildren<Image>(true))
                .Concat(Read<GameObject>(artifactPreview, "statItemPrefab").GetComponentsInChildren<Image>(true)))
                if (image.sprite != null) Check(AssetDatabase.GetAssetPath(image.sprite).StartsWith("Assets/Project/UI/", StringComparison.Ordinal), "UI art is sourced only from Project/UI: " + image.name);
            Set(builder, "playerInventory", inventoryData);
            builder.SetMode(ZoneMode.SafeZone);
            foreach (var slot in slots) slot.Init(builder, Array.IndexOf(slots, slot));
            foreach (var slot in slots)
            {
                var button = Read<Button>(slot, "changeCharacterButton");
                Check(button.GetComponentInChildren<TMP_Text>().text == "Change Character", "Authored button uses the requested label.");
                Check(slot.GetComponentsInChildren<Button>(true).Count(b => b.GetComponentInChildren<TMP_Text>()?.text == "Change Character") == 1, "Each slot has one Change Character button.");
                Check(Read<ArtifactSlotUI[]>(slot, "artifactSlots").Length == 3 && Read<ArtifactSlotUI[]>(slot, "artifactSlots").All(a => a != null), "Each character retains three working artifact destinations.");
                Check(Read<GameObject>(slot, "activeIndicator") != null, "Active character feedback remains available.");
            }
            inventory.Init(builder, root.GetComponent<Canvas>());
            inventory.SubscribeToArtifactClicks(artifact => typeof(TeamBuilderUI).GetMethod("HandleArtifactClicked", Private).Invoke(builder, new object[] { artifact }));
            artifactPreview.SetEquipAction(artifact => builder.TryEquipArtifact(artifact));
            var characterData = AssetDatabase.FindAssets("t:CharacterItem").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterItem>).Where(c => c.characterObjectData != null)
                .GroupBy(c => c.characterObjectData).Select(g => g.First()).Take(2).ToList();
            Check(characterData.Count == 2, "Two real character assets available for regression.");
            slots[0].SetCharacter(characterData[0]); slots[1].SetCharacter(characterData[1]);
            characters.PopulateGrid(characterData);
            root.GetComponentsInChildren<InspectScreenUI>(true).ToList().ForEach(p => p.gameObject.SetActive(false));
            builder.gameObject.SetActive(true);
            root.SetActive(true);
            var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
            slots[0].OnPointerClick(pointer);
            Check(!inventory.IsAnyPanelOpen, "Portrait clicks do not open inventory.");
            Read<Button>(slots[0], "changeCharacterButton").onClick.Invoke();
            Check(inventory.IsCharacterGridOpen && builder.SelectedCharacterSlotIndex == 0, "Change Character opens the correct destination.");
            Check(!preview.gameObject.activeSelf, "Opening the grid does not select a character.");
            var cards = characters.GetComponentsInChildren<CharacterGridItemUI>();
            cards.First(c => c.CharacterData == characterData[0]).OnPointerClick(pointer);
            Check(preview.SelectedCharacter == characterData[0] && preview.gameObject.activeSelf, "Click shows selected character preview.");
            Check(!Read<Button>(preview, "assignButton").interactable, "Current character action is disabled.");
            cards.First(c => c.CharacterData == characterData[1]).OnPointerClick(pointer);
            Check(slots[0].CurrentCharacter == characterData[0], "Previewing does not assign.");
            Capture(root, "character-selected");
            Capture(root, "character-selected-4x3", 1024, 768);
            CheckSlotsDoNotOverlap(root, inventory, slots);
            builder.BeginCharacterSwitch(1);
            Check(!preview.gameObject.activeSelf && preview.SelectedCharacter == null, "Changing destinations clears preview.");
            builder.CloseInventoryPanel(); builder.BeginCharacterSwitch(0);
            Check(!preview.gameObject.activeSelf, "Reopening starts without selection.");
            Capture(root, "character-empty");
            cards.First(c => c.CharacterData == characterData[1]).OnPointerClick(pointer);
            Read<Button>(preview, "assignButton").onClick.Invoke();
            Check(slots[0].CurrentCharacter == characterData[1] && slots[1].CurrentCharacter == characterData[0], "Assign swaps an already assigned character.");
            Check(!inventory.IsAnyPanelOpen && !preview.gameObject.activeSelf, "Assign closes and clears selection.");

            var definitions = Resources.LoadAll<ArtifactDefinition>("ItemSO").Where(d => d.isEquippable).Take(2).ToArray();
            Check(definitions.Length == 2, "Real artifact assets available.");
            var instances = definitions.Select((d, i) => new ArtifactInstance
            {
                instanceId = "ui-validation-" + i, definitionId = d.id, isNew = false,
                rolls = new List<ArtifactRoll> { new ArtifactRoll(ArtifactStatKind.FlatAttack, 11, ArtifactRollSource.Generated), new ArtifactRoll(ArtifactStatKind.FlatMaxHP, 120, ArtifactRollSource.Generated) }
            }).ToList();
            artifacts.PopulateGrid(instances);
            var target = slots[0].GetFirstEmptyOrFirstArtifactSlot();
            builder.BeginArtifactSwitch(target);
            var overlay = Read<Image>(target, "selectionOverlay");
            Check(overlay != null && overlay.gameObject.activeSelf && !overlay.raycastTarget && !overlay.fillCenter, "Destination overlay is visible, noninteractive and leaves the artifact visible.");
            Check(!artifactPreview.gameObject.activeSelf, "Opening artifact destination does not preselect equipped gear.");
            var artifactCard = artifacts.GetComponentsInChildren<ArtifactGridItemUI>().First();
            artifactCard.OnPointerClick(pointer);
            Check(artifactPreview.CurrentArtifact == artifactCard.ArtifactData && artifactPreview.gameObject.activeSelf, "Single click reveals artifact preview.");
            Check(target.CurrentArtifact == null, "First click cannot auto-equip.");
            Capture(root, "artifact-selected");
            Capture(root, "artifact-selected-4x3", 1024, 768);
            var sort = Read<TMP_Dropdown>(artifacts, "sortDropdown");
            var sortRect = (RectTransform)sort.transform;
            Check(sort.options.Any(option => option.text == "Rarity"), "Rarity is available in the sorting menu.");
            Check(sortRect.rect.width > 100f && sortRect.rect.width <= ((RectTransform)sortRect.parent).rect.width + .01f, "Sorting remains visible and inside its toolbar.");
            CheckSlotsDoNotOverlap(root, inventory, slots);
            Check(Read<Button>(artifactPreview, "equipButton").gameObject.activeSelf, "Inline Equip button is visible.");
            Read<Button>(artifactPreview, "equipButton").onClick.Invoke();
            Check(target.CurrentArtifact == artifactCard.ArtifactData, "Equip confirms the selected artifact.");
            Check(artifactPreview.gameObject.activeSelf && inventory.IsArtifactGridOpen, "Equip keeps the inventory and preview open.");
            builder.CloseInventoryPanel(); builder.BeginArtifactSwitch(target);
            Check(!artifactPreview.gameObject.activeSelf && artifactPreview.CurrentArtifact == null, "Artifact reopen clears selection.");
            builder.BeginCharacterSwitch(0);
            Check(!artifactPreview.gameObject.activeSelf && !preview.gameObject.activeSelf, "Category switch clears both previews.");
            builder.SetMode(ZoneMode.CombatZone);
            Check(!inventory.gameObject.activeSelf && !Read<Button>(slots[0], "changeCharacterButton").gameObject.activeSelf, "Combat closes inventory and hides edit buttons.");
            ValidateFeedback(root);
            File.WriteAllText("Logs/TeamBuilderInventoryValidation.txt", $"PASS: {assertions} assertions.\nScreenshots: Logs/TeamBuilderInventoryPreviews\n");
            Debug.Log($"TeamBuilder inventory validation passed: {assertions} assertions.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); Object.DestroyImmediate(inventoryData); }
    }

    private static void Capture(GameObject root, string name, int width = 1280, int height = 720)
    {
        Directory.CreateDirectory("Logs/TeamBuilderInventoryPreviews");
        var canvas = root.GetComponent<Canvas>();
        root.transform.localScale = Vector3.one;
        foreach (var group in root.GetComponents<CanvasGroup>()) group.alpha = 1f;
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var host = new GameObject("UI validation camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(host, root.scene);
        var camera = host.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 5.4f;
        camera.scene = root.scene;
        camera.transform.position = new Vector3(0, 0, -100); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .03f, .055f);
        var target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
        var previous = RenderTexture.active; var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            Canvas.ForceUpdateCanvases();
            foreach (var grid in root.GetComponentsInChildren<TeamInventoryGridLayout>()) grid.Refresh();
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes($"Logs/TeamBuilderInventoryPreviews/{name}.png", texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(host); Object.DestroyImmediate(target);
        }
    }
    private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); assertions++; }
    private static void CheckSlotsDoNotOverlap(GameObject root, InventoryBuilderPanel inventory, TeamSlotUI[] slots)
    {
        var corners = new Vector3[4];
        ((RectTransform)inventory.transform).GetWorldCorners(corners);
        float right = root.transform.InverseTransformPoint(corners[2]).x;
        foreach (var slot in slots)
        {
            ((RectTransform)slot.transform).GetWorldCorners(corners);
            Check(root.transform.InverseTransformPoint(corners[0]).x > right, "Inventory leaves each complete team slot visible.");
        }
    }

    private static void ValidateFeedback(GameObject root)
    {
        var host = new GameObject("Overlay feedback validation", typeof(RectTransform), typeof(UIFeedbackDriver));
        host.transform.SetParent(root.transform, false);
        host.transform.localScale = new Vector3(1.15f, .9f, 1f);
        var content = new GameObject("Content", typeof(RectTransform)); content.transform.SetParent(host.transform, false);
        var overlayObject = new GameObject("Feedback Overlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(host.transform, false);
        var overlay = overlayObject.GetComponent<Image>(); overlay.raycastTarget = false;
        Vector3 scale = host.transform.localScale;
        var routine = (System.Collections.IEnumerator)typeof(UIFeedbackDriver).GetMethod("PulseRoutine", Private)
            .Invoke(host.GetComponent<UIFeedbackDriver>(), new object[] { host.transform, overlay, 1.5f, 1f });
        Check(routine.MoveNext(), "Overlay feedback begins.");
        Check(host.transform.localScale == scale && content.transform.localScale == Vector3.one, "Feedback leaves actual content scale unchanged.");
        Check(overlay.color.r == 1f && overlay.color.g == 1f && overlay.color.b == 1f && !overlay.raycastTarget, "Feedback is a white noninteractive overlay.");
        var motion = root.GetComponentInChildren<TeamBuilderInventoryMotion>(true);
        var slotRoot = Read<RectTransform>(motion, "teamSlotsRoot");
        var originalScale = slotRoot.localScale;
        typeof(TeamBuilderInventoryMotion).GetMethod("ApplyPose", Private).Invoke(motion, new object[] { .5f });
        Check(slotRoot.localScale == originalScale, "Inventory transition moves slots without scaling them.");
        var inventoryRoot = Read<RectTransform>(motion, "inventoryRoot");
        var group = Read<CanvasGroup>(motion, "inventoryGroup");
        Check(Mathf.Approximately(group.alpha, .5f) && inventoryRoot.anchoredPosition.x < 0f, "Transition combines slide with opacity.");
        typeof(TeamBuilderInventoryMotion).GetMethod("ApplyPose", Private).Invoke(motion, new object[] { 0f });
        Check(Mathf.Approximately(group.alpha, 0f) && Mathf.Approximately(slotRoot.offsetMin.x, 24f), "Closing restores the team layout and fades inventory out.");
        Object.DestroyImmediate(host);
    }
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
}
#endif
