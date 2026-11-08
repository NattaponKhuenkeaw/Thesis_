#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class EnemyColorVariantTests
{
    [TestCase('a', "SpawnGround")] [TestCase('b', "SpawnGround")] [TestCase('c', "SpawnGround")]
    [TestCase('a', "SpawnFall")] [TestCase('b', "SpawnFall")] [TestCase('c', "SpawnFall")]
    public void BossSpawnClipsExplicitlyHoldTheirSelectedColor(char color, string state)
    {
        string folder = "Assets/Project/Character/enemy/Boss/Executioner/animation/ColorVariants/";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{folder}boss_{state}_Color{char.ToUpperInvariant(color)}.anim");
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .Where(b => b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite" && b.path == "").ToArray();
        Assert.That(bindings.Length, Is.EqualTo(1), "An empty spawn clip can restore the prefab's default color.");
        var frames = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
        Assert.That(frames.Length, Is.EqualTo(2));
        Assert.That(frames[0].time, Is.Zero);
        Assert.That(frames[1].time, Is.EqualTo(1f).Within(.00001f));
        foreach (var frame in frames)
        {
            Assert.That(AssetDatabase.GetAssetPath(frame.value), Is.EqualTo(EnemyColorVariantAuthoring.VariantSheetPath(111, color)));
            Assert.That(frame.value.name, Is.EqualTo("Gif_Idle_00"));
        }
        Assert.That(clip.isLooping, Is.False);
    }

    [TestCase('a', "SpawnGround")] [TestCase('b', "SpawnGround")] [TestCase('c', "SpawnGround")]
    [TestCase('a', "SpawnFall")] [TestCase('b', "SpawnFall")] [TestCase('c', "SpawnFall")]
    public void BossAnimatorKeepsTheSameColorFromRevealThroughIdleAndWalk(char color, string spawnState)
    {
        const string folder = "Assets/Project/Character/enemy/Boss/Executioner/animation/ColorVariants/";
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            $"{folder}ExecutionerAnimatorController_Color{char.ToUpperInvariant(color)}.overrideController");
        var host = new GameObject("Boss reveal color fixture");
        host.SetActive(false);
        try
        {
            var renderer = host.AddComponent<SpriteRenderer>();
            char otherColor = color == 'a' ? 'b' : 'a';
            renderer.sprite = AssetDatabase.LoadAllAssetsAtPath(EnemyColorVariantAuthoring.VariantSheetPath(111, otherColor))
                .OfType<Sprite>().Single(s => s.name == "Gif_Idle_00");
            var animator = host.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            host.SetActive(true);
            animator.Rebind();
            animator.Update(0f);
            foreach (string state in new[] { spawnState, "Idle", "Walk" })
            {
                animator.SetInteger("LifecyclePhase", state.StartsWith("Spawn") ? 0 : 1);
                animator.Play("Base Layer." + state, 0, 0f);
                animator.Update(0f);
                for (int frame = 0; frame < 14; frame++)
                {
                    Assert.That(AssetDatabase.GetAssetPath(renderer.sprite),
                        Is.EqualTo(EnemyColorVariantAuthoring.VariantSheetPath(111, color)), $"{state}, frame {frame}");
                    Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
                    animator.Update(.1f);
                }
            }
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase('a', "Idle")] [TestCase('b', "Idle")] [TestCase('c', "Idle")]
    [TestCase('a', "Walk")] [TestCase('b', "Walk")] [TestCase('c', "Walk")]
    public void Enemy101GroundColliderMatchesVisibleSpriteBase(char color, string pose)
    {
        var root = PrefabUtility.LoadPrefabContents(EnemyColorVariantAuthoring.PrefabPath(101));
        var texture = EnemyColorVariantAuthoring.ReadTexture(EnemyColorVariantAuthoring.VariantSheetPath(101, color));
        try
        {
            var body = root.GetComponent<EnemyController>().bodyCollider as BoxCollider2D;
            Assert.That(body, Is.Not.Null);
            Assert.That(body.transform, Is.SameAs(root.transform));
            Assert.That(body.size, Is.EqualTo(new Vector2(2.2f, 1.81f)));
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(root.GetComponent<EnemyHitReactionHandler>().UsesPositionReactions, Is.True);
            float colliderBottom = body.offset.y - body.size.y * .5f;
            var pixels = texture.GetPixels32();
            var frames = AssetDatabase.LoadAllAssetsAtPath(EnemyColorVariantAuthoring.VariantSheetPath(101, color))
                .OfType<Sprite>().Where(sprite => sprite.name.StartsWith("Gif_" + pose + "_")).ToArray();
            Assert.That(frames.Length, Is.EqualTo(6));
            foreach (var sprite in frames)
            {
                int bottom = -1;
                var rect = new RectInt(Mathf.RoundToInt(sprite.rect.x), Mathf.RoundToInt(sprite.rect.y),
                    Mathf.RoundToInt(sprite.rect.width), Mathf.RoundToInt(sprite.rect.height));
                for (int y = rect.yMin; y < rect.yMax && bottom < 0; y++)
                    for (int x = rect.xMin; x < rect.xMax; x++)
                        if (pixels[y * texture.width + x].a > 16)
                        {
                            bottom = y - rect.yMin;
                            break;
                        }
                Assert.That(bottom, Is.GreaterThanOrEqualTo(0), sprite.name);
                float visibleBottom = (bottom - sprite.pivot.y) / sprite.pixelsPerUnit;
                Assert.That(colliderBottom, Is.EqualTo(visibleBottom).Within(.001f), sprite.name);
            }
        }
        finally
        {
            Object.DestroyImmediate(texture);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [TestCase(101)] [TestCase(102)] [TestCase(103)] [TestCase(111)]
    public void ThreeNewAppearancesRollIndependentlyWithoutChangingEnemyData(int id)
    {
        var root = PrefabUtility.LoadPrefabContents(EnemyColorVariantAuthoring.PrefabPath(id));
        var random = Random.state;
        try
        {
            var appearance = root.GetComponent<EnemyColorVariants>();
            Assert.That(appearance, Is.Not.Null);
            Assert.That(appearance.VariantCount, Is.EqualTo(3));
            var enemy = root.GetComponent<EnemyController>();
            string data = EditorJsonUtility.ToJson(enemy);
            string collision = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            Random.InitState(731);
            var counts = new int[3];
            for (int i = 0; i < 512; i++)
            {
                appearance.RandomizeAppearance();
                counts[appearance.SelectedIndex]++;
            }
            foreach (int count in counts) Assert.That(count, Is.InRange(110, 230));
            int selected = appearance.SelectedIndex;
            root.SetActive(false); root.SetActive(true);
            Assert.That(appearance.SelectedIndex, Is.EqualTo(selected), "A temporary disable must not recolor a living enemy.");
            Assert.That(EditorJsonUtility.ToJson(enemy), Is.EqualTo(data));
            Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root), Is.EqualTo(collision));
        }
        finally { Random.state = random; PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(101)] [TestCase(102)] [TestCase(103)] [TestCase(111)]
    public void EveryColorKeepsAllAnimationTimingEventsAndNonSpriteCurves(int id)
    {
        var root = PrefabUtility.LoadPrefabContents(EnemyColorVariantAuthoring.PrefabPath(id));
        try
        {
            var appearance = root.GetComponent<EnemyColorVariants>();
            var animator = root.GetComponentInChildren<AnimationHandler_Enemy>().GetComponent<Animator>();
            appearance.ApplyVariant(0);
            var originalController = animator.runtimeAnimatorController;
            var original = EnemyColorVariantAuthoring.ClipOverrides(originalController);
            for (int color = 1; color < 3; color++)
            {
                Assert.That(appearance.ApplyVariant(color), Is.True);
                var colored = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                var controller = (AnimatorOverrideController)animator.runtimeAnimatorController;
                controller.GetOverrides(colored);
                Assert.That(controller.runtimeAnimatorController, Is.EqualTo(originalController is AnimatorOverrideController inherited ? inherited.runtimeAnimatorController : originalController));
                Assert.That(colored.Select(p => p.Key), Is.EquivalentTo(original.Select(p => p.Key)));
                foreach (var pair in original)
                {
                    var source = pair.Value != null ? pair.Value : pair.Key;
                    var target = colored.Single(p => p.Key == pair.Key).Value;
                    Assert.That(target.frameRate, Is.EqualTo(source.frameRate));
                    Assert.That(target.length, Is.EqualTo(source.length).Within(.00001f));
                    Assert.That(target.isLooping, Is.EqualTo(source.isLooping));
                    Assert.That(AnimationUtility.GetAnimationEvents(target).Select(e => e.functionName + ":" + e.time),
                        Is.EqualTo(AnimationUtility.GetAnimationEvents(source).Select(e => e.functionName + ":" + e.time)));
                    foreach (var binding in AnimationUtility.GetCurveBindings(source))
                        Assert.That(AnimationUtility.GetEditorCurve(target, binding).keys,
                            Is.EqualTo(AnimationUtility.GetEditorCurve(source, binding).keys));
                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
                    {
                        var oldFrames = AnimationUtility.GetObjectReferenceCurve(source, binding);
                        var frames = AnimationUtility.GetObjectReferenceCurve(target, binding);
                        Assert.That(frames.Select(f => f.time), Is.EqualTo(oldFrames.Select(f => f.time)));
                        for (int i = 0; i < frames.Length; i++)
                            if (oldFrames[i].value is Sprite sprite && AssetDatabase.GetAssetPath(sprite).Contains("ColorVariants/"))
                            {
                                Assert.That(frames[i].value, Is.TypeOf<Sprite>());
                                Assert.That(frames[i].value.name, Is.EqualTo(sprite.name));
                                Assert.That(AssetDatabase.GetAssetPath(frames[i].value), Does.Contain("ColorVariants/"));
                            }
                            else Assert.That(frames[i].value, Is.EqualTo(oldFrames[i].value));
                    }
                }
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(101)] [TestCase(102)] [TestCase(103)] [TestCase(111)]
    public void SlicedColorsRetainCellSizePixelDensityAndVisualAnchors(int id)
    {
        var oldTexture = EnemyColorVariantAuthoring.ReadTexture(EnemyColorVariantAuthoring.SheetPath(id));
        try
        {
            var originals = AssetDatabase.LoadAllAssetsAtPath(EnemyColorVariantAuthoring.SheetPath(id)).OfType<Sprite>().ToArray();
            foreach (char suffix in new[] { 'a', 'b', 'c' })
            {
                string path = EnemyColorVariantAuthoring.VariantSheetPath(id, suffix);
                var texture = EnemyColorVariantAuthoring.ReadTexture(path);
                try
                {
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
                    Assert.That(sprites.Count, Is.GreaterThan(originals.Length));
                    Assert.That(((TextureImporter)AssetImporter.GetAtPath(path)).filterMode, Is.EqualTo(FilterMode.Point));
                    var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    Assert.That(imported.width, Is.EqualTo(texture.width), "The wider boss sheet must not be downscaled on import.");
                    Assert.That(imported.height, Is.EqualTo(texture.height));
                    foreach (var original in originals)
                    {
                        var sprite = sprites[original.name];
                        Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(id == 111 ? 160 : 128, id == 111 ? 160 : 128)));
                        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(original.pixelsPerUnit));
                        var oldMask = EnemyColorVariantAuthoring.ReadMask(oldTexture, original.rect);
                        var mask = EnemyColorVariantAuthoring.ReadMask(texture, sprite.rect);
                        Assert.That(mask.count, Is.GreaterThan(0));
                        Vector2 oldCenter = (oldMask.center - original.pivot) / original.pixelsPerUnit;
                        Vector2 center = (mask.center - sprite.pivot) / sprite.pixelsPerUnit;
                        Assert.That(Vector2.Distance(center, oldCenter), Is.LessThan(.0001f), original.name);
                    }
                }
                finally { Object.DestroyImmediate(texture); }
            }
        }
        finally { Object.DestroyImmediate(oldTexture); }
    }

    [Test]
    public void BossChargeKeepsAnticipationRushAndOrderedBrakingPoses()
    {
        foreach (char suffix in new[] { 'a', 'b', 'c' })
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(EnemyColorVariantAuthoring.VariantSheetPath(111, suffix)).OfType<Sprite>().ToDictionary(s => s.name);
            Assert.That(sprites["Boss_Final_256_9"].rect.x, Is.EqualTo(0));
            Assert.That(sprites["Boss_Final_256_10"].rect.x, Is.EqualTo(5 * 160));
            Assert.That(sprites["Boss_Rush2"].rect.x, Is.EqualTo(6 * 160));
            Assert.That(sprites["Boss_Rush3"].rect.x, Is.EqualTo(7 * 160));
            Assert.That(sprites["Boss_Rush4"].rect.x, Is.EqualTo(9 * 160));
            Assert.That(sprites["Boss_Hurt"].rect.x, Is.EqualTo(12 * 160));
        }
    }

    [Test]
    public void RangedShotsKeepTheShooterColorThroughAllFramesWithoutMutatingTheKit()
    {
        var root = PrefabUtility.LoadPrefabContents(EnemyColorVariantAuthoring.PrefabPath(102));
        try
        {
            var appearance = root.GetComponent<EnemyColorVariants>();
            var kit = root.GetComponent<EnemyController>().Kit;
            Sprite[] originals = kit.ShotAnimation.ToArray();
            string data = EditorJsonUtility.ToJson(kit);
            for (int color = 0; color < 3; color++)
            {
                appearance.ApplyVariant(color);
                var frames = appearance.ResolveProjectileAnimation(kit.ShotAnimation);
                Assert.That(frames.Length, Is.EqualTo(originals.Length));
                for (int i = 0; i < frames.Length; i++)
                {
                    Assert.That(frames[i], Is.EqualTo(appearance.ResolveProjectileSprite(originals[i])));
                    Assert.That(AssetDatabase.GetAssetPath(frames[i]), Does.Contain("ColorVariants/"));
                }
                var first = frames[0];
                appearance.ApplyVariant((color + 1) % 3);
                Assert.That(frames[0], Is.EqualTo(first), "An existing projectile keeps its original shooter's color.");
            }
            Assert.That(EditorJsonUtility.ToJson(kit), Is.EqualTo(data));
            Assert.That(kit.ShotAnimation, Is.EqualTo(originals));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void DummyAndRoomContainerDoNotReceiveRandomColorComponents()
    {
        foreach (string path in new[] {
            EnemyColorVariantAuthoring.EnemyFolder + "EnemyCore/Enemy_Dummy.prefab",
            "Assets/Project/Resources/RoomLayouts/RoomLayout_Boss_Executioner.prefab" })
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<EnemyColorVariants>(true), Is.Empty);
    }
}
#endif
