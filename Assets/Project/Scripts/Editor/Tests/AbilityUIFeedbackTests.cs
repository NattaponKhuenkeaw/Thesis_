#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class AbilityUIFeedbackTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("AbilityUIFeedbackTests");
        host.SetActive(false);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
    }

    [Test]
    public void CharacterRefreshDoesNotStartSkillReadyRush()
    {
        var ui = host.AddComponent<AbilityUIManager>();
        Set(ui, "skillWasReadyLastFrame", false);
        Invoke(ui, "HandleCharacterChanged", new object[] { null });
        Assert.That(IsRushing(ui, "skillRush"), Is.False);
        Assert.That(Get(ui, "skillWasReadyLastFrame"), Is.True);
        Invoke(ui, "HandleCharacterChanged", new object[] { null });
        Assert.That(IsRushing(ui, "skillRush"), Is.False);
    }

    [Test]
    public void ReenableEstablishesReadyStateWithoutBounce()
    {
        var ui = host.AddComponent<AbilityUIManager>();
        Set(ui, "skillWasReadyLastFrame", false);
        Invoke(ui, "RefreshWithoutReadyFeedback");
        Invoke(ui, "UpdateSkillUI", string.Empty);
        Assert.That(IsRushing(ui, "skillRush"), Is.False);
        Assert.That(Get(ui, "suppressReadyFeedback"), Is.False);
    }

    [Test]
    public void DashDisplayReadsMovementFramesAndStartsOneRushOnExpiry()
    {
        var movement = host.AddComponent<PlayerMovement>();
        movement.dashData = new DashData { cooldownFrames = 30 };
        var player = host.AddComponent<PlayerCharacter>();
        player.Movement = movement;
        var ui = host.AddComponent<AbilityUIManager>();
        Set(ui, "playerCharacter", player);
        var imageObject = new GameObject("DashOverlay", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(host.transform);
        var overlay = imageObject.GetComponent<Image>();
        Set(ui, "dashCooldownOverlay", overlay);
        Set(movement, "dashCooldownFramesRemaining", 15);

        Assert.That(movement.DashCooldownRemaining, Is.EqualTo(15 * Time.fixedDeltaTime));
        Assert.That(movement.DashCooldownDuration, Is.EqualTo(30 * Time.fixedDeltaTime));
        Invoke(ui, "UpdateDashUI");
        Assert.That(overlay.fillAmount, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(overlay.gameObject.activeSelf, Is.True);
        Assert.That(IsRushing(ui, "dashRush"), Is.False);

        Set(movement, "dashCooldownFramesRemaining", 0);
        Invoke(ui, "UpdateDashUI");
        Assert.That(IsRushing(ui, "dashRush"), Is.True);
        Assert.That(Get(ui, "dashWasCoolingDown"), Is.False);
        Set(ui, "dashRush", System.Activator.CreateInstance(Get(ui, "dashRush").GetType()));
        Invoke(ui, "UpdateDashUI");
        Assert.That(IsRushing(ui, "dashRush"), Is.False);
    }

    [Test]
    public void RebindingAfterDashExpiryDoesNotQueueReadyFeedback()
    {
        var ui = host.AddComponent<AbilityUIManager>();
        Set(ui, "dashWasCoolingDown", true);
        Invoke(ui, "RefreshWithoutReadyFeedback");
        Assert.That(IsRushing(ui, "dashRush"), Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ZoneCommandTogglesCombatRootsEvenWithoutNavigation(bool hasNavigation)
    {
        var hud = host.AddComponent<PlayerHUD>();
        var hp = Child("PlayerHP_UI");
        var abilities = Child("Cool");
        var nav = hasNavigation ? Child("Navigation") : null;
        Set(hud, "hudRoot", hp.transform as RectTransform);
        Set(hud, "abilityRoot", abilities);
        Set(hud, "navigationRoot", nav);

        hud.OnZoneChanged(ZoneMode.SafeZone);
        Assert.That(hp.activeSelf, Is.False);
        Assert.That(abilities.activeSelf, Is.False);
        if (nav != null) Assert.That(nav.activeSelf, Is.True);

        hud.OnZoneChanged(ZoneMode.CombatZone);
        Assert.That(hp.activeSelf, Is.True);
        Assert.That(abilities.activeSelf, Is.True);
        if (nav != null) Assert.That(nav.activeSelf, Is.False);

        hud.OnZoneChanged(ZoneMode.SafeZone);
        Assert.That(hp.activeSelf, Is.False);
        Assert.That(abilities.activeSelf, Is.False);
        if (nav != null) Assert.That(nav.activeSelf, Is.True);
    }

    [Test]
    public void AuthoredPrefabWiresAllAbilitySlotsAndKeepsControllerOutsideHiddenRoots()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/CombatUI/Player_UI.prefab");
        Assert.That(prefab, Is.Not.Null);
        var ui = prefab.GetComponentInChildren<AbilityUIManager>(true);
        foreach (string field in new[] { "skillCooldownOverlay", "skillIcon", "skillCountdownText",
                     "ultCooldownOverlay", "ultIcon", "ultCountdownText", "ultWindowEffect",
                     "dashCooldownOverlay", "dashIcon", "dashCountdownText", "dashFeedbackRoot",
                     "normalAttackFeedbackRoot" })
            Assert.That(Get(ui, field), Is.Not.Null, field);
        var hud = prefab.GetComponentInChildren<PlayerHUD>(true);
        var abilityRoot = (GameObject)Get(hud, "abilityRoot");
        Assert.That(abilityRoot, Is.Not.Null);
        Assert.That(hud.transform.IsChildOf(abilityRoot.transform), Is.False);
        Assert.That(ui.transform.IsChildOf(abilityRoot.transform), Is.False);
    }

    [Test]
    public void SharedOutlineReusesItsGraphicAndNeverBlocksInput()
    {
        var root = new GameObject("Opportunity test", typeof(RectTransform), typeof(Image));
        try
        {
            var image = root.GetComponent<Image>();
            var effect = TimedOpportunityGraphic.GetOrCreate(image);
            Assert.That(TimedOpportunityGraphic.GetOrCreate(image), Is.SameAs(effect));
            Assert.That(effect.raycastTarget, Is.False);
            Assert.That(effect.maskable, Is.False);
            effect.SetPresentation(true, false, false);
            Assert.That(effect.IsVisible, Is.True);
            effect.Clear();
            Assert.That(effect.IsVisible, Is.False);
            Assert.That(root.activeSelf, Is.True);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ReducedMotionKeepsOnlyTheCoreAndPauseDoesNotAdvancePulse()
    {
        var root = new GameObject("Opportunity mesh test", typeof(RectTransform), typeof(Image));
        try
        {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 100f);
            var effect = TimedOpportunityGraphic.GetOrCreate(root.GetComponent<Image>());
            var populate = typeof(TimedOpportunityGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null);
            using var vertices = new VertexHelper();
            effect.rectTransform.sizeDelta = new Vector2(100f, 100f);
            effect.SetPresentation(true, false, true);
            populate.Invoke(effect, new object[] { vertices });
            int animatedVertices = vertices.currentVertCount;
            Assert.That(animatedVertices, Is.GreaterThan(0));
            var phase = typeof(TimedOpportunityGraphic).GetField("_phase", BindingFlags.Instance | BindingFlags.NonPublic);
            phase.SetValue(effect, 0.4f);
            typeof(TimedOpportunityGraphic).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effect, null);
            Assert.That(phase.GetValue(effect), Is.EqualTo(0.4f));
            effect.SetPresentation(true, true, false);
            populate.Invoke(effect, new object[] { vertices });
            Assert.That(vertices.currentVertCount, Is.GreaterThan(0).And.LessThan(animatedVertices));
            effect.Clear();
            populate.Invoke(effect, new object[] { vertices });
            Assert.That(vertices.currentVertCount, Is.Zero);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(0f)]
    [TestCase(0.35f)]
    [TestCase(0.8f)]
    public void EnlargedOpportunityGlowKeepsItsGeometryOutOfTheIconCenter(float phase)
    {
        var root = new GameObject("Opportunity edge spill test", typeof(RectTransform), typeof(Image));
        try
        {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 80f);
            var effect = TimedOpportunityGraphic.GetOrCreate(root.GetComponent<Image>());
            effect.SetPresentation(true, false, true);
            typeof(TimedOpportunityGraphic).GetField("_phase", PrivateInstance).SetValue(effect, phase);
            var populate = typeof(TimedOpportunityGraphic).GetMethod("OnPopulateMesh", PrivateInstance,
                null, new[] { typeof(VertexHelper) }, null);
            using var vertices = new VertexHelper();
            populate.Invoke(effect, new object[] { vertices });
            var vertex = UIVertex.simpleVert;
            Vector2 center = effect.rectTransform.rect.center;
            float halfWidth = effect.rectTransform.rect.width * 0.5f;
            float halfHeight = effect.rectTransform.rect.height * 0.5f;
            float spill = effect.rectTransform.rect.height * 0.025f;
            float farthestX = 0f;
            bool softEdgeOverlap = false;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                Vector2 point = (Vector2)vertex.position - center;
                farthestX = Mathf.Max(farthestX, Mathf.Abs(point.x));
                Assert.That(Mathf.Abs(point.x), Is.GreaterThanOrEqualTo(halfWidth - spill - 0.001f));
                Assert.That(Mathf.Abs(point.y), Is.GreaterThanOrEqualTo(halfHeight - spill - 0.001f));
                softEdgeOverlap |= Mathf.Abs(point.x) < halfWidth && Mathf.Abs(point.y) < halfHeight;
            }
            Assert.That(softEdgeOverlap, Is.True, "A narrow feather may overlap the art edge.");
            Assert.That(farthestX, Is.GreaterThan(halfWidth * 1.5f), "Impact must extend visibly beyond the icon.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private GameObject Child(string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(host.transform);
        return child;
    }

    private static bool IsRushing(object target, string field)
    {
        object rush = Get(target, field);
        return (bool)rush.GetType().GetField("isRushing").GetValue(rush);
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, PrivateInstance).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    private static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, args);
}
#endif
