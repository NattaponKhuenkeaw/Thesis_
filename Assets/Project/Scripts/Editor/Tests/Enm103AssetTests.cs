#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class Enm103AssetTests
{
    private const string Root = "Assets/Project/Character/enemy/enemy/Enm103/";

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
        Assert.That(controller["Enemy2_Crouch"].name, Is.EqualTo("Enm103_Windup"));
        Assert.That(controller["Enemy2_Kick"].name, Is.EqualTo("Enm103_Rush"));
        foreach (var clip in controller.animationClips)
        {
            Assert.That(clip.name, Does.StartWith("Enm103_"));
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
