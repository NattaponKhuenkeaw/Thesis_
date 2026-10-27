#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Progression.LevelRoom;
using Project.Progression.RestRoom;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SafeRoomDisplayPrefabTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string DisplayPath(string kind) => $"Assets/Project/Prefab/UI/{kind}RoomDisplayCanvas.prefab";
    private static string RoomPath(string kind) => $"Assets/Project/Resources/RoomLayouts/RoomLayout_{kind}_01.prefab";
    private static MonoBehaviour Display(GameObject root,string kind) => kind == "Rest"
        ? (MonoBehaviour)root.GetComponentInChildren<RestRoomDisplay>(true)
        : root.GetComponentInChildren<LevelRoomDisplay>(true);
    private static MonoBehaviour Controller(GameObject root,string kind) => kind == "Rest"
        ? (MonoBehaviour)root.GetComponentInChildren<RestRoomController>(true)
        : root.GetComponentInChildren<LevelRoomController>(true);
    private static MonoBehaviour Interactable(GameObject root,string kind) => kind == "Rest"
        ? (MonoBehaviour)root.GetComponentInChildren<RestRoomInteractable>(true)
        : root.GetComponentInChildren<LevelRoomInteractable>(true);
    private static object Get(object target,string field) => target.GetType().GetField(field,Private).GetValue(target);
    private static void Call(object target,string method) => target.GetType().GetMethod(method,Private).Invoke(target,null);

    [TestCase("Rest")]
    [TestCase("Level")]
    public void DisplayPrefabIsAnEditableWorldCanvasWithLocalTextReferences(string kind)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DisplayPath(kind));
        Assert.That(prefab,Is.Not.Null);
        var display = Display(prefab,kind);
        Assert.That(display.gameObject,Is.EqualTo(prefab));
        Assert.That(prefab.GetComponent<Canvas>().renderMode,Is.EqualTo(RenderMode.WorldSpace));
        Assert.That(prefab.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
        Assert.That(prefab.GetComponent<GraphicRaycaster>(),Is.Null);
        using (var data = new SerializedObject(display))
        {
            foreach (string field in new[] { "numberText", "statusText" })
            {
                var text = data.FindProperty(field).objectReferenceValue as TMP_Text;
                Assert.That(text,Is.Not.Null,field);
                Assert.That(text.transform.IsChildOf(prefab.transform),Is.True,field);
                Assert.That(text.raycastTarget,Is.False,field);
            }
        }
        foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject),Is.Zero,child.name);
        Assert.That(prefab.GetComponentInChildren<RestRoomController>(true),Is.Null);
        Assert.That(prefab.GetComponentInChildren<LevelRoomController>(true),Is.Null);
    }

    [TestCase("Rest")]
    [TestCase("Level")]
    public void SafeRoomScreenUsesAuthoredPixelArtworkAndKeepsTheFrameOutsideReadoutAnimation(string kind)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(DisplayPath(kind));
        Assert.That(root.GetComponent<Canvas>().sortingLayerName,Is.EqualTo("WorldUI"),"Foreground must not obscure the room readout.");
        var frame = root.transform.Find("DisplayFrame");
        Assert.That(frame,Is.Not.Null);
        Assert.That(frame.GetComponent<Image>().type,Is.EqualTo(Image.Type.Sliced));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(frame.GetComponent<Image>().sprite)),
            Is.EqualTo("6eb62d2d1f6dc67409980f6b4578602c"));
        Assert.That(frame.Find("RoomIcon").GetComponent<Image>().sprite,Is.Not.Null);
        Assert.That(frame.Find("RoomTitle").GetComponent<TMP_Text>().text,Is.EqualTo(kind == "Rest" ? "REST" : "LEVEL UP"));
        Assert.That(frame.Find("Divider").GetComponent<Image>().sprite,Is.Not.Null);
        Assert.That(frame.parent,Is.EqualTo(root.transform),"Cycling numbers must not fade the screen frame.");
        Assert.That(root.transform.Find("ReadoutContent/ReadoutLayout/NumberText"),Is.Not.Null);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.That(AssetDatabase.GetAssetPath(text.font),
                Is.EqualTo("Assets/Project/Fonts/GNF World UI Pixel.asset"),text.name);
            Assert.That(text.fontSharedMaterial.shader.name,Is.EqualTo("TextMeshPro/Mobile/Bitmap"));
            Assert.That(text.characterSpacing,Is.Zero);
        }
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.raycastTarget,Is.False,graphic.name);
    }

    [Test]
    public void ScreenBitmapFontHasTransparentGlyphPaddingInsteadOfDistanceFieldQuads()
    {
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Project/Fonts/GNF World UI Pixel.asset");
        Assert.That(font,Is.Not.Null);
        Assert.That(font.atlasPopulationMode,Is.EqualTo(AtlasPopulationMode.Static));
        Assert.That(font.atlasTexture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(font.material.mainTexture,Is.SameAs(font.atlasTexture));
        foreach (char symbol in "LEVEL UPrestInac?+%0123456789\u00d7\u2192")
        {
            Assert.That(font.HasCharacter(symbol),Is.True,symbol.ToString());
            if (char.IsWhiteSpace(symbol)) continue;
            var rect=font.characterLookupTable[symbol].glyph.glyphRect;
            if (rect.width==0 || rect.height==0) continue;
            Assert.That(font.atlasTexture.GetPixel(rect.x-1,rect.y+rect.height/2).a,Is.Zero,symbol+" left padding");
            Assert.That(font.atlasTexture.GetPixel(rect.x+rect.width/2,rect.y-1).a,Is.Zero,symbol+" bottom padding");
        }
    }

    [TestCase("Rest",1280,720,false)]
    [TestCase("Level",1280,720,false)]
    [TestCase("Rest",1024,768,false)]
    [TestCase("Level",1024,768,false)]
    [TestCase("Rest",1280,720,true)]
    [TestCase("Level",1280,720,true)]
    [TestCase("Rest",400,225,false)]
    [TestCase("Level",400,225,false)]
    public void SafeRoomScreensRenderIdleSelectedWorkingResultAndClaimedStates(string kind,int width,int height,bool inRoom)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(inRoom ? RoomPath(kind) : DisplayPath(kind)));
        var display = Display(root,kind);
        var host = new GameObject("Safe room screen capture",typeof(Camera));
        var camera = host.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = inRoom ? 8.5f : Mathf.Max(4.5f,8f/((float)width/height)+.5f);
        camera.transform.position = display.transform.position + new Vector3(0,inRoom ? -2 : 0,-100);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f,.035f,.055f);
        var target = new RenderTexture(width,height,24);
        target.Create();
        camera.targetTexture = target;
        var previous = RenderTexture.active;
        var image = new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            Directory.CreateDirectory("Logs/SafeRoomScreenPreviews");
            foreach (string state in new[] { "Idle","Selected","Working","Result","Claimed","Capped" })
            {
                if (kind == "Rest")
                {
                    var rest = (RestRoomDisplay)display;
                    switch (state)
                    {
                        case "Idle": rest.ShowIdle(); break;
                        case "Selected": rest.ShowSelected(); break;
                        case "Working": rest.ShowHealing(); break;
                        case "Result": rest.ShowHealed(.3f); break;
                        case "Claimed": rest.ShowClaimed(); break;
                        default: rest.ShowHealed(); break;
                    }
                }
                else
                {
                    var level = (LevelRoomDisplay)display;
                    switch (state)
                    {
                        case "Idle": level.ShowIdle(); break;
                        case "Selected": level.ShowSelected(); break;
                        case "Working": level.ShowCycling(2); break;
                        case "Result": level.ShowReveal(3,2,30,30); break;
                        case "Claimed": level.ShowClaimed(); break;
                        default: level.ShowReveal(3,0,30,30); break;
                    }
                }
                Canvas.ForceUpdateCanvases();
                foreach (var text in display.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.ForceMeshUpdate();
                    Assert.That(text.isTextOverflowing,Is.False,$"{kind} {state} {text.name}");
                    foreach (var character in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(x => x.isVisible))
                        Assert.That(character.textElement.unicode,Is.EqualTo((uint)character.character),
                            $"{kind} {state} {text.name} must not display a missing-glyph square.");
                }
                var corners = new Vector3[4];
                ((RectTransform)display.transform.Find("DisplayFrame")).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 point = camera.WorldToViewportPoint(corner);
                    Assert.That(point.x,Is.InRange(0f,1f));
                    Assert.That(point.y,Is.InRange(0f,1f));
                }
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,width,height),0,0);
                image.Apply();
                Assert.That(image.GetPixels32().Select(x => ((int)x.r<<16)|((int)x.g<<8)|x.b).Distinct().Take(20).Count(),
                    Is.GreaterThan(8),"Screen must not render blank");
                File.WriteAllBytes($"Logs/SafeRoomScreenPreviews/{kind}_{state}_{width}x{height}{(inRoom ? "_InRoom" : "")}.png",image.EncodeToPNG());
            }
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }

    [TestCase("Rest")]
    [TestCase("Level")]
    public void RoomAndInteractableReferenceTheNestedDisplayWithoutMovingTheCameraFocus(string kind)
    {
        var room = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath(kind));
        var display = Display(room,kind);
        var controller = Controller(room,kind);
        var canvas = display.GetComponent<Canvas>();
        Assert.That(canvas,Is.Not.Null);
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(canvas),
            Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(DisplayPath(kind)).GetComponent<Canvas>()));
        Assert.That(Get(controller,"display"),Is.EqualTo(display));
        Assert.That(Get(controller,"sequenceCanvas"),Is.EqualTo(canvas));
        Assert.That(Get(Interactable(room,kind),"display"),Is.EqualTo(display));
        var focus = (Transform)Get(controller,"cameraFocusTarget");
        Assert.That(focus.name,Is.EqualTo("BackgroundDisplay"));
        Assert.That(display.transform.parent,Is.EqualTo(focus));
        Assert.That(focus.localPosition,Is.EqualTo(new Vector3(-.49f,6.04f,0f)));
        Assert.That(Get(Interactable(room,kind),"buttonVisual"),Is.EqualTo(focus.GetComponent<SpriteRenderer>()));
        Assert.That(focus.GetComponent<SpriteRenderer>().enabled,Is.False,"The legacy solid rectangle must not create a pale border behind the pixel frame.");
        Assert.That(((Collider2D)Interactable(room,kind).GetComponent<Collider2D>()).isTrigger,Is.True);
        if (kind == "Rest") Assert.That((float)Get(controller,"healPercentOfMaxHp"),Is.EqualTo(.3f));
        else Assert.That((float)Get(controller,"cycleDurationSeconds"),Is.EqualTo(1.4f));
        Assert.That((float)Get(controller,"cameraReturnBlendSeconds"),Is.EqualTo(.65f));
        Assert.That(Get(controller,kind == "Rest" ? "healClip" : "levelUpClip"),Is.Not.Null);
    }

    [Test]
    public void DefaultAndPixelFontsDoNotAddWhiteOutlineOrDuplicateUnderlay()
    {
        foreach (string path in new[] {
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset",
            "Assets/Project/Fonts/GNF TTF SDF.asset" })
        {
            var material=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path).material;
            Assert.That(material.IsKeywordEnabled("OUTLINE_ON"),Is.False,path);
            Assert.That(material.IsKeywordEnabled("UNDERLAY_ON"),Is.False,path);
            Assert.That(material.GetFloat("_OutlineWidth"),Is.Zero,path);
        }
    }

    [UnityTest]
    public IEnumerator InGameSafeRoomScreensStayCleanAfterIdleAndSelectedTintChanges()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (string kind in new[] { "Rest","Level" })
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath(kind)));
            try
            {
                yield return new WaitForSecondsRealtime(.3f);
                var display=Display(root,kind);
                var focus=(Transform)Get(Controller(root,kind),"cameraFocusTarget");
                Assert.That(focus.GetComponent<SpriteRenderer>().enabled,Is.False);
                CaptureInGameRoom(root,kind,"Idle");
                CaptureInGameRoom(root,kind,"Idle_Distant",14.5f);
                Call(Interactable(root,kind),"ApplySelectedVisual");
                display.GetType().GetMethod("ShowSelected").Invoke(display,null);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(focus.GetComponent<SpriteRenderer>().enabled,Is.False);
                foreach (var text in display.GetComponentsInChildren<TMP_Text>(true))
                {
                    Assert.That(text.fontSharedMaterial.IsKeywordEnabled("OUTLINE_ON"),Is.False,text.name);
                    Assert.That(text.fontSharedMaterial.IsKeywordEnabled("UNDERLAY_ON"),Is.False,text.name);
                }
                CaptureInGameRoom(root,kind,"Selected");
                CaptureInGameRoom(root,kind,"Selected_Distant",14.5f);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
        yield return new ExitPlayMode();
    }

    private static void CaptureInGameRoom(GameObject root,string kind,string state,float cameraSize=8.5f)
    {
        var display=Display(root,kind);
        var host=new GameObject("In-game safe room capture",typeof(Camera));
        var camera=host.GetComponent<Camera>();
        camera.enabled=false; camera.orthographic=true; camera.orthographicSize=cameraSize;
        camera.transform.position=display.transform.position+new Vector3(0,-2,-100);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.035f,.055f);
        var target=new RenderTexture(1280,720,24); target.Create(); camera.targetTexture=target;
        var previous=RenderTexture.active;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
            RenderTexture.active=target; image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            Directory.CreateDirectory("Logs/SafeRoomScreenPreviews");
            File.WriteAllBytes($"Logs/SafeRoomScreenPreviews/{kind}_{state}_PlayMode.png",image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;
            Object.DestroyImmediate(host); Object.DestroyImmediate(image); Object.DestroyImmediate(target);
        }
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterScreenTest()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [TestCase("Rest")]
    [TestCase("Level")]
    public void SequenceKeepsItsDisplayVisibleAndRestoresHudAndInputOnAbort(string kind)
    {
        var room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath(kind)));
        var hudHost = new GameObject("Enabled HUD",typeof(Canvas));
        var disabledHost = new GameObject("Disabled HUD",typeof(Canvas));
        var controller = Controller(room,kind);
        bool wasLocked = InputLockManager.IsLocked;
        disabledHost.GetComponent<Canvas>().enabled = false;
        controller.GetType().GetField("cameraFocusTarget",Private).SetValue(controller,null);
        try
        {
            Call(controller,"AcquirePresentationOwnership");
            Assert.That(InputLockManager.IsLocked,Is.True);
            Assert.That(Display(room,kind).GetComponent<Canvas>().enabled,Is.True);
            Assert.That(hudHost.GetComponent<Canvas>().enabled,Is.False);
            Call(controller,"OnDisable");
            Assert.That(hudHost.GetComponent<Canvas>().enabled,Is.True);
            Assert.That(disabledHost.GetComponent<Canvas>().enabled,Is.False);
            Assert.That(InputLockManager.IsLocked,Is.EqualTo(wasLocked));
        }
        finally
        {
            Call(controller,"AbortSequenceAndRestoreOwnership");
            Object.DestroyImmediate(room); Object.DestroyImmediate(hudHost); Object.DestroyImmediate(disabledHost);
        }
    }

    [Test]
    public void RestDisplayShowsTheExistingHealStatesWithoutCreatingMoreUi()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DisplayPath("Rest")));
        try
        {
            var display = root.GetComponent<RestRoomDisplay>();
            var number = (TMP_Text)Get(display,"numberText");
            var status = (TMP_Text)Get(display,"statusText");
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            display.ShowIdle(); Assert.That(number.text,Is.EqualTo("+"));
            display.ShowSelected(); Assert.That(status.text,Is.EqualTo("Press Interact to heal"));
            display.ShowHealing(); Assert.That(status.text,Is.EqualTo("Restoring team..."));
            display.ShowHealed(.3f); Assert.That(status.text,Is.EqualTo("Restored 30% Max HP"));
            display.ShowHealed(1f); Assert.That(status.text,Is.EqualTo("Team restored"));
            display.ShowClaimed(); Assert.That(status.text,Is.EqualTo("Already rested"));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(count));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void LevelDisplayShowsUpgradeResultsAndCapWithoutCreatingMoreUi()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DisplayPath("Level")));
        try
        {
            var display = root.GetComponent<LevelRoomDisplay>();
            var number = (TMP_Text)Get(display,"numberText");
            var status = (TMP_Text)Get(display,"statusText");
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            display.ShowIdle(); Assert.That(number.text,Is.EqualTo("-"));
            display.ShowSelected(); Assert.That(number.text,Is.EqualTo("?"));
            display.ShowCycling(2); Assert.That(number.text,Is.EqualTo("2"));
            display.ShowReveal(3,2,30,30);
            Assert.That(number.text,Is.EqualTo("3"));
            Assert.That(status.text,Does.StartWith("+2").And.EndWith("Lv.30/30"));
            display.ShowReveal(3,0,30,30); Assert.That(status.text,Is.EqualTo("Max Lv.30"));
            display.ShowClaimed(); Assert.That(status.text,Is.EqualTo("Already claimed"));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(count));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase("Rest")]
    [TestCase("Level")]
    public void ClaimRemainsConsumedWhenInteractingAgain(string kind)
    {
        var room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath(kind)));
        try
        {
            var controller = Controller(room,kind);
            controller.GetType().GetField("hasClaimedThisVisit",Private).SetValue(controller,true);
            var result = controller.GetType().GetMethod("TryBeginSequence").Invoke(controller,null);
            Assert.That(result,Is.False);
            Assert.That(((TMP_Text)Get(Display(room,kind),"statusText")).text,
                Is.EqualTo(kind == "Rest" ? "Already rested" : "Already claimed"));
        }
        finally { Object.DestroyImmediate(room); }
    }
}
#endif
