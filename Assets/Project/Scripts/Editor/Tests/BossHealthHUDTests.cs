#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class BossHealthHUDTests
{
    private const string HudPath = "Assets/Project/Resources/UI/BossHealthHUD.prefab";
    private const string BossPath = "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

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
            foreach (string field in new[] { "panelRoot", "healthSlider", "damageLerpSlider", "healthFill", "nameText", "remainingBarsText" })
                Assert.That(data.FindProperty(field).objectReferenceValue,Is.Not.Null,field);
            Assert.That(data.FindProperty("layerColors").arraySize,Is.EqualTo(5));
        }
        Assert.That(prefab.GetComponentsInChildren<Slider>(true).Length,Is.EqualTo(2),"One HP fill and one delayed damage trail, not five bars.");
        foreach (var graphic in prefab.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.raycastTarget,Is.False,graphic.name);
        Assert.That(prefab.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
        Assert.That(prefab.GetComponent<Canvas>().renderMode,Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(prefab.GetComponent<CanvasScaler>().referenceResolution,Is.EqualTo(new Vector2(1920,1080)));
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
                foreach (string field in new[] { "panelRoot", "healthSlider", "damageLerpSlider", "healthFill", "nameText", "remainingBarsText" })
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

    [TestCase("Enm101/Enemy1.prefab",750f)]
    [TestCase("Enm102/Enemy102.prefab",300f)]
    [TestCase("Enm103/Enemy103.prefab",900f)]
    public void NormalEnemyHealthTuningIsSavedOnTheIndividualPrefab(string path,float hp)
    {
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/"+path).GetComponent<EnemyController>();
        Assert.That(enemy.maxHP,Is.EqualTo(hp));
        Assert.That(enemy.currentHP,Is.EqualTo(hp));
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
            Assert.That(data.FindProperty("defaultDefense").floatValue,Is.EqualTo(500f));
        }
    }
}
#endif
