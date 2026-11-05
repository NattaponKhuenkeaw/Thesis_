#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using Unity.Behavior;
using UnityEngine;
using Object = UnityEngine.Object;

public class EnemyGifAnimationTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(101)] [TestCase(102)] [TestCase(103)] [TestCase(111)]
    public void NewSheetsIncludeEveryReferencedGifFrameWithoutChangingOriginalSheet(int id)
    {
        Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(EnemyColorVariantAuthoring.SheetPath(id)), Is.Not.Null);
        foreach (char color in new[] { 'a', 'b', 'c' })
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(EnemyColorVariantAuthoring.VariantSheetPath(id, color)).OfType<Sprite>().ToDictionary(s => s.name);
            var texture = EnemyColorVariantAuthoring.ReadTexture(EnemyColorVariantAuthoring.VariantSheetPath(id, color));
            try
            {
                int size = id == 111 ? 160 : 128;
                foreach (var pose in EnemyGifAnimationAuthoring.Poses.Where(p => p.enemy == id))
                    for (int frame = 0; frame < pose.seconds.Length; frame++)
                    {
                        var sprite = sprites[pose.SpriteName(frame)];
                        Assert.That(sprite.rect, Is.EqualTo(new Rect(frame * size, texture.height - (pose.row + 1) * size, size, size)));
                        Assert.That(EnemyColorVariantAuthoring.ReadMask(texture, sprite.rect).count, Is.GreaterThan(0), sprite.name);
                        if (id == 103 && pose.name == "Hurt") Assert.That(sprite.pivot,Is.EqualTo(sprites["Enm103_Hurt"].pivot));
                        else if (id != 102) Assert.That(sprite.pivot, Is.EqualTo(sprites["Gif_Idle_00"].pivot), "Keep sheet-relative movement, not a separately re-centered pose on each frame.");
                        else if (pose.name == "Projectile")
                            Assert.That(sprite.pivot,Is.EqualTo(sprites["Enm102_Shot"+frame].pivot),"Keep bullet artwork centered on its existing hitbox.");
                    }
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }

    [TestCase(101)] [TestCase(102)] [TestCase(103)] [TestCase(111)]
    public void IdleWalkFallAndRecoveryKeepTheGifFrameHolds(int id)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyColorVariantAuthoring.PrefabPath(id));
        var root = PrefabUtility.LoadPrefabContents(EnemyColorVariantAuthoring.PrefabPath(id));
        try
        {
            var variants = root.GetComponent<EnemyColorVariants>();
            var animator = root.GetComponentInChildren<AnimationHandler_Enemy>().GetComponent<Animator>();
            Assert.That(prefab.GetComponent<EnemyColorVariants>().VariantCount, Is.EqualTo(3));
            for (int color = 0; color < 3; color++)
            {
                variants.ApplyVariant(color);
                foreach (var pair in EnemyColorVariantAuthoring.ClipOverrides(animator.runtimeAnimatorController))
                {
                    var pose = EnemyGifAnimationAuthoring.Resolve(id, pair.Key.name);
                    if (pose == null || !(new[] { "Idle", "Walk", "Fall", "Recover" }).Contains(pose.name)
                        || pair.Key.name == "Enemy_Knockdown") continue;
                    var clip = pair.Value;
                    var frames = AnimationUtility.GetObjectReferenceCurve(clip, new EditorCurveBinding { type = typeof(SpriteRenderer), propertyName = "m_Sprite", path = "" });
                    Assert.That(frames.Length, Is.EqualTo(pose.seconds.Length), clip.name);
                    float elapsed = 0f;
                    for (int i = 0; i < frames.Length; i++)
                    {
                        Assert.That(frames[i].time, Is.EqualTo(elapsed).Within(.0001f), clip.name);
                        Assert.That(frames[i].value.name, Is.EqualTo(pose.SpriteName(i)));
                        elapsed += pose.seconds[i];
                    }
                    Assert.That(clip.length, Is.EqualTo(elapsed).Within(.0001f));
                    Assert.That(clip.isLooping, Is.EqualTo(pose.name == "Idle" || pose.name == "Walk"));
                }
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void BitePreparationAndImpactAreDifferentClipsAndFiringUsesTheStance()
    {
        AssertFrames(101,"Enemy2_Crouch","Bite",0,3);
        AssertFrames(101,"Enemy2_Kick","Bite",4,2);
        AssertFrames(102,"Enemy2_Crouch","Shoot",0,6);
        AssertFrames(102,"Enemy2_Kick","ShootStance",0,4);
        AssertFrames(103,"Enemy2_Crouch","Guard",0,4);
        AssertFrames(103,"Enemy2_Kick","Dash",0,7);
        var kit = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyColorVariantAuthoring.PrefabPath(102)).GetComponent<EnemyController>().Kit;
        Assert.That(kit.ShotAnimationFPS, Is.EqualTo(1f/.15f).Within(.0001f));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void BiteHoldsItsOpenMouthThroughoutTheWarningAndClosesOnlyOnStrike(int color)
    {
        var host = MakeHandler(101, out var handler, out var animator);
        var target = new GameObject("Bite target");
        EnemyAttackAction attack = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyColorVariantAuthoring.PrefabPath(101));
            using (var variants = new SerializedObject(prefab.GetComponent<EnemyColorVariants>()))
                animator.runtimeAnimatorController = (RuntimeAnimatorController)variants.FindProperty("variants")
                    .GetArrayElementAtIndex(color).FindPropertyRelative("controller").objectReferenceValue;
            animator.Rebind(); animator.Update(0f);
            var warningSprite = AssetDatabase.LoadAllAssetsAtPath(
                EnemyColorVariantAuthoring.VariantSheetPath(101, (char)('a' + color)))
                .OfType<Sprite>().Single(sprite => sprite.name == "Gif_Bite_02");
            handler.useMeleeAttackPoses = prefab.GetComponentInChildren<AnimationHandler_Enemy>().useMeleeAttackPoses;
            Assert.That(handler.useMeleeAttackPoses, Is.True);
            var enemy = host.GetComponent<EnemyController>();
            using (var data = new SerializedObject(enemy))
            {
                data.FindProperty("kit").objectReferenceValue = prefab.GetComponent<EnemyController>().Kit;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var hitbox = new GameObject("Bite hitbox");
            hitbox.transform.SetParent(host.transform);
            enemy.attackHitbox = hitbox.AddComponent<BoxCollider2D>();
            target.transform.position = Vector3.right;
            attack = new EnemyAttackAction {
                Agent = new BlackboardVariable<GameObject>(host),
                Target = new BlackboardVariable<GameObject>(target),
                CanAttack = new BlackboardVariable<bool>(true)
            };
            Assert.That(CallAttack(attack, "OnStart").ToString(), Is.EqualTo("Running"));
            animator.Update(0f);
            Assert.That(enemy.ResolveWarningDuration(.45f), Is.EqualTo(.9f).Within(.001f));
            animator.Update(.35f);
            Assert.That(host.GetComponent<SpriteRenderer>().sprite, Is.EqualTo(warningSprite),
                $"state={animator.GetCurrentAnimatorStateInfo(0).fullPathHash} progress={animator.GetCurrentAnimatorStateInfo(0).normalizedTime} speed={animator.speed}");
            typeof(EnemyAttackAction).GetField("m_Timer", Flags).SetValue(attack, .35f);
            CallAttack(attack, "OnUpdate");
            Assert.That(enemy.attackHitbox.enabled, Is.False);
            animator.Update(.54f);
            typeof(EnemyAttackAction).GetField("m_Timer", Flags).SetValue(attack, .7f);
            CallAttack(attack, "OnUpdate");
            Assert.That(host.GetComponent<SpriteRenderer>().sprite, Is.EqualTo(warningSprite));
            Assert.That(enemy.attackHitbox.enabled, Is.False);
            Assert.That(attack.CanAttack.Value, Is.False);
            Assert.That(animator.speed, Is.EqualTo(1f), "Holding a non-looping pose must not freeze Hurt or the strike.");

            typeof(EnemyAttackAction).GetField("m_Timer", Flags).SetValue(attack, .9f);
            CallAttack(attack, "OnUpdate"); animator.Update(0f);
            Assert.That(host.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Gif_Bite_04"));
            Assert.That(enemy.attackHitbox.enabled, Is.True);
            Assert.That(attack.CanAttack.Value, Is.False);
            animator.Update(.11f);
            Assert.That(host.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Gif_Bite_05"));
            float start = (float)typeof(EnemyAttackAction).GetField("m_AttackStartTime", Flags).GetValue(attack);
            float end = (float)typeof(EnemyAttackAction).GetField("m_PhaseEndTime", Flags).GetValue(attack);
            Assert.That(end - start, Is.EqualTo(.28f + 3.8f).Within(.001f));
            animator.Update(.15f);
            var strikePose = animator.GetCurrentAnimatorStateInfo(0);
            typeof(EnemyAttackAction).GetField("m_Timer", Flags).SetValue(attack, start + .29f);
            CallAttack(attack, "OnUpdate"); animator.Update(0f);
            Assert.That(enemy.attackHitbox.enabled, Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                Is.EqualTo(Animator.StringToHash("Base Layer.Idle")),
                $"Recovery locomotion must not stay stuck in the bite pose. progress={strikePose.normalizedTime} length={strikePose.length} loop={strikePose.loop} active={handler.enemyController?.IsGameplayActive} reaction={handler.enemyController?.CurrentReactionState} playing={typeof(AnimationHandler_Enemy).GetField("bodyChargeAnimationPlaying", Flags).GetValue(handler)}");
            Assert.That(attack.CanAttack.Value, Is.False, "Returning the animation must not reopen the attack gate.");
            typeof(EnemyAttackAction).GetField("m_Timer", Flags).SetValue(attack, end - .2f);
            Assert.That(CallAttack(attack, "OnUpdate").ToString(), Is.EqualTo("Running"));
            Assert.That(attack.CanAttack.Value, Is.False);
            typeof(EnemyAttackAction).GetField("m_Timer", Flags).SetValue(attack, end + .01f);
            Assert.That(CallAttack(attack, "OnUpdate").ToString(), Is.EqualTo("Success"));
            Assert.That(attack.CanAttack.Value, Is.True);
        }
        finally
        {
            if (attack != null) CallAttack(attack, "OnEnd");
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(host);
        }
    }

    private static object CallAttack(EnemyAttackAction attack, string method)
        => typeof(EnemyAttackAction).GetMethod(method, Flags).Invoke(attack, null);

    [Test]
    public void BossMotionTimeSamplesOnlyMovingFramesDuringRushAndAllEightBrakingFramesAfterwards()
    {
        var root = new GameObject("GIF boss sampling"); root.AddComponent<SpriteRenderer>(); root.AddComponent<Animator>();
        try
        {
            var clip = Clip(111,"boss_AttackCharge");
            Assert.That(Keys(clip).Length, Is.EqualTo(14));
            float[] phases = { 0f,.15f,.3f,.35f,.4f,.45f,.5f,.5625f,.625f,.6875f,.75f,.8125f,.875f,.9375f };
            for (int i = 0; i < phases.Length; i++)
            {
                clip.SampleAnimation(root,(phases[i]+.0001f)*clip.length);
                Assert.That(root.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo($"Gif_Charge_{i:00}"));
            }
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyColorVariantAuthoring.PrefabPath(111)).GetComponent<Project.AIBehaviour.Boss.BossPatternController>();
            Assert.That(boss.ChargeStopAnimationDuration, Is.EqualTo(.8f).Within(.0001f));
            Assert.That(Keys(Clip(111,"boss_AttackCleave"))[1].time, Is.EqualTo(boss.CleaveTiming.telegraphDuration).Within(.0001f));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ShieldChargeKeepsBrakingOutOfTheMovingLoopAndCanBeInterrupted()
    {
        var host = MakeHandler(103, out var handler, out var animator);
        try
        {
            handler.PlayShieldChargeAnticipation(); animator.Update(0f);
            Assert.That(host.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Gif_Dash_00"));
            handler.PlayBodyChargePose(true);
            for (int i = 0; i < 20; i++)
            {
                typeof(AnimationHandler_Enemy).GetMethod("TickGifShieldCharge",Flags).Invoke(handler,new object[] { .1f }); animator.Update(0f);
                CollectionAssert.Contains(new[] { "Gif_Dash_01","Gif_Dash_02","Gif_Dash_03","Gif_Dash_04" },host.GetComponent<SpriteRenderer>().sprite.name);
            }
            handler.EndBodyChargePose(); animator.Update(0f);
            Assert.That(host.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Gif_Dash_05"));
            typeof(AnimationHandler_Enemy).GetMethod("TickGifShieldCharge",Flags).Invoke(handler,new object[] { .16f }); animator.Update(0f);
            Assert.That(host.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Gif_Dash_06"));
            handler.PlayShieldGuardPose(true); animator.Update(0f);
            Assert.That(host.GetComponent<SpriteRenderer>().sprite.name, Does.StartWith("Gif_Guard_"));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase(101)] [TestCase(102)] [TestCase(103)]
    public void GetUpFinishesBeforeReleaseButANewHitCanInterruptIt(int id)
    {
        var host = MakeHandler(id, out var handler, out var animator);
        try
        {
            handler.reactionAnimations.enabled = true;
            handler.reactionAnimations.finishGetUpAnimation = true;
            handler.BeginReactionAnimation(new HitReactionData { reactionKind = EnemyReactionKind.Knockup },false);
            handler.SetReactionState(EnemyReactionState.Knockdown); animator.Update(.05f);
            handler.SetReactionState(EnemyReactionState.Recovering); animator.Update(.05f);
            var state = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(handler.IsFinishingGetUpAnimation, Is.True,
                $"profile={handler.UsesReactionAnimationProfile} active={animator.isActiveAndEnabled} landed={typeof(AnimationHandler_Enemy).GetField("visualLandedKnockdown",Flags).GetValue(handler)} pose={typeof(AnimationHandler_Enemy).GetField("reactionPoseHash",Flags).GetValue(handler)} state={state.fullPathHash} next={animator.GetNextAnimatorStateInfo(0).fullPathHash} time={state.normalizedTime} loop={state.loop}");
            animator.Update(1.2f);
            Assert.That(handler.IsFinishingGetUpAnimation, Is.False);
            handler.SetReactionState(EnemyReactionState.Knockdown); animator.Update(.05f);
            handler.SetReactionState(EnemyReactionState.Recovering); animator.Update(.05f);
            handler.BeginReactionAnimation(new HitReactionData { reactionKind = EnemyReactionKind.Flinch },false);
            handler.SetReactionState(EnemyReactionState.ImpactFreeze); animator.Update(0f);
            Assert.That(handler.IsFinishingGetUpAnimation, Is.False);
            Assert.That(host.GetComponent<SpriteRenderer>().sprite.name, Does.StartWith("Gif_Hurt_"));
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static GameObject MakeHandler(int id, out AnimationHandler_Enemy handler, out Animator animator)
    {
        var host = new GameObject("GIF state test");
        EnemyLifecycleTestUtility.AddActive(host);
        host.AddComponent<SpriteRenderer>();
        handler = host.AddComponent<AnimationHandler_Enemy>();
        handler.anim = animator = host.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyColorVariantAuthoring.PrefabPath(id)).GetComponentInChildren<AnimationHandler_Enemy>().GetComponent<Animator>().runtimeAnimatorController;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind(); animator.Update(0f);
        handler.gifShieldCharge = id == 103; handler.animateShieldGuard = id == 103;
        typeof(AnimationHandler_Enemy).GetMethod("Awake",Flags).Invoke(handler,null);
        return host;
    }

    private static AnimationClip Clip(int id,string key)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyColorVariantAuthoring.PrefabPath(id));
        return EnemyColorVariantAuthoring.ClipOverrides(prefab.GetComponentInChildren<AnimationHandler_Enemy>().GetComponent<Animator>().runtimeAnimatorController).Single(p=>p.Key.name==key).Value;
    }

    private static ObjectReferenceKeyframe[] Keys(AnimationClip clip) => AnimationUtility.GetObjectReferenceCurve(clip,
        new EditorCurveBinding { type=typeof(SpriteRenderer), propertyName="m_Sprite", path="" });

    private static void AssertFrames(int id,string key,string pose,int first,int count)
    {
        var frames=Keys(Clip(id,key)); Assert.That(frames.Length,Is.EqualTo(count));
        for(int i=0;i<count;i++) Assert.That(frames[i].value.name,Is.EqualTo($"Gif_{pose}_{first+i:00}"));
    }
}
#endif
