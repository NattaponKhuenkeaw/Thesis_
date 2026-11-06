#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Project.AIBehaviour.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class Boss112ColorVariantTests
{
    [UnityTearDown]
    public IEnumerator ExitRuntimeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    private static string Sheet(int color) => Boss112PrototypeAuthoring.Folder + $"/Enm112{(char)('a' + color)}.png";

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void EveryColorKeepsTheOriginalSpriteRectsPivotsAndPixelScale(int color)
    {
        var original = AssetDatabase.LoadAllAssetsAtPath(Sheet(0)).OfType<Sprite>().ToDictionary(sprite => sprite.name);
        var colored = AssetDatabase.LoadAllAssetsAtPath(Sheet(color)).OfType<Sprite>().ToDictionary(sprite => sprite.name);
        Assert.That(colored.Count, Is.EqualTo(22));
        foreach (var entry in original)
        {
            var sprite = colored[entry.Key];
            Assert.That(sprite.rect, Is.EqualTo(entry.Value.rect), entry.Key);
            Assert.That(sprite.pivot, Is.EqualTo(entry.Value.pivot), entry.Key);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(entry.Value.pixelsPerUnit));
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(sprite.texture.width, Is.EqualTo(entry.Value.texture.width));
            Assert.That(sprite.texture.height, Is.EqualTo(entry.Value.texture.height));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TestAndRunPrefabsRandomizeThreeColorsWithoutChangingCombatOrPhysics(bool run)
    {
        string path = run ? Boss112PrototypeAuthoring.Folder + "/Boss112_Run.prefab" : Boss112PrototypeAuthoring.BossPath;
        var root = PrefabUtility.LoadPrefabContents(path);
        var random = Random.state;
        try
        {
            var appearance = root.GetComponent<EnemyColorVariants>();
            Assert.That(appearance, Is.Not.Null);
            Assert.That(appearance.VariantCount, Is.EqualTo(3));
            var enemy = root.GetComponent<EnemyController>();
            string before = EditorJsonUtility.ToJson(enemy);
            string bossBefore = EditorJsonUtility.ToJson(root.GetComponent<Boss112Controller>());
            string physicsBefore = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            var counts = new int[3];
            Random.InitState(112376);
            for (int i = 0; i < 512; i++)
            {
                appearance.RandomizeAppearance();
                counts[appearance.SelectedIndex]++;
            }
            foreach (int count in counts) Assert.That(count, Is.InRange(110, 230));
            Assert.That(EditorJsonUtility.ToJson(enemy), Is.EqualTo(before));
            Assert.That(EditorJsonUtility.ToJson(root.GetComponent<Boss112Controller>()), Is.EqualTo(bossBefore));
            Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root), Is.EqualTo(physicsBefore));
        }
        finally { Random.state = random; PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void IdleSlamAndAllBeamFramesStayOnTheSelectedSheet(int color)
    {
        var root = PrefabUtility.LoadPrefabContents(Boss112PrototypeAuthoring.BossPath);
        try
        {
            var appearance = root.GetComponent<EnemyColorVariants>();
            Assert.That(appearance.ApplyVariant(color), Is.True);
            var visual = root.transform.Find("Visual");
            var animator = visual.GetComponent<Animator>();
            var renderer = visual.GetComponent<SpriteRenderer>();
            animator.Rebind(); animator.Update(0f);
            foreach (string state in new[] { "Idle", "Slam" })
            {
                for (int frame = 0; frame < 9; frame++)
                {
                    animator.Play("Base Layer." + state, 0, frame / 9f);
                    animator.Update(0f);
                    Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Is.EqualTo(Sheet(color)), state);
                }
            }
            var beam = root.GetComponent<Boss112Controller>().Beam;
            var beamRenderer = beam.GetComponentInChildren<SpriteRenderer>(true);
            for (int frame = 0; frame < 4; frame++)
            {
                beam.Show(new Vector2(2f, 9f), 0f, false, false, frame / 8f);
                Assert.That(beamRenderer.sprite.name, Is.EqualTo($"BeamWarning_{frame:00}"));
                Assert.That(AssetDatabase.GetAssetPath(beamRenderer.sprite), Is.EqualTo(Sheet(color)));
                Assert.That(beam.IsDamaging, Is.False);
            }
            beam.Show(new Vector2(2f, 9f), 0f, true, true, 0f);
            Assert.That(beamRenderer.sprite.name, Is.EqualTo("BeamActive"));
            Assert.That(AssetDatabase.GetAssetPath(beamRenderer.sprite), Is.EqualTo(Sheet(color)));
            Assert.That(beam.IsDamaging, Is.True);
            Assert.That(beam.DamageCollider.size, Is.EqualTo(new Vector2(2.8f, 9f)));
            beam.Hide();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(1)]
    [TestCase(2)]
    public void ColoredClipsKeepTimingEventsAndEveryNonSpriteCurve(int color)
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Boss112PrototypeAuthoring.Folder +
            $"/Boss112_Color{(char)('A' + color)}.overrideController");
        Assert.That(controller, Is.Not.Null);
        foreach (var pair in EnemyColorVariantAuthoring.ClipOverrides(controller))
        {
            Assert.That(pair.Value.frameRate, Is.EqualTo(pair.Key.frameRate));
            Assert.That(pair.Value.length, Is.EqualTo(pair.Key.length));
            Assert.That(pair.Value.isLooping, Is.EqualTo(pair.Key.isLooping));
            Assert.That(AnimationUtility.GetAnimationEvents(pair.Value).Length,
                Is.EqualTo(AnimationUtility.GetAnimationEvents(pair.Key).Length));
            foreach (var binding in AnimationUtility.GetCurveBindings(pair.Key))
                Assert.That(AnimationUtility.GetEditorCurve(pair.Value, binding).keys,
                    Is.EqualTo(AnimationUtility.GetEditorCurve(pair.Key, binding).keys));
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(pair.Key))
            {
                var original = AnimationUtility.GetObjectReferenceCurve(pair.Key, binding);
                var cloned = AnimationUtility.GetObjectReferenceCurve(pair.Value, binding);
                Assert.That(cloned.Select(key => key.time), Is.EqualTo(original.Select(key => key.time)));
                Assert.That(cloned.Select(key => key.value.name), Is.EqualTo(original.Select(key => key.value.name)));
            }
        }
    }

    [UnityTest]
    public IEnumerator SpawnRollHappensOnceAndReenablingCannotChangeTheColor()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var parent = new GameObject("112 spawn color fixture"); parent.SetActive(false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Boss112PrototypeAuthoring.BossPath);
        var root = Object.Instantiate(prefab, parent.transform);
        using (var data = new SerializedObject(root.GetComponent<Boss112Controller>()))
        {
            data.FindProperty("bindHealthHud").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        parent.SetActive(true);
        yield return null;
        var appearance = root.GetComponent<EnemyColorVariants>();
        int selected = appearance.SelectedIndex;
        Assert.That(selected, Is.InRange(0, 2));
        Assert.That(AssetDatabase.GetAssetPath(root.transform.Find("Visual").GetComponent<SpriteRenderer>().sprite),
            Is.EqualTo(Sheet(selected)));
        root.SetActive(false); root.SetActive(true);
        yield return null;
        Assert.That(appearance.SelectedIndex, Is.EqualTo(selected));
        Assert.That(AssetDatabase.GetAssetPath(root.transform.Find("Visual").GetComponent<SpriteRenderer>().sprite),
            Is.EqualTo(Sheet(selected)));
        Object.Destroy(parent);
        yield return null;
        yield return new ExitPlayMode();
    }
}
#endif
