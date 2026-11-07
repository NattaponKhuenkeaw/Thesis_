#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class RoomChoiceAssetTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<RoomData> rooms = new List<RoomData>();
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Project/UI/MainMenu/{name}.PNG");

    private RoomChoiceOffer Offer(RoomKind kind, RouteDifficulty difficulty = RouteDifficulty.Easy, int coins = 0)
    {
        var room = ScriptableObject.CreateInstance<RoomData>();
        rooms.Add(room);
        var data = new SerializedObject(room);
        data.FindProperty("roomKind").intValue = (int)kind;
        data.FindProperty("roomId").stringValue = "RoomChoiceTest_" + kind;
        data.ApplyModifiedPropertiesWithoutUndo();
        return new RoomChoiceOffer(room, difficulty, null, coins > 0 ? new RouteReward { Coins = coins } : null);
    }

    [Test]
    public void CardsAreNestedPrefabsWithSeparateAuthoredBordersAndReusedMainMenuAssets()
    {
        var source = AssetDatabase.LoadAssetAtPath<RoomChoiceUI>("Assets/Project/Prefab/UI/RoomChoiceUI.prefab");
        var shared = AssetDatabase.LoadAssetAtPath<RoomChoiceCardView>("Assets/Project/Prefab/UI/RoomChoiceCard.prefab");
        var cards = Get<RoomChoiceCardView[]>(source, "cards");
        Assert.That(cards.Length, Is.EqualTo(3));
        foreach (var card in cards)
        {
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(card), Is.EqualTo(shared));
            Assert.That(card.GetComponent<Image>().sprite, Is.EqualTo(Art("UI_BlackBox")));
            var border = Get<Image>(card, "borderImage");
            Assert.That(border.gameObject, Is.Not.EqualTo(card.gameObject));
            Assert.That(border.sprite, Is.EqualTo(Art("UI_Grey_Border_NoOutLine")));
            Assert.That(card.Button.targetGraphic, Is.EqualTo(border));
            Assert.That(card.Button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
            Assert.That(card.Button.spriteState.selectedSprite, Is.EqualTo(Art("UI_Yellow_Border_NoOutLine")));
            Assert.That(card.Button.spriteState.highlightedSprite, Is.EqualTo(Art("UI_Grey_Border_NoOutLine")),
                "A hovered but unfocused card must not look selected");
            Assert.That(card.transform.Find("Content/Divider").GetComponent<Image>().sprite, Is.EqualTo(Art("UI_GreyLine")));
        }
        foreach (var item in source.GetComponentsInChildren<Transform>(true))
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject), Is.Zero, item.name);
    }

    [TestCase(RoomKind.Combat, "Defeat all enemies", "COMBAT REWARD", false)]
    [TestCase(RoomKind.Boss, "Defeat the boss", "FINAL ENCOUNTER", true)]
    [TestCase(RoomKind.Rest, "Recover your team", "RECOVERY", false)]
    [TestCase(RoomKind.Shop, "Browse and buy items", "MERCHANT", false)]
    [TestCase(RoomKind.Level, "Randomly level up\nyour team", "TEAM GROWTH", true)]
    public void KindLabelsAndIconsMatchActualRoomMechanicsWithoutBuildingObjects(RoomKind kind, string description, string footer, bool tag)
    {
        var card = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RoomChoiceCardView>("Assets/Project/Prefab/UI/RoomChoiceCard.prefab"));
        try
        {
            int count = card.GetComponentsInChildren<Transform>(true).Length;
            var offer = Offer(kind);
            for (int i = 0; i < 5; i++) card.Bind(offer, null);
            Assert.That(Get<TMP_Text>(card, "kindLabel").text, Is.EqualTo(kind.ToString().ToUpperInvariant()));
            Assert.That(Get<TMP_Text>(card, "descriptionLabel").text, Is.EqualTo(description));
            Assert.That(Get<TMP_Text>(card, "rewardOrZoneLabel").text, Is.EqualTo(footer));
            Assert.That(Get<GameObject>(card, "difficultyRoot").activeSelf, Is.EqualTo(tag));
            Assert.That(Get<Image>(card, "iconImage").enabled, Is.True);
            if (kind == RoomKind.Level)
                Assert.That(Get<Image>(card, "iconImage").sprite,
                    Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/UI/Room/UI_LevelUp_Icon.PNG")));
            Assert.That(Get<Image>(card, "soulIcon").gameObject.activeSelf, Is.False);
            Assert.That(card.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        }
        finally { Object.DestroyImmediate(card.gameObject); }
    }

    [UnityTest]
    public IEnumerator ChoiceLocksGameplayButNotUiNavigationAndReleasesOnlyItsOwnLock()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var ui = Object.Instantiate(Resources.Load<RoomChoiceUI>("UI/RoomChoiceUI"));
        var inputHost = new GameObject("Choice gameplay input test", typeof(InputHandler_Player));
        var input = inputHost.GetComponent<InputHandler_Player>();
        var offers = new[] { Offer(RoomKind.Combat), Offer(RoomKind.Shop) };
        try
        {
            InputLockManager.Lock();
            ui.Show(offers); ui.Show(offers); ui.Show(offers);
            ui.Hide();
            Assert.That(InputLockManager.IsLocked, Is.True, "Keep locks owned by another UI");
            InputLockManager.Unlock();
            Assert.That(InputLockManager.IsLocked, Is.False, "Repeated Show must not accumulate locks");
            ui.Show(offers);
            Assert.That(InputLockManager.IsLocked, Is.True);
            typeof(InputHandler_Player).GetField("moveInput", Private).SetValue(input, Vector2.right);
            typeof(InputHandler_Player).GetMethod("Update", Private).Invoke(input, null);
            Assert.That(Get<Vector2>(input, "moveInput"), Is.EqualTo(Vector2.zero));
            yield return null;
            Canvas.ForceUpdateCanvases();
            var cards = Get<RoomChoiceCardView[]>(ui, "cards");
            ExecuteEvents.Execute(cards[0].gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right, moveVector = Vector2.right }, ExecuteEvents.moveHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[1].gameObject), "UI navigation remains usable while gameplay is locked");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            ui.HideFromProximityExit();
            Assert.That(InputLockManager.IsLocked, Is.False);
            ui.Show(offers);
            ui.gameObject.SetActive(false);
            Assert.That(InputLockManager.IsLocked, Is.False);
            ui.gameObject.SetActive(true);
            Assert.That(ui.IsShowing, Is.False);
            ui.Show(offers);
            Object.DestroyImmediate(ui.gameObject);
            Assert.That(InputLockManager.IsLocked, Is.False);
        }
        finally
        {
            if (ui != null) Object.DestroyImmediate(ui.gameObject);
            Object.Destroy(inputHost);
        }
        yield return new ExitPlayMode();
    }

    [TestCase(1)]
    [TestCase(242)]
    [TestCase(int.MaxValue)]
    public void SoulRewardUsesAuthoredIconAndClearsItWhenCardIsRebound(int amount)
    {
        var card = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RoomChoiceCardView>("Assets/Project/Prefab/UI/RoomChoiceCard.prefab"));
        var artifact = ScriptableObject.CreateInstance<Project.InventorySystem.Items.Artifact>();
        try
        {
            var offer = Offer(RoomKind.Combat, coins: amount);
            int childCount = card.GetComponentsInChildren<Transform>(true).Length;
            var icon = Get<Image>(card, "soulIcon");
            var label = Get<TMP_Text>(card, "rewardOrZoneLabel");
            card.Bind(offer, null);
            Assert.That(label.text, Is.EqualTo($"{amount:N0} SOUL"));
            Assert.That(icon.gameObject.activeSelf, Is.True);
            Assert.That(icon.sprite, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/UI/Room/UI_Soul.PNG")));
            Assert.That(icon.preserveAspect, Is.True);
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(offer.Reward.Coins, Is.EqualTo(amount), "Presentation must not change the currency payout");

            offer.Reward.GrantsArtifact = true;
            offer.Reward.Artifact = artifact;
            card.Bind(offer, null);
            Assert.That(label.text, Is.EqualTo($"{amount:N0} SOUL + ARTIFACT"));
            Assert.That(icon.gameObject.activeSelf, Is.True);

            offer.Reward.Coins = 0;
            card.Bind(offer, null);
            Assert.That(label.text, Is.EqualTo("ARTIFACT"));
            Assert.That(icon.gameObject.activeSelf, Is.False);
            card.Bind(Offer(RoomKind.Rest), null);
            Assert.That(label.text, Is.EqualTo("RECOVERY"));
            Assert.That(icon.gameObject.activeSelf, Is.False);
            Assert.That(card.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(childCount));
        }
        finally
        {
            Object.DestroyImmediate(card.gameObject);
            Object.DestroyImmediate(artifact);
        }
    }

    [TestCase("UI_Soul")]
    [TestCase("UI_LevelUp_Icon")]
    public void SuppliedIconsKeepPixelArtImportSettings(string name)
    {
        string path = $"Assets/Project/UI/Room/{name}.PNG";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
    }

    [UnityTest]
    public IEnumerator SelectionIsStableAndDelayedCommitCanBeCancelledOrSubmittedOnlyOnce()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var ui = Object.Instantiate(Resources.Load<RoomChoiceUI>("UI/RoomChoiceUI"));
        var offers = new[] { Offer(RoomKind.Rest), Offer(RoomKind.Combat, RouteDifficulty.Hard, 242), Offer(RoomKind.Shop) };
        try
        {
            Time.timeScale = 0;
            ui.Show(offers);
            yield return null;
            var cards = Get<RoomChoiceCardView[]>(ui, "cards");
            int count = ui.GetComponentsInChildren<Transform>(true).Length;
            var card = cards[1];
            var rect = (RectTransform)card.transform;
            var size = rect.sizeDelta;
            var baseSprite = card.GetComponent<Image>().sprite;
            card.OnPointerEnter(new PointerEventData(EventSystem.current));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(card.gameObject));
            Assert.That(Get<Image>(card, "borderImage").overrideSprite, Is.EqualTo(Get<Sprite>(card, "selectedBorder")));
            Assert.That(Get<GameObject>(cards[0], "selectionMarker").activeSelf, Is.False);
            Assert.That(Get<TMP_Text>(card, "rewardOrZoneLabel").text, Is.EqualTo("242 SOUL"));
            Assert.That(Get<Image>(card, "soulIcon").gameObject.activeSelf, Is.True);
            Assert.That(Get<Image>(card, "soulIcon").sprite,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/UI/Room/UI_Soul.PNG")));
            Assert.That(Get<Image>(card, "rewardFrame").enabled, Is.False);
            Assert.That(Get<GameObject>(card, "difficultyRoot").activeSelf, Is.False);
            Assert.That(Get<Image>(card, "borderImage").color, Is.EqualTo(Get<TMP_Text>(card, "rewardOrZoneLabel").color));
            yield return new WaitForSecondsRealtime(.35f);
            Capture(ui.GetComponent<Canvas>(), "Rest_Combat_Shop", 1920, 1080);
            Capture(ui.GetComponent<Canvas>(), "Rest_Combat_Shop_720p", 1280, 720);
            Capture(ui.GetComponent<Canvas>(), "Rest_Combat_Shop_4x3", 1024, 768);
            Capture(ui.GetComponent<Canvas>(), "Rest_Combat_Shop_Ultrawide", 2560, 1080);
            Assert.That(rect.sizeDelta, Is.EqualTo(size));
            Assert.That(card.GetComponent<Image>().sprite, Is.EqualTo(baseSprite));
            Assert.That(card.GetComponent<Image>().color, Is.EqualTo(Color.white));
            card.Button.onClick.Invoke();
            Assert.That(ui.HasCommittedChoice, Is.True);
            Assert.That(ui.IsShowing, Is.True, "Show selection feedback before transition");
            Assert.That(cards.All(x => !x.Button.interactable), Is.True);
            Assert.That(Get<Image>(card, "borderImage").overrideSprite, Is.EqualTo(Get<Sprite>(card, "selectedBorder")));
            ui.Hide();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ui.HasCommittedChoice, Is.False);
            Assert.That(ActiveRouteOffer.HasSelection, Is.False);
            ui.Show(offers);
            LogAssert.Expect(LogType.Error, "[RoomChoiceUI] SceneTransitionManager.Instance is missing.");
            cards[1].Button.onClick.Invoke();
            cards[2].Button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ui.IsShowing, Is.False);
            Assert.That(ui.HasCommittedChoice, Is.False);
            Assert.That(ActiveRouteOffer.HasSelection, Is.False);
            ui.Show(new[] { Offer(RoomKind.Boss), Offer(RoomKind.Combat, RouteDifficulty.Medium), Offer(RoomKind.Combat) });
            yield return new WaitForSecondsRealtime(.35f);
            cards[1].OnPointerEnter(new PointerEventData(EventSystem.current));
            Capture(ui.GetComponent<Canvas>(), "Boss_Medium_Easy", 1920, 1080);
            ui.Show(new[] { Offer(RoomKind.Level) });
            yield return new WaitForSecondsRealtime(.35f);
            Capture(ui.GetComponent<Canvas>(), "Level_Icon", 1280, 720);
            Assert.That(cards.Count(x => x.gameObject.activeSelf), Is.EqualTo(1));
            var localCenter = cards[0].transform.parent.InverseTransformPoint(cards[0].transform.position);
            Assert.That(Mathf.Abs(localCenter.x), Is.LessThan(1));
            Assert.That(ui.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        }
        finally { Time.timeScale = 1; Object.Destroy(ui.gameObject); ActiveRouteOffer.Clear(); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator WasdStaysInsideRoomChoicesWithOtherHudButtonsPresent()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        var previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        // Batch-mode has no focused Game view; route the synthetic keyboard to Play Mode.
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.EnableDevice(keyboard);
        var ui = Object.Instantiate(Resources.Load<RoomChoiceUI>("UI/RoomChoiceUI"));
        var hud = new GameObject("Other HUD", typeof(RectTransform), typeof(Canvas));
        hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var otherButton = new GameObject("HUD button outside room choices", typeof(RectTransform), typeof(Image), typeof(Button));
        otherButton.transform.SetParent(hud.transform, false);
        var offers = new[] { Offer(RoomKind.Rest), Offer(RoomKind.Combat), Offer(RoomKind.Shop) };
        try
        {
            Time.timeScale = 0f;
            ui.Show(offers);
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
            var cards = Get<RoomChoiceCardView[]>(ui, "cards");
            otherButton.transform.position = cards[0].transform.position + Vector3.up * 300f;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[0].gameObject));
            Assert.That(InputLockManager.IsLocked, Is.True);

            yield return Tap(Key.W);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[0].gameObject), "W must not navigate into the HUD");
            yield return Tap(Key.D);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[1].gameObject));
            yield return Tap(Key.S);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[2].gameObject));
            yield return Tap(Key.D);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[2].gameObject));
            yield return Tap(Key.A);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[1].gameObject));
            yield return Tap(Key.W);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[0].gameObject));

            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(cards[2].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[2].gameObject));
            AssertSingleSelectedCard(cards, 2);
            yield return Tap(Key.W);
            AssertSingleSelectedCard(cards, 1);
            yield return Tap(Key.A);
            AssertSingleSelectedCard(cards, 0);
            Capture(ui.GetComponent<Canvas>(), "Keyboard_Selected_Mouse_Hover_Other", 1280, 720);
            ExecuteEvents.Execute(cards[2].gameObject, pointer, ExecuteEvents.pointerExitHandler);
            AssertSingleSelectedCard(cards, 0);

            ExecuteEvents.Execute(cards[1].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            AssertSingleSelectedCard(cards, 1);
            ExecuteEvents.Execute(cards[1].gameObject, pointer, ExecuteEvents.pointerExitHandler);
            AssertSingleSelectedCard(cards, 1);

            ui.Show(new[] { offers[0] });
            foreach (var key in new[] { Key.W, Key.A, Key.S, Key.D })
            {
                yield return Tap(key);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[0].gameObject), "One-card offers cannot navigate into hidden cards or other UI");
            }
            ui.Hide();
            Assert.That(InputLockManager.IsLocked, Is.False);
        }
        finally
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(ui.gameObject);
            Object.DestroyImmediate(hud);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
        }
        yield return new ExitPlayMode();
    }

    private static void AssertSingleSelectedCard(RoomChoiceCardView[] cards, int selected)
    {
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cards[selected].gameObject));
        for (int i = 0; i < cards.Length; i++)
        {
            Assert.That(Get<Image>(cards[i], "borderImage").overrideSprite,
                Is.EqualTo(Get<Sprite>(cards[i], i == selected ? "selectedBorder" : "normalBorder")),
                $"Card {i}: only the focused card may have a yellow border");
            Assert.That(Get<GameObject>(cards[i], "selectionMarker").activeSelf, Is.EqualTo(i == selected));
        }
    }

    private static IEnumerator Tap(Key key)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        InputSystem.Update();
        var module = EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        Assert.That(Keyboard.current[key].isPressed, Is.True, "Synthetic keyboard state must be processed in Play Mode");
        Assert.That(module.move.action.ReadValue<Vector2>().sqrMagnitude, Is.GreaterThan(.5f), "Keyboard input must reach the UI Navigate action");
        module.Process();
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();
        module.Process();
        yield return null;
        yield return null;
    }

    private static void Capture(Canvas canvas, string name, int width, int height)
    {
        Directory.CreateDirectory("Logs/RoomChoicePreviews");
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        float distance = canvas.planeDistance;
        var host = new GameObject("Room choice capture camera", typeof(Camera));
        var camera = host.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -100);
        camera.backgroundColor = new Color(.025f, .035f, .06f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        var target = new RenderTexture(width, height, 24);
        target.Create();
        camera.targetTexture = target;
        var previous = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
            Canvas.ForceUpdateCanvases();
            var panel = Get<GameObject>(canvas.GetComponent<RoomChoiceUI>(), "panelRoot").GetComponent<RectTransform>();
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var screen = camera.WorldToViewportPoint(corner);
                Assert.That(screen.x, Is.InRange(0f, 1f), name);
                Assert.That(screen.y, Is.InRange(0f, 1f), name);
            }
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, name + ": " + label.text);
            }
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            Assert.That(image.GetPixels32().Select(x => ((int)x.r << 16) | ((int)x.g << 8) | x.b).Distinct().Take(20).Count(), Is.GreaterThan(8));
            File.WriteAllBytes($"Logs/RoomChoicePreviews/{name}.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
            Object.DestroyImmediate(image); Object.DestroyImmediate(host); Object.DestroyImmediate(target);
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        foreach (var room in rooms) if (room != null) Object.DestroyImmediate(room);
        rooms.Clear();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
