#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
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

public class BossHealthHUDTests
{
    private const string HudPath = "Assets/Project/Resources/UI/BossHealthHUD.prefab";
    private const string BossPath = "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Get<T>(object host,string field) => (T)host.GetType().GetField(field,Private).GetValue(host);

    [TestCase(4000f, 5, 1f)]
    [TestCase(3600f, 5, .5f)]
    [TestCase(3200f, 4, 1f)]
    [TestCase(2800f, 4, .5f)]
    [TestCase(2400f, 3, 1f)]
    [TestCase(1600f, 2, 1f)]
    [TestCase(800f, 1, 1f)]
    [TestCase(400f, 1, .5f)]
    [TestCase(1f, 1, .00125f)]
    [TestCase(0f, 0, 0f)]
    [TestCase(-100f, 0, 0f)]
    [TestCase(5000f, 5, 1f)]
    public void LayersCarryDamageAcrossBoundariesWithoutFreeHealth(float hp, int expectedBars, float expectedFill)
    {
        BossHealthHUD.CalculateLayers(hp,4000f,5,out int bars,out float fill);
        Assert.That(bars,Is.EqualTo(expectedBars));
        Assert.That(fill,Is.EqualTo(expectedFill).Within(.00001f));
    }

    [Test]
    public void LayerDisplayUsesScaledMaxHealthAndHandlesInvalidMaxHealth()
    {
        BossHealthHUD.CalculateLayers(5600f,8000f,5,out int bars,out float fill);
        Assert.That(bars,Is.EqualTo(4));
        Assert.That(fill,Is.EqualTo(.5f));
        BossHealthHUD.CalculateLayers(100f,0f,5,out bars,out fill);
        Assert.That(bars,Is.Zero);
        Assert.That(fill,Is.Zero);
        BossHealthHUD.CalculateLayers(50f,100f,0,out bars,out fill);
        Assert.That(bars,Is.EqualTo(1));
        Assert.That(fill,Is.EqualTo(.5f));
    }

