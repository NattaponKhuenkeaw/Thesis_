#if UNITY_INCLUDE_TESTS
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class Enm103AssetTests
{
    private const string Root = "Assets/Project/Character/enemy/enemy/Enm103/";

    [Test]
    public void ShieldEnemyPreservesTheAuthoredSizeTuning()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Enemy103.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.transform.localScale.x, Is.EqualTo(.80325f).Within(.00001f));
        Assert.That(prefab.transform.localScale.y, Is.EqualTo(.80325f).Within(.00001f));
        Assert.That(prefab.transform.localScale.z, Is.EqualTo(.80325f).Within(.00001f));
        var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "sprite/Enm103.png");
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(20f));
        var body = (BoxCollider2D)prefab.GetComponent<EnemyController>().bodyCollider;
        Assert.That(body.size.x, Is.EqualTo(1.35f).Within(.0001f));
        Assert.That(body.size.y, Is.EqualTo(2.3f).Within(.0001f));
        Assert.That(body.offset, Is.EqualTo(new Vector2(0f, 1.15f)));
    }

    [TestCase(.80325f, 0f)]
    [TestCase(.80325f, 180f)]
    [TestCase(1.5f, 0f)]
    [TestCase(1.5f, 180f)]
    public void CenteredGrabPlacesHurtSpriteRedCoreAtTrapAnchor(float scale, float yaw)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Enemy103.prefab");
        var instance = Object.Instantiate(prefab);
        var socket = new GameObject("Enm103GrabAnchor");
        var texture = new Texture2D(2, 2);
        try
        {
            instance.SetActive(false);
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            socket.transform.position = new Vector3(20f, 4f, 0f);
            socket.transform.rotation = instance.transform.rotation;
            socket.transform.localScale = new Vector3(1.2f, .8f, 1f);
            var hurt = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "animation/Enm103_Hurt.anim");
            var sprite = (Sprite)AnimationUtility.GetObjectReferenceCurve(hurt,
                AnimationUtility.GetObjectReferenceCurveBindings(hurt)[0])[0].value;
            Assert.That(texture.LoadImage(File.ReadAllBytes(Root + "sprite/Enm103.png")), Is.True);

            // Locate the red core, not the shield or transparent frame bounds.
            Vector2 corePixels = Vector2.zero;
            int count = 0;
            Rect rect = sprite.rect;
            for (int y = 0; y < (int)rect.height; y++)
                for (int x = 0; x < (int)rect.width; x++)
                {
                    Color32 pixel = texture.GetPixel((int)rect.x + x, (int)rect.y + y);
                    if (pixel.a <= 128 || pixel.r <= 160 || pixel.g >= 80 || pixel.b <= 40 || pixel.b >= 220)
                        continue;
                    corePixels += new Vector2(x + .5f, y + .5f);
                    count++;
                }
            Assert.That(count, Is.GreaterThan(0));
            Vector2 coreLocal = (corePixels / count - sprite.pivot) / sprite.pixelsPerUnit;
            var handler = instance.GetComponent<EnemyHitReactionHandler>();
            typeof(EnemyHitReactionHandler).GetMethod("Awake", flags).Invoke(handler, null);
            var hit = new HitReactionData
            {
                reactionKind = EnemyReactionKind.Grab,
                alignGrabToBodyCenter = true,
                grabSocket = socket.transform,
                grabOffset = new Vector2(.06f, 2.61f)
            };
            typeof(EnemyHitReactionHandler).GetField("currentReaction", flags).SetValue(handler, hit);
            Vector2 rootPosition = (Vector2)typeof(EnemyHitReactionHandler)
                .GetMethod("ResolveGrabbedPosition", flags).Invoke(handler, new object[] { socket.transform });
            Vector2 coreOffset = instance.GetComponent<SpriteRenderer>().transform.TransformVector(coreLocal);
            Vector2 trapAnchor = socket.transform.TransformPoint(hit.grabOffset);
            Assert.That(Vector2.Distance(rootPosition + coreOffset, trapAnchor), Is.LessThan(.001f));
            var body = (BoxCollider2D)instance.GetComponent<EnemyController>().bodyCollider;
            Assert.That(body.offset.y - body.size.y * .5f, Is.EqualTo(0f).Within(.0001f));
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(socket);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void PrefabUsesShieldGuardKitAndOverridesEveryBaseClip()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Enemy103.prefab");
        Assert.That(prefab, Is.Not.Null);
        var enemy = prefab.GetComponent<EnemyController>();
        Assert.That(enemy, Is.Not.Null);
        Assert.That(enemy.Kit, Is.EqualTo(AssetDatabase.LoadAssetAtPath<EnemyKit>(
            "Assets/Project/Resources/EnemySo/EK_ShieldGuard.asset")));
        Assert.That(enemy.CombatRole, Is.EqualTo(EnemyCombatRole.ShieldGuard));
        Assert.That(prefab.GetComponent<EnemyShieldGuard>(), Is.Not.Null);
        var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorOverrideController;
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller["Enemy2_Crouch"].name, Is.EqualTo("Gif_Enemy2_Crouch_ColorA"));
        Assert.That(controller["Enemy2_Kick"].name, Is.EqualTo("Gif_Enemy2_Kick_ColorA"));
        foreach (var clip in controller.animationClips)
        {
            Assert.That(clip.name, Does.StartWith("Gif_Enemy"));
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    Assert.That(key.value, Is.InstanceOf<Sprite>(), clip.name);
        }
        var body = enemy.bodyCollider as BoxCollider2D;
        Assert.That(body, Is.Not.Null);
        Assert.That(body.offset.y - body.size.y / 2f, Is.EqualTo(0f).Within(.0001f));
    }

    [Test]
    public void WindupUsesShieldThenAnticipationAndRushHasOnlyMovingPose()
    {
        var windup = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "animation/Enm103_Windup.anim");
        var keys = AnimationUtility.GetObjectReferenceCurve(windup,
            AnimationUtility.GetObjectReferenceCurveBindings(windup)[0]);
        Assert.That(keys.Length, Is.EqualTo(2));
        Assert.That(keys[0].value.name, Is.EqualTo("Enm103_Shield"));
        Assert.That(keys[1].value.name, Is.EqualTo("Enm103_Anticipation"));
        Assert.That(keys[1].time, Is.LessThan(.8f));
        var rush = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "animation/Enm103_Rush.anim");
        var movingKeys = AnimationUtility.GetObjectReferenceCurve(rush,
            AnimationUtility.GetObjectReferenceCurveBindings(rush)[0]);
        Assert.That(movingKeys.Length, Is.EqualTo(1));
        Assert.That(movingKeys[0].value.name, Is.EqualTo("Enm103_Rush"));
    }
}
#endif
