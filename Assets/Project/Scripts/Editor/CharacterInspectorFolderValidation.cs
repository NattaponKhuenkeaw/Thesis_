#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using Project.TeamBuilder.UI.Panels;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CharacterInspectorFolderValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;

#if UNITY_INCLUDE_TESTS
    [MenuItem("Tools/Neo-JJK/UI/Validate Character Inspector Regression")]
    public static void RunRegression()
    {
        typeof(ShadowMeterValidation).GetMethod("RunFixtures", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { new[] { "RunGlobalLevelTests", "CameraGirlPotentialTests", "ArtifactInstanceSystemTests" }, "Logs/character-inspector-regression.xml", false });
    }
#endif

    [MenuItem("Tools/Neo-JJK/UI/Validate Character Inspector Folder Layout")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Validate outside Play Mode.");
        checks = 0;
        var root = PrefabUtility.LoadPrefabContents(CharacterInspectorFolderAuthoring.CanvasPath);
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            var inspector = root.GetComponentInChildren<InspectScreenUI>(true);
            var content = Read<GameObject>(inspector, "contentPanel");
            var potential = root.GetComponentInChildren<CharacterPotentialPanel>(true);
            var folderDocument = Read<RectTransform>(inspector, "folderDocument");
            var info = content.GetComponentInChildren<CharacterInfoPanel>(true);
            var skills = content.GetComponentInChildren<CharacterSkillPanel>(true);
            var visual = content.GetComponentInChildren<CharacterVisualPanel>(true);
            var equipment = content.GetComponentInChildren<EquipmentPanel>(true);
            var data = AssetDatabase.FindAssets("t:CharacterItem").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterItem>).Where(c => c.characterObjectData != null && c.characterObjectData.kitDefinition != null)
                .GroupBy(c => c.characterObjectData).Select(g => g.First()).Take(2).ToList();
            Check(data.Count == 2, "Two authored characters are available.");
            inventory.ownedItems.Clear(); inventory.ownedItems.AddRange(data);
            Check(!inspector.gameObject.activeSelf, "Inspector starts closed.");
            Check(potential != null, "Existing Potential panel remains present.");
            Check(Read<Button>(info, "potentialPanelButton") != null, "Potential route remains bound.");
            Check(Read<Image[]>(info, "potentialSegments").Length == 12, "Potential has twelve display segments.");
            foreach (var image in content.GetComponentsInChildren<Image>(true))
                if (image.sprite != null) Check(AssetDatabase.GetAssetPath(image.sprite).StartsWith("Assets/Project/UI/", StringComparison.Ordinal), "Decorative art is from Project/UI: " + image.name);
            foreach (var image in content.GetComponentsInChildren<RawImage>(true))
                if (image.texture != null) Check(AssetDatabase.GetAssetPath(image.texture).StartsWith("Assets/Project/UI/", StringComparison.Ordinal), "City backdrop is from Project/UI.");
            foreach (string field in new[] { "levelText", "atkText", "defText", "hpText", "critRateText", "critDmgText", "dotMasteryText", "AEgainText", "additionalDamageIncreaseText", "damageIncreaseText", "cooldownMasteryText", "potentialText" })
                Check(Read<TMP_Text>(info, field) != null, "Info data binding: " + field);
            var railPrefab = Read<GameObject>(inspector, "characterSliderItemPrefab");
            Check(railPrefab.GetComponent<CharacterGridItemUI>() != null, "Rail entries use character selection input.");
            Check(Read<GameObject>(skills, "entryTemplate") != null, "Skills have an authored entry template.");
            Set(inspector, "playerInventory", inventory); Set(inspector, "zoneStateProvider", null); Set(inspector, "owningTeamUI", null);
            var teamUI = root.GetComponent<Project.UI.TeamUI>(); if (teamUI != null) Object.DestroyImmediate(teamUI);
            root.GetComponentInChildren<TeamBuilderUI>(true).gameObject.SetActive(false);
            root.transform.localScale = Vector3.one;
            Call(inspector, "Awake");
            // OnEnable is invoked explicitly because validation runs outside the gameplay lifecycle.
            inspector.Open(data[0]); Call(inspector, "OnEnable"); Call(info, "OnEnable");
            var rail = Read<Transform>(inspector, "characterSliderGridParent");
            Check(inspector.CurrentCharacter == data[0], "Initial character selection.");
            Check(info.gameObject.activeSelf && !skills.gameObject.activeSelf, "Info is initially the active page.");
            Check(Read<Image>(visual, "characterPortrait").sprite == data[0].characterObjectData.metaData.portrait, "Identity portrait comes from character data.");
            Check(Read<TMP_Text>(visual, "characterNameText").text == data[0].characterObjectData.metaData.displayName, "Identity name comes from character data.");
            Check(Read<TMP_Text>(info, "levelText").text.Contains(inventory.GlobalLevel.ToString()), "Info uses the real shared run level.");
            Check(rail.childCount == 2, "Rail contains owned characters once each.");
            var firstCard = rail.GetChild(0).GetComponent<CharacterGridItemUI>();
            var secondCard = rail.GetChild(1).GetComponent<CharacterGridItemUI>();
            Check(Read<bool>(firstCard, "isSelected") && !Read<bool>(secondCard, "isSelected"), "Selected rail entry has a distinct state.");
            Check(Read<Image>(Read<Button>(inspector, "infoTabButton"), "m_TargetGraphic") != null, "Info tab has a visual target.");
            Capture(root, "info-main"); Capture(root, "info-main-4x3", 1024, 768);
            Read<Button>(inspector, "skillTabButton").onClick.Invoke();
            Check(skills.gameObject.activeSelf && !info.gameObject.activeSelf, "Skills tab switches document content.");
            Check(Read<Transform>(skills, "entryContainer").childCount >= 2, "Skills bind real authored entries.");
            Capture(root, "skills-main");
            secondCard.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            Check(inspector.CurrentCharacter == data[1], "Rail changes the selected character.");
            Check(rail.childCount == 2 && rail.GetChild(1).gameObject == secondCard.gameObject, "Selecting a character preserves rail objects and scroll.");
            Check(!Read<bool>(firstCard, "isSelected") && Read<bool>(secondCard, "isSelected"), "Rail highlight follows selection.");
            Check(skills.gameObject.activeSelf, "Character switch preserves the current folder tab.");
            inspector.ShowInfoPage(); Capture(root, "info-alternate");
            var equipmentButton = info.GetComponentsInChildren<Button>().First(b => b.name == "ViewEquipment");
            Check(equipmentButton.onClick.GetPersistentTarget(0) == inspector && equipmentButton.onClick.GetPersistentMethodName(0) == "ShowEquipmentPage", "Equipment action targets the current Inspector.");
            inspector.ShowEquipmentPage();
            Check(equipment.gameObject.activeSelf && !info.gameObject.activeSelf, "Equipment remains reachable.");
            Check(Read<TMP_Text>(equipment, "artifactDescriptionText").text.Length > 0, "Equipment includes an empty-loadout explanation.");
            Capture(root, "equipment");
            inspector.ShowInfoPage(); Read<Button>(info, "potentialPanelButton").onClick.Invoke();
            Check(potential.gameObject.activeSelf && !info.gameObject.activeSelf, "Upgrade Potential opens the existing panel.");
            inspector.ShowInfoPage(); Check(!potential.gameObject.activeSelf && info.gameObject.activeSelf, "Returning from Potential restores Info.");
            Check(folderDocument.localScale == Vector3.one, "Folder document remains at its authored scale.");
            var origin = folderDocument.anchoredPosition;
            var drawer = (System.Collections.IEnumerator)typeof(InspectScreenUI).GetMethod("AnimateFolderPage", Private).Invoke(inspector, null);
            Check(drawer.MoveNext(), "Drawer animation starts.");
            Check(folderDocument.anchoredPosition.x > origin.x && Read<CanvasGroup>(inspector, "folderDocumentGroup").alpha < 1f, "Drawer moves and fades while opening.");
            Check(folderDocument.localScale == Vector3.one, "Drawer animation does not scale document content.");
            Call(inspector, "ResetFolderMotion");
            Check(folderDocument.anchoredPosition == origin && Read<CanvasGroup>(inspector, "folderDocumentGroup").alpha == 1f, "Interrupted drawer motion restores its pose.");
            Read<Button>(inspector, "closeButton").onClick.Invoke(); Check(!inspector.gameObject.activeSelf, "Close button dismisses Inspector.");
            inventory.ownedItems.Clear(); inspector.Open(null);
            Check(Read<GameObject>(inspector, "emptyStatePanel").activeSelf, "Empty inventory has a readable state.");
            Check(!info.gameObject.activeSelf && !visual.gameObject.activeSelf, "Empty state hides stale character data.");
            Check(!folderDocument.gameObject.activeSelf, "Empty state hides the old folder document.");
            Capture(root, "empty");
            File.WriteAllText("Logs/CharacterInspectorFolderValidation.txt", $"PASS: {checks} assertions.\nPreviews: Logs/CharacterInspectorFolderPreviews\n");
            Debug.Log($"Character Inspector folder validation passed: {checks} assertions.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); Object.DestroyImmediate(inventory); }
    }

    private static void Capture(GameObject root, string name, int width = 1280, int height = 720)
    {
        Directory.CreateDirectory("Logs/CharacterInspectorFolderPreviews");
        var canvas = root.GetComponent<Canvas>(); root.transform.localScale = Vector3.one;
        foreach (var group in root.GetComponents<CanvasGroup>()) group.alpha = 1;
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var host = new GameObject("Inspector validation camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(host, root.scene);
        var camera = host.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 5.4f; camera.scene = root.scene;
        camera.transform.position = new Vector3(0, 0, -100); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.03f, .04f, .07f);
        var target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
        var previous = RenderTexture.active; var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var layout in root.GetComponentsInChildren<VerticalLayoutGroup>()) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
            }
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes($"Logs/CharacterInspectorFolderPreviews/{name}.png", texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(host); Object.DestroyImmediate(target);
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
    private static T Read<T>(object host, string field)
    {
        for (var type = host.GetType(); type != null; type = type.BaseType)
        {
            var member = type.GetField(field, Private); if (member != null) return (T)member.GetValue(host);
        }
        throw new MissingFieldException(field);
    }
    private static void Set(object host, string field, object value) => host.GetType().GetField(field, Private).SetValue(host, value);
    private static void Call(object host, string method) => host.GetType().GetMethod(method, Private).Invoke(host, null);
}
#endif
