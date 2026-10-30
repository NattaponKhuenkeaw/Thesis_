#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using Project.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class TeamSelectionLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> temporary = new List<Object>();
    private TeamBuilderUI builder;
    private TeamUI shell;
    private PlayerRunInventory inventory;
    private CharacterItem main, camera;
    private float timeScale, volume;
    private int locks;

    [SetUp]
    public void SetUp()
    {
        timeScale = Time.timeScale;
        volume = AudioListener.volume;
        locks = (int)typeof(InputLockManager).GetField("lockCount", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        temporary.Add(inventory);
        main = Object.Instantiate(Resources.Load<CharacterItem>("ItemSO/char_0_Main"));
        camera = Object.Instantiate(Resources.Load<CharacterItem>("ItemSO/char_1_CameraGIrl"));
        temporary.Add(main); temporary.Add(camera);
        inventory.ownedItems.Clear();
        inventory.ownedItems.Add(main); inventory.ownedItems.Add(camera);
        inventory.DeployTeam(new[] { main.characterObjectData, camera.characterObjectData }, 0);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefab/UI/TeamCanvas.prefab"));
        temporary.Add(root);
        shell = root.GetComponent<TeamUI>();
        Call(shell, "Awake");
        builder = root.GetComponentInChildren<TeamBuilderUI>(true);
        Set(builder, "playerInventory", inventory);
        Set(builder, "zoneStateProvider", null);
        Set(builder, "teamLoader", null);
        builder.SetMode(ZoneMode.SafeZone);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = temporary.Count - 1; i >= 0; i--)
            if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
        temporary.Clear();
        Time.timeScale = timeScale;
        AudioListener.volume = volume;
        typeof(InputLockManager).GetField("lockCount", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, locks);
    }

    [Test]
    public void ClosingBeforeStartDoesNotInitializeThrowOrRestoreAnotherPanelsPause()
    {
        Time.timeScale = .25f;
        AudioListener.volume = .63f;
        Assert.DoesNotThrow(builder.ClosePanel);
        Assert.That(Get<bool>(builder, "isInitialized"), Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(.25f));
        Assert.That(AudioListener.volume, Is.EqualTo(.63f).Within(.001f));
        Assert.That(builder.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void ClosingAnInitializedButUnopenedPanelDoesNotDeployAnEmptyDraft()
    {
        var loader = Loader(false);
        Set(builder, "teamLoader", loader);
        Call(builder, "EnsureInitialized");
        Time.timeScale = .4f;
        AudioListener.volume = .61f;
        builder.ClosePanel();
        Assert.That(Time.timeScale, Is.EqualTo(.4f));
        Assert.That(AudioListener.volume, Is.EqualTo(.61f).Within(.001f));
        Assert.That(inventory.GetTeamCharacterAt(0), Is.EqualTo(main.characterObjectData));
        Assert.That(inventory.GetTeamCharacterAt(1), Is.EqualTo(camera.characterObjectData));
    }

    [Test]
    public void OpeningAfterLoaderAppearsRebindsThePreviouslyInitializedDraft()
    {
        Call(builder, "EnsureInitialized");
        Assert.That(Get<TeamLoader>(builder, "teamLoader"), Is.Null);
        var loader = Loader(true);
        builder.OpenPanel();
        Assert.That(Get<TeamLoader>(builder, "teamLoader"), Is.EqualTo(loader));
        var slots = Get<TeamSlotUI[]>(builder, "teamSlots");
        Assert.That(slots[0].CurrentCharacter, Is.EqualTo(main));
        Assert.That(slots[1].CurrentCharacter, Is.EqualTo(camera));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void CoreCharacterButtonsPreviewSwapAndDeployOnClose(int slotIndex)
    {
        Set(builder, "teamLoader", Loader(false));
        builder.ClosePanel();
        builder.OpenPanel();
        var slots = Get<TeamSlotUI[]>(builder, "teamSlots");
        var expected = slots[1 - slotIndex].CurrentCharacter;
        Get<Button>(slots[slotIndex], "changeCharacterButton").onClick.Invoke();
        var panel = Get<InventoryBuilderPanel>(builder, "inventoryPanel");
        Assert.That(panel.IsCharacterGridOpen, Is.True);
        Assert.That(builder.SelectedCharacterSlotIndex, Is.EqualTo(slotIndex));
        var grid = Get<CharacterGridInventoryUI>(panel, "characterGridPanel");
        var card = grid.GetComponentsInChildren<CharacterGridItemUI>().Single(c => c.CharacterData == expected);
        card.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null));
        var preview = Get<CharacterPreviewPanel>(grid, "previewPanel");
        Assert.That(preview.SelectedCharacter, Is.EqualTo(expected));
        Get<Button>(preview, "assignButton").onClick.Invoke();
        Assert.That(slots[slotIndex].CurrentCharacter, Is.EqualTo(expected));
        Assert.That(inventory.GetTeamCharacterAt(slotIndex), Is.Not.EqualTo(expected.characterObjectData), "The draft is not live until closing.");
        builder.ClosePanel();
        Assert.That(inventory.GetTeamCharacterAt(slotIndex), Is.EqualTo(expected.characterObjectData));
        Assert.That(shell.IsTeamBuilderOpen, Is.False);
        Assert.That(InputLockManager.IsLocked, Is.EqualTo(locks > 0));
    }

    [Test]
    public void RepeatedOpenAndCloseRestoreOriginalAudioTimeAndSortingOnlyOnce()
    {
        Set(builder, "teamLoader", Loader(false));
        Time.timeScale = .35f;
        AudioListener.volume = .71f;
        int order = shell.GetComponent<Canvas>().sortingOrder;
        builder.OpenPanel();
        float ducked = AudioListener.volume;
        builder.OpenPanel();
        Assert.That(AudioListener.volume, Is.EqualTo(ducked).Within(.001f));
        builder.ClosePanel();
        builder.ClosePanel();
        Assert.That(Time.timeScale, Is.EqualTo(.35f));
        Assert.That(AudioListener.volume, Is.EqualTo(.71f).Within(.001f));
        Assert.That(shell.GetComponent<Canvas>().sortingOrder, Is.EqualTo(order));
        Assert.That(InputLockManager.IsLocked, Is.EqualTo(locks > 0));
    }

    [Test]
    public void DisablingAnOpenPanelReleasesItsPauseAndInputLock()
    {
        Set(builder, "teamLoader", Loader(false));
        Time.timeScale = .6f;
        AudioListener.volume = .8f;
        builder.OpenPanel();
        builder.gameObject.SetActive(false);
        // Runtime OnDisable is invoked explicitly outside Play Mode.
        Call(builder, "OnDisable");
        Assert.That(Time.timeScale, Is.EqualTo(.6f));
        Assert.That(AudioListener.volume, Is.EqualTo(.8f).Within(.001f));
        Assert.That(shell.IsTeamBuilderOpen, Is.False);
        Assert.That(InputLockManager.IsLocked, Is.EqualTo(locks > 0));
    }

    [Test]
    public void CoreCharacterInventoryAndArtifactFlowStillPassesItsAuthoredValidation()
    {
        TeamBuilderInventoryValidation.Run();
    }

    [TestCase(0)]
    [TestCase(1)]
    public void PointerRaycastsReachChangeCharacterInventoryCardAndAssign(int slotIndex)
    {
        Set(builder, "teamLoader", Loader(false));
        builder.OpenPanel();
        var canvas = shell.GetComponent<Canvas>();
        var cameraRoot = new GameObject("Team UI click camera");
        temporary.Add(cameraRoot);
        var uiCamera = cameraRoot.AddComponent<Camera>();
        uiCamera.enabled = false;
        uiCamera.orthographic = true;
        uiCamera.orthographicSize = 5.4f;
        uiCamera.transform.position = new Vector3(0f, 0f, -100f);
        uiCamera.GetUniversalAdditionalCameraData();
        var target = new RenderTexture(1280, 720, 24);
        temporary.Add(target);
        target.Create();
        uiCamera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCamera;
        canvas.planeDistance = 10f;
        var events = new GameObject("Team UI click events", typeof(EventSystem));
        temporary.Add(events);
        var slots = Get<TeamSlotUI[]>(builder, "teamSlots");
        var expected = slots[1 - slotIndex].CurrentCharacter;
        Click(Get<Button>(slots[slotIndex], "changeCharacterButton"));
        var panel = Get<InventoryBuilderPanel>(builder, "inventoryPanel");
        var grid = Get<CharacterGridInventoryUI>(panel, "characterGridPanel");
        Click(grid.GetComponentsInChildren<CharacterGridItemUI>().Single(c => c.CharacterData == expected));
        var preview = Get<CharacterPreviewPanel>(grid, "previewPanel");
        Assert.That(preview.SelectedCharacter, Is.EqualTo(expected));
        Click(Get<Button>(preview, "assignButton"));
        Assert.That(slots[slotIndex].CurrentCharacter, Is.EqualTo(expected));

        void Click(Component control)
        {
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(uiCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var rect = (RectTransform)control.transform;
            var pointer = new PointerEventData(events.GetComponent<EventSystem>())
            {
                position = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits, Is.Not.Empty, control.name + " is not reachable by a pointer at " + pointer.position);
            var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(receiver, Is.EqualTo(control.gameObject), "A different UI layer intercepts " + control.name + ": " + hits[0].gameObject.name);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        }
    }

    private TeamLoader Loader(bool active)
    {
        var root = new GameObject("Team selection test loader");
        root.SetActive(false);
        temporary.Add(root);
        var loader = root.AddComponent<TeamLoader>();
        Set(loader, "runInventory", inventory);
        root.SetActive(active);
        return loader;
    }

    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
#endif
