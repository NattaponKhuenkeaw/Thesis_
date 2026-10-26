#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.Save;
using Project.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class MergedUiPrefabIntegrationTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);

    [TestCase("SaveSelectPanel")]
    [TestCase("PauseMenu")]
    [TestCase("RoomChoiceUI")]
    public void ResourcesLinksInheritTheMergedArtPrefabWithoutCopiedUi(string name)
    {
        var runtime = Resources.Load<GameObject>($"UI/{name}");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Project/Prefab/UI/{name}.prefab");
        Assert.That(runtime, Is.Not.Null);
        Assert.That(PrefabUtility.GetPrefabAssetType(runtime), Is.EqualTo(PrefabAssetType.Variant));
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(runtime), Is.EqualTo(source));
        foreach (var item in runtime.GetComponentsInChildren<Transform>(true))
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject), Is.Zero, item.name);
    }

    [Test]
    public void SaveCardsReferenceTheirOwnLabelsPortraitsAndDetailsNotTheSharedDialog()
    {
        var source = Resources.Load<SaveSelectPanel>("UI/SaveSelectPanel");
        var cards = Get<SaveSlotCardView[]>(source, "slots");
        Assert.That(cards.Length, Is.EqualTo(SaveSlotStore.SlotCount));
        foreach (var card in cards)
        {
            foreach (string field in new[] { "slotNameText", "levelText", "currencyText" })
            {
                var text = Get<Text>(card, field);
                Assert.That(text, Is.Not.Null, field);
                Assert.That(text.transform.IsChildOf(card.transform), Is.True, field);
            }
            Assert.That(Get<Text>(card, "biomeText"), Is.Null);
            Assert.That(Get<GameObject>(card, "actionPanel"), Is.Null);
            Assert.That(Get<Button>(card, "playButton"), Is.Null);
            Assert.That(Get<Button>(card, "deleteButton"), Is.Null);
            foreach (var portrait in Get<Image[]>(card, "portraits"))
                Assert.That(portrait != null && portrait.transform.IsChildOf(card.transform), Is.True);
            foreach (var item in Get<GameObject[]>(card, "filledObjects"))
                Assert.That(item != null && item.transform.IsChildOf(card.transform), Is.True);
        }
        foreach (string field in new[] { "actionPlayButton", "actionDeleteButton", "actionCancelButton", "closeButton" })
            Assert.That(Get<Button>(source, field), Is.Not.Null, field);
        Assert.That(Get<Button>(source, "closeButton"), Is.Not.EqualTo(Get<Button>(source, "actionCancelButton")));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MenuCreatesAndOwnsASceneInstanceFromEitherResourcesOrSerializedPrefab(bool serializedPrefab)
    {
        var host = new GameObject("Menu prefab reference test");
        host.SetActive(false);
        var source = AssetDatabase.LoadAssetAtPath<SaveSelectPanel>("Assets/Project/Prefab/UI/SaveSelectPanel.prefab");
        SaveSelectPanel instance = null;
        bool originalOpen = source.IsOpen;
        try
        {
            var menu = host.AddComponent<MainMenuController>();
            if (serializedPrefab)
                typeof(MainMenuController).GetField("saveSelectPanel", Private).SetValue(menu, source);
            Call(menu, "EnsureSavePanel");
            instance = Get<SaveSelectPanel>(menu, "saveSelectPanel");
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.gameObject.scene.IsValid(), Is.True);
            Assert.That(EditorUtility.IsPersistent(instance), Is.False);
            instance.Hide();
            Assert.That(source.IsOpen, Is.EqualTo(originalOpen), "Do not toggle the prefab asset itself");
            Call(menu, "EnsureSavePanel");
            Assert.That(Get<SaveSelectPanel>(menu, "saveSelectPanel"), Is.EqualTo(instance));
            Call(menu, "OnDestroy");
            Assert.That(instance == null, Is.True, "Owned popup must not outlive its menu");
        }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance.gameObject);
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void MenuPreservesAnAuthoredSceneInstanceInsteadOfCloningOrDestroyingIt()
    {
        var host = new GameObject("Menu scene reference test");
        host.SetActive(false);
        var instance = Object.Instantiate(Resources.Load<SaveSelectPanel>("UI/SaveSelectPanel"));
        try
        {
            var menu = host.AddComponent<MainMenuController>();
            typeof(MainMenuController).GetField("saveSelectPanel", Private).SetValue(menu, instance);
            Call(menu, "EnsureSavePanel");
            Assert.That(Get<SaveSelectPanel>(menu, "saveSelectPanel"), Is.EqualTo(instance));
            Call(menu, "OnDestroy");
            Assert.That(instance != null, Is.True);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(instance.gameObject); }
    }

    [Test]
    public void MainMenuSceneKeepsItsFinalArtAndValidButtonAndSavePrefabReferences()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Project/Scenes/MainMenu.unity", OpenSceneMode.Additive);
        try
        {
            var menu = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MainMenuController>(true)).Single();
            foreach (string field in new[] { "playButton", "settingsButton", "quitButton", "settingsBackButton", "confirmCancelButton", "confirmOkButton" })
                Assert.That(Get<Button>(menu, field), Is.Not.Null, field);
            var source = AssetDatabase.LoadAssetAtPath<SaveSelectPanel>("Assets/Project/Prefab/UI/SaveSelectPanel.prefab");
            Assert.That(Get<SaveSelectPanel>(menu, "saveSelectPanel"), Is.EqualTo(source));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator MainMenuPlayAndSettingsButtonsControlTheInstantiatedSavePopup()
    {
        EditorSceneManager.OpenScene("Assets/Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        const string migrated = "project.save.slots.migrated";
        bool hadMigration = PlayerPrefs.HasKey(migrated);
        int migration = PlayerPrefs.GetInt(migrated, 0);
        PlayerPrefs.SetInt(migrated, 1);
        try
        {
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            var panel = Get<SaveSelectPanel>(menu, "saveSelectPanel");
            Assert.That(panel.gameObject.scene.IsValid(), Is.True);
            Assert.That(panel.IsOpen, Is.False);
            yield return null;
            Click(Get<Button>(menu, "playButton"));
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(Object.FindObjectsByType<SaveSelectPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            panel.Hide();
            Click(Get<Button>(menu, "settingsButton"));
            Assert.That(Get<GameObject>(menu, "settingsPanel").activeSelf, Is.True);
            yield return null;
            Click(Get<Button>(menu, "settingsBackButton"));
            Assert.That(Get<GameObject>(menu, "mainPanel").activeSelf, Is.True);
            Assert.That(Get<GameObject>(menu, "settingsPanel").activeSelf, Is.False);
            yield return null;
            Click(Get<Button>(menu, "playButton"));
            Assert.That(panel.IsOpen, Is.True);
            panel.Hide();
        }
        finally
        {
            if (hadMigration) PlayerPrefs.SetInt(migrated, migration); else PlayerPrefs.DeleteKey(migrated);
            PlayerPrefs.Save();
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SharedSaveDialogRoutesOnlyTheSelectedSlotAndCanCancelOrClose()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var strings = new Dictionary<string, string>();
        var ints = new Dictionary<string, int>();
        var existing = new HashSet<string>();
        string migrated = "project.save.slots.migrated";
        BackupInt(migrated, ints, existing);
        for (int i = 0; i < SaveSlotStore.SlotCount; i++)
        {
            BackupInt($"project.save.slot{i}.occupied", ints, existing);
            string key = $"project.save.slot{i}.header";
            if (PlayerPrefs.HasKey(key)) existing.Add(key);
            strings[key] = PlayerPrefs.GetString(key, "");
            PlayerPrefs.SetInt($"project.save.slot{i}.occupied", 0);
        }
        PlayerPrefs.SetInt(migrated, 1);
        var panel = Object.Instantiate(Resources.Load<SaveSelectPanel>("UI/SaveSelectPanel"));
        try
        {
            var created = new List<int>();
            var played = new List<int>();
            var deleted = new List<int>();
            panel.CreateRequested += created.Add;
            panel.PlayRequested += played.Add;
            panel.DeleteRequested += deleted.Add;
            panel.Open();
            yield return null;
            Click(panel.GetSlot(1).GetComponent<Button>());
            Assert.That(created, Is.EqualTo(new[] { 1 }));

            for (int i = 0; i < SaveSlotStore.SlotCount; i++)
            {
                PlayerPrefs.SetInt($"project.save.slot{i}.occupied", 1);
                PlayerPrefs.SetString($"project.save.slot{i}.header", JsonUtility.ToJson(new SaveSlotHeader { currency = 111 * (i + 1), globalLevel = i + 3 }));
            }
            panel.Refresh();
            Assert.That(Get<Text>(panel.GetSlot(0), "slotNameText").text, Is.EqualTo("Save 1"));
            Assert.That(Get<Text>(panel.GetSlot(1), "slotNameText").text, Is.EqualTo("Save 2"));
            Assert.That(Get<Text>(panel.GetSlot(0), "currencyText").text, Is.EqualTo("111"));
            Assert.That(Get<Text>(panel.GetSlot(1), "currencyText").text, Is.EqualTo("222"));
            Assert.That(Get<Text>(panel.GetSlot(1), "levelText").text, Is.EqualTo("4"));
            panel.GetSlot(1).GetComponent<Button>().onClick.Invoke();
            Assert.That(Get<Text>(panel, "actionTitleText").text, Is.EqualTo("Play Save 2?"));
            Assert.That(Get<Text>(panel, "actionCurrencyText").text, Is.EqualTo("222"));
            yield return null;
            Click(Get<Button>(panel, "actionCancelButton"));
            Assert.That(Get<GameObject>(panel, "actionPanel").activeSelf, Is.False);
            Assert.That(panel.IsOpen, Is.True);
            Get<Button>(panel, "actionPlayButton").onClick.Invoke();
            Assert.That(played, Is.Empty, "Canceled selection must not be reused");
            panel.GetSlot(1).GetComponent<Button>().onClick.Invoke();
            yield return null;
            Click(Get<Button>(panel, "actionPlayButton"));
            Assert.That(played, Is.EqualTo(new[] { 1 }));
            Get<Button>(panel, "actionPlayButton").onClick.Invoke();
            Assert.That(played.Count, Is.EqualTo(1), "Repeated clicks must not submit twice");
            panel.GetSlot(0).GetComponent<Button>().onClick.Invoke();
            yield return null;
            Click(Get<Button>(panel, "actionDeleteButton"));
            Assert.That(deleted, Is.EqualTo(new[] { 0 }));
            panel.GetSlot(1).GetComponent<Button>().onClick.Invoke();
            Get<Button>(panel, "closeButton").onClick.Invoke();
            Assert.That(panel.IsOpen, Is.False);
            panel.Open();
            Assert.That(Get<GameObject>(panel, "actionPanel").activeSelf, Is.False);
        }
        finally
        {
            Object.Destroy(panel.gameObject);
            foreach (var item in strings)
                if (existing.Contains(item.Key)) PlayerPrefs.SetString(item.Key, item.Value); else PlayerPrefs.DeleteKey(item.Key);
            foreach (var item in ints)
                if (existing.Contains(item.Key)) PlayerPrefs.SetInt(item.Key, item.Value); else PlayerPrefs.DeleteKey(item.Key);
            PlayerPrefs.Save();
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PauseAndRoomChoiceLoadTheNewPrefabsAndRestoreGameplayState()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var pause = PauseMenuController.Instance;
        Assert.That(pause, Is.Not.Null);
        Call(pause, "TryPause");
        Assert.That(PauseMenuController.IsOpen, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(AudioListener.pause, Is.True);
        Assert.That(InputLockManager.IsLocked, Is.True);
        Get<Button>(pause, "settingsButton").onClick.Invoke();
        Assert.That(Get<GameObject>(pause, "settingsPanel").activeSelf, Is.True);
        Get<Button>(pause, "settingsBackButton").onClick.Invoke();
        Get<Button>(pause, "returnToTitleButton").onClick.Invoke();
        Assert.That(Get<GameObject>(pause, "confirmPanel").activeSelf, Is.True);
        Get<Button>(pause, "confirmCancelButton").onClick.Invoke();
        Assert.That(Get<GameObject>(pause, "confirmPanel").activeSelf, Is.False);
        Get<Button>(pause, "resumeButton").onClick.Invoke();
        Assert.That(PauseMenuController.IsOpen, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(AudioListener.pause, Is.False);
        Assert.That(InputLockManager.IsLocked, Is.False);
        Assert.That(Object.FindObjectsByType<PauseMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));

        var choice = RoomChoiceUI.EnsureInstance();
        Assert.That(choice, Is.Not.Null);
        Assert.That(RoomChoiceUI.EnsureInstance(), Is.EqualTo(choice));
        var room = ScriptableObject.CreateInstance<RoomData>();
        try
        {
            var data = new SerializedObject(room);
            data.FindProperty("roomId").stringValue = "Boss_UI_Test";
            data.FindProperty("roomKind").enumValueIndex = (int)RoomKind.Boss;
            data.ApplyModifiedPropertiesWithoutUndo();
            choice.Show(new[] { new RoomChoiceOffer(room) });
            Assert.That(choice.IsShowing, Is.True);
            var cards = Get<RoomChoiceCardView[]>(choice, "cards");
            Assert.That(cards[0].gameObject.activeSelf && cards[0].Button.interactable, Is.True);
            Assert.That(cards[1].gameObject.activeSelf || cards[2].gameObject.activeSelf, Is.False);
            choice.HideFromProximityExit();
            Assert.That(choice.IsShowing, Is.False);
            choice.Show(new[] { new RoomChoiceOffer(room), new RoomChoiceOffer(room), new RoomChoiceOffer(room) });
            Assert.That(cards.All(x => x.gameObject.activeSelf && x.Button.interactable), Is.True);
            choice.Hide();
            Assert.That(choice.HasCommittedChoice, Is.False);
        }
        finally { Object.Destroy(room); Object.Destroy(choice.gameObject); }
        yield return new ExitPlayMode();
    }

    private static void BackupInt(string key, Dictionary<string, int> values, HashSet<string> existing)
    {
        if (PlayerPrefs.HasKey(key)) existing.Add(key);
        values[key] = PlayerPrefs.GetInt(key, 0);
    }

    private static void Click(Button button)
    {
        var canvas = button.GetComponentInParent<Canvas>();
        var mode = canvas.renderMode;
        var originalCamera = canvas.worldCamera;
        float planeDistance = canvas.planeDistance;
        var target = new RenderTexture(Screen.width, Screen.height, 24);
        var cameraObject = new GameObject("Batch UI raycast render camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.transform.position = new Vector3(0, 0, -100);
        camera.orthographic = true;
        camera.targetTexture = target;
        target.Create();
        try
        {
            // Batch mode may not render newly enabled overlay graphics, leaving their depth at -1.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), $"No raycast hit on {button.name}: active={button.gameObject.activeInHierarchy}, position={pointer.position}, graphicDepth={button.targetGraphic.depth}");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), $"{button.name} is occluded by {hits[0].gameObject.name}");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        finally
        {
            canvas.renderMode = mode;
            canvas.worldCamera = originalCamera;
            canvas.planeDistance = planeDistance;
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
        }
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeOnFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