    [Test]
    public void SavedPrefabContainsAllEditableWidgetsAndDoesNotBlockPlayerInput()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        Assert.That(prefab,Is.Not.Null);
        var hud = prefab.GetComponent<BossHealthHUD>();
        using (var data = new SerializedObject(hud))
        {
            foreach (string field in new[] { "panelRoot", "healthSlider", "damageLerpSlider", "healthFill", "nextLayerFill", "damageTrailFill", "nameText", "remainingBarsText" })
                Assert.That(data.FindProperty(field).objectReferenceValue,Is.Not.Null,field);
            Assert.That(data.FindProperty("layerColors").arraySize,Is.EqualTo(5));
        }
        Assert.That(prefab.GetComponentsInChildren<Slider>(true).Length,Is.EqualTo(2),"One HP fill and one delayed damage trail, not five bars.");
        foreach (var graphic in prefab.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.raycastTarget,Is.False,graphic.name);
        Assert.That(prefab.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
        Assert.That(prefab.GetComponent<Canvas>().renderMode,Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(prefab.GetComponent<CanvasScaler>().referenceResolution,Is.EqualTo(new Vector2(1920,1080)));
        var underlay = Get<Image>(hud,"nextLayerFill");
        var health = Get<Slider>(hud,"healthSlider");
        var trail = Get<Slider>(hud,"damageLerpSlider");
        Assert.That(underlay.color.a,Is.EqualTo(1f));
        Assert.That(underlay.rectTransform.sizeDelta,Is.EqualTo(((RectTransform)health.transform).sizeDelta));
        Assert.That(underlay.transform.GetSiblingIndex(),Is.LessThan(trail.transform.GetSiblingIndex()));
        Assert.That(trail.transform.GetSiblingIndex(),Is.LessThan(health.transform.GetSiblingIndex()));
    }

    [Test]
    public void SavedHudUsesTheMenuPixelAssetsWithoutTakingNavigationFocus()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        var panel = prefab.transform.Find("BossHealthBar");
        var frame = panel.GetComponent<Image>();
        var track = panel.Find("HpBar/Track").GetComponent<Image>();
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(frame.sprite)),
            Is.EqualTo("6eb62d2d1f6dc67409980f6b4578602c"));
        Assert.That(frame.type,Is.EqualTo(Image.Type.Sliced));
        Assert.That(track.color.a,Is.EqualTo(1f));
        Assert.That(track.color.grayscale,Is.GreaterThan(.15f),"Missing HP must remain visible against the dark panel.");
        foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(text.font)),
                Is.EqualTo("1b2fdb289d38c5a49852fac97ab54c2f"));
            Assert.That(text.fontStyle,Is.EqualTo(FontStyles.Normal));
        }
        foreach (var slider in prefab.GetComponentsInChildren<Slider>(true))
        {
            Assert.That(slider.interactable,Is.False);
            Assert.That(slider.navigation.mode,Is.EqualTo(Navigation.Mode.None));
            Assert.That(((RectTransform)slider.transform).sizeDelta,Is.EqualTo(new Vector2(-8,-8)));
        }
    }

    [TestCase(1280,720)]
    [TestCase(1024,768)]
    public void PixelHudRendersAllFiveLayersAndFitsTheViewport(int width,int height)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
        var host = new GameObject("Boss HUD visual test");
        host.SetActive(false);
        var cameraHost = new GameObject("Boss HUD capture",typeof(Camera));
        var camera = cameraHost.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.transform.position = new Vector3(0,0,-100);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f,.035f,.055f);
        var target = new RenderTexture(width,height,24);
        target.Create();
        camera.targetTexture = target;
        var previous = RenderTexture.active;
        var image = new Texture2D(width,136,TextureFormat.RGB24,false);
        try
        {
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var enemy = host.AddComponent<EnemyController>();
            enemy.maxHP = 4000;
            var hud = root.GetComponent<BossHealthHUD>();
            var panel = (RectTransform)root.transform.Find("BossHealthBar");
            var count = panel.Find("RemainingBars").GetComponent<TMP_Text>();
            var name = panel.Find("BossName").GetComponent<TMP_Text>();
            var underlay = Get<Image>(hud,"nextLayerFill");
            Directory.CreateDirectory("Logs/BossHudPreviews");
            for (int layer = 5; layer >= 1; layer--)
            {
                enemy.currentHP = (layer-1)*800+400;
                hud.Bind(enemy,"Executioner",5);
                Canvas.ForceUpdateCanvases();
                Assert.That(count.text,Is.EqualTo("\u00d7"+layer));
                foreach (var text in new[] { name,count })
                {
                    text.ForceMeshUpdate();
                    Assert.That(text.isTextOverflowing,Is.False,text.name);
                }
                var corners = new Vector3[4];
                panel.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 point = camera.WorldToViewportPoint(corner);
                    Assert.That(point.x,Is.InRange(0f,1f));
                    Assert.That(point.y,Is.InRange(0f,1f));
                }
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0,height-136,width,136),0,0);
                image.Apply();
                var area = underlay.rectTransform.rect;
                foreach (float fraction in new[] { .25f,.75f })
                {
                    Vector3 sample = camera.WorldToScreenPoint(underlay.transform.TransformPoint(
                        new Vector3(area.xMin+area.width*fraction,area.center.y,0)));
                    Color actual = image.GetPixel(Mathf.RoundToInt(sample.x),Mathf.RoundToInt(sample.y)-(height-136));
                    Color expected = fraction<.5f ? Get<Image>(hud,"healthFill").color
                        : layer>1 ? underlay.color : panel.Find("HpBar/Track").GetComponent<Image>().color;
                    float distance = Mathf.Min(Vector3.Distance((Vector4)actual,(Vector4)expected),
                        Vector3.Distance((Vector4)actual,(Vector4)expected.gamma),Vector3.Distance((Vector4)actual,(Vector4)expected.linear));
                    Assert.That(distance,Is.LessThan(.06f),"The uncovered half must show the next color, not empty gray or a white trail.");
                }
                Assert.That(image.GetPixels32().Select(x => ((int)x.r<<16)|((int)x.g<<8)|x.b).Distinct().Take(20).Count(),
                    Is.GreaterThan(8),"HUD must not render blank");
                File.WriteAllBytes($"Logs/BossHudPreviews/Boss_{layer}_{width}x{height}.png",image.EncodeToPNG());
            }
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(cameraHost);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void EnsureInstanceLoadsTheSavedPrefabAndReusesIt()
    {
        var hud = BossHealthHUD.EnsureInstance();
        try
        {
            Assert.That(hud,Is.Not.Null);
            var source = Resources.Load<BossHealthHUD>("UI/BossHealthHUD");
            Assert.That(source,Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath).GetComponent<BossHealthHUD>()));
            Assert.That(hud.gameObject.name,Is.EqualTo(source.gameObject.name+"(Clone)"));
            using (var data = new SerializedObject(hud))
            {
                foreach (string field in new[] { "panelRoot", "healthSlider", "damageLerpSlider", "healthFill", "nextLayerFill", "damageTrailFill", "nameText", "remainingBarsText" })
                {
                    var reference = data.FindProperty(field).objectReferenceValue as Component;
                    if (reference != null)
                        Assert.That(reference.transform.IsChildOf(hud.transform),Is.True,field);
                    else
                        Assert.That(((GameObject)data.FindProperty(field).objectReferenceValue).transform.IsChildOf(hud.transform),Is.True,field);
                }
            }
            Assert.That(BossHealthHUD.EnsureInstance(),Is.SameAs(hud));
            Assert.That(hud.GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath).GetComponentsInChildren<Transform>(true).Length));
        }
        finally { if (hud != null) Object.DestroyImmediate(hud.gameObject); }
    }

    [UnityTest]
    public IEnumerator DamageBoundariesSkippedLayersAndHealingRevealTheNextColorImmediately()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        Time.timeScale = 1;
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
        var host = new GameObject("Layered boss HUD runtime test");
        host.SetActive(false);
        try
        {
            var enemy = host.AddComponent<EnemyController>();
            enemy.maxHP = enemy.currentHP = 4000f;
            var hud = root.GetComponent<BossHealthHUD>();
            hud.Bind(enemy,"Executioner",5);
            int hierarchy = root.GetComponentsInChildren<Transform>(true).Length;
            var underlay = Get<Image>(hud,"nextLayerFill");
            var trailFill = Get<Image>(hud,"damageTrailFill");
            var fill = Get<Image>(hud,"healthFill");
            var bar = Get<Slider>(hud,"healthSlider");
            var trail = Get<Slider>(hud,"damageLerpSlider");
            var colors = Get<Color[]>(hud,"layerColors");
            var damaged = typeof(BossHealthHUD).GetMethod("HandleDamaged",Private);
            foreach (float hp in new[] { 3600f,3201f,3200f,3199f,1700f,1599f,500f,3600f,500f,100f })
            {
                enemy.currentHP = hp;
                damaged.Invoke(hud,new object[] { default(HitReactionData) });
                BossHealthHUD.CalculateLayers(hp,4000,5,out int remaining,out float amount);
                Assert.That(bar.value,Is.EqualTo(amount).Within(.00001f));
                Assert.That(fill.color,Is.EqualTo(colors[remaining-1]));
                Assert.That(underlay.enabled,Is.EqualTo(remaining>1));
                Assert.That(trailFill.enabled,Is.EqualTo(remaining==1));
                if (remaining>1)
                {
                    Assert.That(underlay.color,Is.EqualTo(colors[remaining-2]));
                    Assert.That(underlay.rectTransform.anchorMin,Is.EqualTo(Vector2.zero));
                    Assert.That(underlay.rectTransform.anchorMax,Is.EqualTo(Vector2.one));
                }
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(hierarchy));
            }
            Assert.That(trail.value,Is.GreaterThan(bar.value),"The final red layer keeps its damage feedback.");
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(trail.value,Is.EqualTo(bar.value).Within(.001f));
            enemy.currentHP = 0;
            typeof(EnemyController).GetField("isAlive",Private).SetValue(enemy,false);
            typeof(BossHealthHUD).GetMethod("HandleDied",Private).Invoke(hud,null);
            Assert.That(underlay.enabled || trailFill.enabled,Is.False);
            Assert.That(hud.IsVisible,Is.False);
            hud.Clear();
            hud.Bind(enemy,"Defeated boss",1);
            Assert.That(hud.IsVisible,Is.False);
            enemy.currentHP = 4000;
            typeof(EnemyController).GetField("isAlive",Private).SetValue(enemy,true);
            hud.RefreshIfBound(enemy);
            Assert.That(underlay.enabled,Is.False,"A single-layer boss must not show phantom extra health.");
        }
        finally { Object.Destroy(root); Object.Destroy(host); Time.timeScale = 1; }
        yield return null;
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterLayerTest()
    {
        Time.timeScale = 1;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [Test]
    public void BindingChangingLayersHealingAndClearingReuseThePrefabHierarchy()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
        var host = new GameObject("Boss HUD Test");
        host.SetActive(false);
        try
        {
            var hud = root.GetComponent<BossHealthHUD>();
            var enemy = host.AddComponent<EnemyController>();
            enemy.maxHP = enemy.currentHP = 4000f;
            int transforms = root.GetComponentsInChildren<Transform>(true).Length;
            var bar = (Slider)typeof(BossHealthHUD).GetField("healthSlider",Private).GetValue(hud);
            var trail = (Slider)typeof(BossHealthHUD).GetField("damageLerpSlider",Private).GetValue(hud);
            var text = (TMP_Text)typeof(BossHealthHUD).GetField("remainingBarsText",Private).GetValue(hud);
            var fill = (Image)typeof(BossHealthHUD).GetField("healthFill",Private).GetValue(hud);
            hud.Bind(enemy,"Executioner",5);
            Assert.That(hud.IsVisible,Is.True);
            Assert.That(text.text,Is.EqualTo("\u00d75"));
            Color initialColor = fill.color;
            enemy.currentHP = 2800f;
            typeof(BossHealthHUD).GetMethod("HandleDamaged",Private).Invoke(hud,new object[] { default(HitReactionData) });
            Assert.That(text.text,Is.EqualTo("\u00d74"));
            Assert.That(bar.value,Is.EqualTo(.5f));
            Assert.That(trail.value,Is.EqualTo(.5f));
            Assert.That(fill.color,Is.Not.EqualTo(initialColor));
            enemy.currentHP = 400f;
            hud.RefreshIfBound(enemy);
            Assert.That(text.text,Is.EqualTo("\u00d71"));
            enemy.maxHP = 8000f; enemy.currentHP = 8000f;
            hud.RefreshIfBound(enemy);
            Assert.That(text.text,Is.EqualTo("\u00d75"));
            Assert.That(bar.value,Is.EqualTo(1f));
            enemy.currentHP = 0f;
            typeof(BossHealthHUD).GetMethod("HandleDied",Private).Invoke(hud,null);
            Assert.That(text.text,Is.EqualTo("\u00d70"));
            Assert.That(hud.IsVisible,Is.False);
            hud.Clear();
            Assert.That(hud.IsBound,Is.False);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(transforms));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(host); }
    }

    [TestCase("Enm101/Enemy1.prefab",200f,10f,10f,100f)]
    [TestCase("Enm102/Enemy102.prefab",100f,0f,0f,80f)]
    [TestCase("Enm103/Enemy103.prefab",260f,15f,10f,80f)]
    public void NormalEnemyHealthTuningIsSavedOnTheIndividualPrefab(string path,float hp,float defense,float resistance,float attack)
    {
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/"+path).GetComponent<EnemyController>();
        Assert.That(enemy.maxHP,Is.EqualTo(hp));
        Assert.That(enemy.currentHP,Is.EqualTo(hp));
        Assert.That(enemy.defense,Is.EqualTo(defense));
        Assert.That(enemy.damageRes,Is.EqualTo(resistance),"This tuning changes HP/DEF only.");
        Assert.That(enemy.attackDamage,Is.EqualTo(attack));
    }

    [TestCase("Enm101/Enemy1.prefab",200f,10f,0)]
    [TestCase("Enm101/Enemy1.prefab",200f,10f,1)]
    [TestCase("Enm101/Enemy1.prefab",200f,10f,5)]
    [TestCase("Enm102/Enemy102.prefab",100f,0f,0)]
    [TestCase("Enm102/Enemy102.prefab",100f,0f,1)]
    [TestCase("Enm102/Enemy102.prefab",100f,0f,5)]
    [TestCase("Enm103/Enemy103.prefab",260f,15f,0)]
    [TestCase("Enm103/Enemy103.prefab",260f,15f,1)]
    [TestCase("Enm103/Enemy103.prefab",260f,15f,5)]
    public void NormalEnemyBaseTuningCompoundsHealthAndAttackWithoutScalingDefense(string path,float hp,float defense,int biome)
    {
        var holder = new GameObject("Inactive enemy balance fixture");
        holder.SetActive(false);
        try
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Project/Character/enemy/enemy/"+path),holder.transform);
            var enemy = root.GetComponent<EnemyController>();
            float attack = enemy.attackDamage;
            float resistance = enemy.damageRes;
            float healthMultiplier = Project.Core.ZoneState.RunProgressionManager.ResolveNormalEnemyHealthMultiplier(biome);
            float attackMultiplier = Project.Core.ZoneState.RunProgressionManager.ResolveNormalEnemyAttackMultiplier(biome);
            Assert.That(healthMultiplier,Is.EqualTo(Mathf.Pow(1.4f,biome)).Within(.0001f));
            Assert.That(attackMultiplier,Is.EqualTo(Mathf.Pow(1.15f,biome)).Within(.0001f));
            enemy.ApplyStatMultipliers(healthMultiplier,attackMultiplier,1f);
            Assert.That(enemy.maxHP,Is.EqualTo(hp*healthMultiplier).Within(.001f));
            Assert.That(enemy.currentHP,Is.EqualTo(enemy.maxHP));
            Assert.That(enemy.defense,Is.EqualTo(defense));
            Assert.That(enemy.attackDamage,Is.EqualTo(attack*attackMultiplier).Within(.001f));
            Assert.That(enemy.damageRes,Is.EqualTo(resistance));
        }
        finally { Object.DestroyImmediate(holder); }
    }

    [Test]
    public void BossDefaultStatsCannotOverwriteItsFiveFullHealthLayers()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
        var enemy = prefab.GetComponent<EnemyController>();
        Assert.That(enemy.maxHP,Is.EqualTo(4000f));
        Assert.That(enemy.currentHP,Is.EqualTo(4000f));
        using (var data = new SerializedObject(prefab.GetComponent<Project.AIBehaviour.Boss.BossPatternController>()))
        {
            Assert.That(data.FindProperty("defaultMaxHp").floatValue,Is.EqualTo(enemy.maxHP));
            Assert.That(data.FindProperty("healthBarCount").intValue,Is.EqualTo(5));
            Assert.That(data.FindProperty("defaultAttackDamage").floatValue,Is.EqualTo(165f));
            Assert.That(data.FindProperty("defaultDefense").floatValue,Is.Zero);
            Assert.That(enemy.defense,Is.Zero);
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(5)]
    public void BossInitializationAndBiomeScalingKeepDefenseAtZero(int biome)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BossPath));
        try
        {
            var enemy = root.GetComponent<EnemyController>();
            var controller = root.GetComponent<Project.AIBehaviour.Boss.BossPatternController>();
            enemy.defense = 500f;
            controller.GetType().GetField("statsApplied",Private).SetValue(controller,false);
            controller.GetType().GetMethod("ApplyDefaultStatsIfNeeded",Private).Invoke(controller,null);
            Assert.That(enemy.defense,Is.Zero);
            Assert.That(enemy.maxHP,Is.EqualTo(4000f));
            Assert.That(enemy.attackDamage,Is.EqualTo(165f));

            float multiplier = Project.Core.ZoneState.RunProgressionManager.ResolveEnemyBasicStatMultiplier(biome);
            float health = Project.Core.ZoneState.RunProgressionManager.ResolveBossHealthMultiplier(biome);
            enemy.ApplyStatMultipliers(health,multiplier,multiplier);
            Assert.That(enemy.defense,Is.Zero);
            Assert.That(enemy.maxHP,Is.EqualTo(4000f*health).Within(.01f));
            Assert.That(enemy.attackDamage,Is.EqualTo(165f*multiplier).Within(.001f));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
