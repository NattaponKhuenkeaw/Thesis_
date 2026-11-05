#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class Enemy102ColliderTests
{
    [TestCase(0, 1f)] [TestCase(1, 1f)] [TestCase(2, 1f)]
    [TestCase(0, -1f)] [TestCase(1, -1f)] [TestCase(2, -1f)]
    public void EveryAnimatedOrbStaysInsideItsBodyColliderAndCenteredOnTheMuzzle(int color, float facing)
    {
        var root = PrefabUtility.LoadPrefabContents(EnemyColorVariantAuthoring.PrefabPath(102));
        var textures = new Dictionary<string, Texture2D>();
        var report = new List<string>();
        var failures = new List<string>();
        try
        {
            root.GetComponent<EnemyColorVariants>().ApplyVariant(color);
            Vector3 scale = root.transform.localScale;
            root.transform.localScale = new Vector3(Mathf.Abs(scale.x) * facing, scale.y, scale.z);
            var renderer = root.GetComponent<SpriteRenderer>();
            var enemy = root.GetComponent<EnemyController>();
            var collider = enemy.bodyCollider;
            var animator = root.GetComponent<Animator>();
            var sprites = animator.runtimeAnimatorController.animationClips.SelectMany(clip => AnimationUtility.GetObjectReferenceCurveBindings(clip)
                .Where(b => b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite")
                .SelectMany(b => AnimationUtility.GetObjectReferenceCurve(clip, b)).Select(k => k.value).OfType<Sprite>()).Distinct().ToArray();
            Assert.That(sprites.Length, Is.GreaterThan(10));
            foreach (var sprite in sprites)
            {
                string path = AssetDatabase.GetAssetPath(sprite);
                if (!textures.TryGetValue(path, out var texture)) textures[path] = texture = EnemyColorVariantAuthoring.ReadTexture(path);
                float hue = path.EndsWith("a.png") ? 5f / 6f : path.EndsWith("b.png") ? .5f : 1f / 6f;
                Vector2 center = EnemyColorVariantAuthoring.OrbCenter(texture, sprite.rect, hue);
                Vector2 local = (center - sprite.pivot) / sprite.pixelsPerUnit;
                renderer.sprite = sprite;
                foreach (float angle in new[] { 0f, 90f, 180f })
                {
                    root.transform.rotation = Quaternion.Euler(0, 0, angle);
                    Physics2D.SyncTransforms();
                    Vector3 world = renderer.transform.TransformPoint(local);
                    Vector2 relative = collider.transform.InverseTransformPoint(world);
                    float distance = Vector2.Distance(relative, collider.offset);
                    Vector2 muzzle = renderer.transform.InverseTransformPoint(enemy.GetRangedMuzzleWorldPosition());
                    float muzzleError = Vector2.Distance(local, muzzle);
                    report.Add($"{sprite.name} angle={angle} orb={local} colliderOffset={collider.offset} centerError={distance:F4} muzzleError={muzzleError:F4}");
                    if (!collider.OverlapPoint(world) || distance > .2f)
                        failures.Add($"{sprite.name} angle={angle}: orb={local}, colliderOffset={collider.offset}, centerError={distance:F4}");
                    if (muzzleError > .025f)
                        failures.Add($"{sprite.name} angle={angle}: muzzle={muzzle}, orb={local}, muzzleError={muzzleError:F4}");
                }
            }
            Directory.CreateDirectory("Logs/Enemy102Collider");
            File.WriteAllLines($"Logs/Enemy102Collider/Color{color}_Facing{facing}.txt", report);
            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(8)));
        }
        finally
        {
            foreach (var texture in textures.Values) Object.DestroyImmediate(texture);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
