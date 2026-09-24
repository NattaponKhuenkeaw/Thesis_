#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public class EnemyCombatRoleTests
{
    [TestCase("Crouch", "BodyChargeWindup")]
    [TestCase("Kick", "BodyChargeRush")]
    public void ChargePoseUsesNormalizedSpriteWithoutOverridingYellowTint(string pose, string stateName)
    {
        const string folder = "Assets/Project/TestChar/Asset/enemy_anim_test/";
        AnimationClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Enemy2_" + pose + ".anim");
        Assert.That(clip, Is.Not.Null);
        var bindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip);
        Assert.That(bindings.Length, Is.EqualTo(1));
        Assert.That(bindings[0].propertyName, Is.EqualTo("m_Sprite"));
        var keys = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
        Sprite sprite = keys[0].value as Sprite;
        Assert.That(sprite, Is.Not.Null);
        Assert.That(sprite.name, Is.EqualTo(pose + "_2"));
        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(43f));
        Assert.That(sprite.pivot, Is.EqualTo(new Vector2(64f, 0f)));
        Assert.That(UnityEditor.AnimationUtility.GetCurveBindings(clip), Is.Empty, "Pose must not override renderer tint or transform.");
        var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
            "Assets/Project/TestChar/Animation/EnemyAnimatorController.controller");
        bool found = false;
        foreach (var state in controller.layers[0].stateMachine.states)
            if (state.state.name == stateName)
            {
                Assert.That(state.state.motion, Is.EqualTo(clip));
                Assert.That(state.state.transitions, Is.Empty, "Attack timing owns pose duration.");
                found = true;
            }
        Assert.That(found, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BodyChargeOptInControlsActiveArmorAndRestoresHitbox(bool bodyCharge)
    {
        var agent = new GameObject("ChargeTest");
        var hitboxObject = new GameObject("Hitbox");
        EnemyKit kit = ScriptableObject.CreateInstance<EnemyKit>();
        try
        {
            JsonUtility.FromJsonOverwrite("{\"combatRole\":1,\"bodyCharge\":" + (bodyCharge ? "true" : "false") + "}", kit);
            Rigidbody2D body = agent.AddComponent<Rigidbody2D>();
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.Kit = kit;
            enemy.bodyCollider = agent.AddComponent<BoxCollider2D>();
            hitboxObject.transform.SetParent(agent.transform);
            hitboxObject.transform.localPosition = Vector3.right * 2f;
            BoxCollider2D hitbox = hitboxObject.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector2(2f, 3f);
            hitbox.offset = new Vector2(0.3f, 0.4f);
            enemy.attackHitbox = hitbox;
            Physics2D.SyncTransforms();

            var action = new EnemyAttackAction();
            action.Agent.Value = agent;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(EnemyAttackAction).GetField("m_Controller", flags).SetValue(action, enemy);
            typeof(EnemyAttackAction).GetField("m_Body", flags).SetValue(action, body);
            typeof(EnemyAttackAction).GetMethod("BeginCommitment", flags).Invoke(action, null);
            Assert.That(enemy.ignoreHitReactions, Is.True);
            typeof(EnemyAttackAction).GetMethod("StartActualAttack", flags).Invoke(action, null);
            Assert.That(enemy.ignoreHitReactions, Is.EqualTo(bodyCharge));
            if (bodyCharge)
            {
                Assert.That(hitbox.transform.TransformPoint(hitbox.offset).x,
                    Is.EqualTo(enemy.bodyCollider.bounds.center.x).Within(0.001f));
                body.linearVelocity = new Vector2(10f, -2f);
                typeof(EnemyAttackAction).GetField("m_Timer", flags).SetValue(action, 0.5f);
                typeof(EnemyAttackAction).GetMethod("OnUpdate", flags).Invoke(action, null);
                Assert.That(enemy.ignoreHitReactions, Is.False, "Recovery must be punishable.");
                Assert.That(hitbox.enabled, Is.False);
                Assert.That(body.linearVelocity.x, Is.Zero);
            }
            typeof(EnemyAttackAction).GetMethod("OnEnd", flags).Invoke(action, null);
            Assert.That(enemy.ignoreHitReactions, Is.False);
            Assert.That(hitbox.enabled, Is.False);
            Assert.That(hitbox.size, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(hitbox.offset, Is.EqualTo(new Vector2(0.3f, 0.4f)));
            if (bodyCharge)
            {
                Assert.That(body.linearVelocity.x, Is.Zero);
                Assert.That(body.linearVelocity.y, Is.EqualTo(-2f));
            }
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(kit);
        }
    }

    [Test]
    public void Enemy2KitEnablesShortChargeWithoutChangingOtherKits()
    {
        EnemyKit charge = Resources.Load<EnemyKit>("EnemySo/EK_MeleeCommit");
        Assert.That(charge, Is.Not.Null);
        Assert.That(charge.BodyCharge, Is.True);
        Assert.That(charge.LungeDistance, Is.EqualTo(4f));
        Assert.That(charge.WarningDuration, Is.EqualTo(0.8f));
        Assert.That(charge.HitWindowDuration, Is.EqualTo(0.4f));
        Assert.That(charge.RecoveryPlantDuration, Is.EqualTo(0.8f));
        EnemyKit melee = Resources.Load<EnemyKit>("EnemySo/EK_MeleeInterruptible");
        Assert.That(melee, Is.Not.Null);
        Assert.That(melee.BodyCharge, Is.False);
        EnemyKit defaults = ScriptableObject.CreateInstance<EnemyKit>();
        try { Assert.That(defaults.BodyCharge, Is.False); }
        finally { Object.DestroyImmediate(defaults); }
    }

    [Test]
    public void CommitMeleeLocksWindupButNotIdleProfile()
    {
        var go = new GameObject(nameof(CommitMeleeLocksWindupButNotIdleProfile));
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(go);
            enemy.CombatRole = EnemyCombatRole.CommitMelee;

            Assert.That(enemy.UninterruptibleOnceWindup, Is.True);
            Assert.That(enemy.ArmorDuringCommittedSwing, Is.True);
            Assert.That(enemy.UsesRangedAttack, Is.False);
            Assert.That(enemy.NeverEvade, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void InterruptibleMeleeHasNoSwingArmor()
    {
        var go = new GameObject(nameof(InterruptibleMeleeHasNoSwingArmor));
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(go);
            enemy.CombatRole = EnemyCombatRole.InterruptibleMelee;

            Assert.That(enemy.UninterruptibleOnceWindup, Is.False);
            Assert.That(enemy.ArmorDuringCommittedSwing, Is.False);
            Assert.That(enemy.NeverEvade, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RangedHoldUsesLongRangeAndDoesNotEvade()
    {
        var go = new GameObject(nameof(RangedHoldUsesLongRangeAndDoesNotEvade));
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(go);
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 7f;

            Assert.That(enemy.UsesRangedAttack, Is.True);
            Assert.That(enemy.NeverEvade, Is.True);
            Assert.That(enemy.ResolveAttackRange(1.5f), Is.EqualTo(7f));
            Assert.That(enemy.ResolveLungeDistance(0.55f), Is.EqualTo(0f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RangedHoldDoesNotMoveAwayWhenPlayerIsAlreadyInRange()
    {
        var agent = new GameObject(nameof(RangedHoldDoesNotMoveAwayWhenPlayerIsAlreadyInRange));
        var target = new GameObject(nameof(RangedHoldDoesNotMoveAwayWhenPlayerIsAlreadyInRange) + "_Target");
        try
        {
            agent.AddComponent<Rigidbody2D>();
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 7f;
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();

            agent.transform.position = new Vector3(1f, 0f, 0f);
            target.transform.position = Vector3.zero;

            bool inRange = locomotion.MoveTowardsTargetRange(target.transform, 7f, 6f, 0.16f);
            Assert.That(inRange, Is.True);
            Assert.That(agent.transform.position.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(agent.GetComponent<Rigidbody2D>().linearVelocity.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(locomotion.IsAtHoldSlot(target.transform, 7f), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void GroundMeleeDoesNotTreatHigherPlatformAsAttackRange()
    {
        var agent = new GameObject(nameof(GroundMeleeDoesNotTreatHigherPlatformAsAttackRange));
        var target = new GameObject(nameof(GroundMeleeDoesNotTreatHigherPlatformAsAttackRange) + "_Target");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.InterruptibleMelee;
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();

            agent.transform.position = Vector3.zero;
            target.transform.position = new Vector3(0.4f, 2f, 0f);

            Assert.That(locomotion.IsInAttackRangeOfTarget(target.transform, 1.5f), Is.False);
            Assert.That(locomotion.IsOnSamePlatform(target.transform), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void NearbyMeleeStaysBesidePlayerWhenTheyJump()
    {
        var agent = new GameObject(nameof(NearbyMeleeStaysBesidePlayerWhenTheyJump));
        var target = new GameObject(nameof(NearbyMeleeStaysBesidePlayerWhenTheyJump) + "_Target");
        try
        {
            agent.AddComponent<Rigidbody2D>();
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.InterruptibleMelee;
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();

            agent.transform.position = Vector3.zero;
            target.transform.position = new Vector3(0.4f, 2f, 0f);

            Assert.That(locomotion.CanWalkTo(target.transform), Is.True);
            bool inRange = locomotion.MoveTowardsTargetRange(target.transform, 1.5f, 6f, 0.16f);
            Assert.That(inRange, Is.False);
            Assert.That(agent.transform.position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(agent.GetComponent<Rigidbody2D>().linearVelocity.x, Is.EqualTo(0f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void GroundMeleeTreatsSameFloorAsAttackRange()
    {
        var agent = new GameObject(nameof(GroundMeleeTreatsSameFloorAsAttackRange));
        var target = new GameObject(nameof(GroundMeleeTreatsSameFloorAsAttackRange) + "_Target");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.InterruptibleMelee;
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();

            agent.transform.position = Vector3.zero;
            target.transform.position = new Vector3(0.4f, 0f, 0f);

            Assert.That(locomotion.IsOnSamePlatform(target.transform), Is.True);
            Assert.That(locomotion.IsInAttackRangeOfTarget(target.transform, 1.5f), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void RangedHoldDoesNotShootAtPlayerOnHigherPlatform()
    {
        var agent = new GameObject(nameof(RangedHoldDoesNotShootAtPlayerOnHigherPlatform));
        var target = new GameObject(nameof(RangedHoldDoesNotShootAtPlayerOnHigherPlatform) + "_Target");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 7f;
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();

            agent.transform.position = Vector3.zero;
            target.transform.position = new Vector3(2f, 2.5f, 0f);

            Assert.That(locomotion.IsInAttackRangeOfTarget(target.transform, 7f), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void GroundMeleeCanStrikeWhenLedgeWalkIsBlocked()
    {
        var agent = new GameObject(nameof(GroundMeleeCanStrikeWhenLedgeWalkIsBlocked));
        var target = new GameObject(nameof(GroundMeleeCanStrikeWhenLedgeWalkIsBlocked) + "_Target");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.InterruptibleMelee;
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();
            agent.AddComponent<BoxCollider2D>();

            agent.transform.position = Vector3.zero;
            target.transform.position = new Vector3(1f, 0f, 0f);

            Assert.That(locomotion.IsOnSamePlatform(target.transform), Is.True);
            Assert.That(locomotion.IsInAttackRangeOfTarget(target.transform, 1.5f), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void AimPointUsesColliderCenterNotPivot()
    {
        var target = new GameObject(nameof(AimPointUsesColliderCenterNotPivot));
        try
        {
            target.transform.position = Vector3.zero;
            BoxCollider2D body = target.AddComponent<BoxCollider2D>();
            body.offset = new Vector2(0f, 1f);
            body.size = new Vector2(1f, 2f);

            Vector3 aim = EnemyCombatRange.GetAimPoint(target.transform);
            Assert.That(aim.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(aim.y, Is.EqualTo(1f).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void RangedShotDirectionIsFacingNotTowardRaisedTarget()
    {
        var agent = new GameObject(nameof(RangedShotDirectionIsFacingNotTowardRaisedTarget));
        var target = new GameObject(nameof(RangedShotDirectionIsFacingNotTowardRaisedTarget) + "_Target");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            agent.transform.position = Vector3.zero;
            target.transform.position = new Vector3(3f, 4f, 0f);

            Assert.That(enemy.GetRangedShotDirection(), Is.EqualTo(Vector2.right));
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void RangedMuzzleTransformSetsShotOrigin()
    {
        var agent = new GameObject(nameof(RangedMuzzleTransformSetsShotOrigin));
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            var muzzle = new GameObject("RangedMuzzle");
            muzzle.transform.SetParent(agent.transform);
            muzzle.transform.localPosition = new Vector3(1.2f, 0.4f, 0f);
            enemy.RangedMuzzle = muzzle.transform;
            agent.transform.position = new Vector3(2f, 1f, 0f);

            Vector3 origin = enemy.GetRangedMuzzleWorldPosition();
            Assert.That(origin.x, Is.EqualTo(3.2f).Within(0.01f));
            Assert.That(origin.y, Is.EqualTo(1.4f).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(agent);
        }
    }

    [Test]
    public void RangedHoldOnlyAttacksWhenStraightLineHitsBody()
    {
        var agent = new GameObject(nameof(RangedHoldOnlyAttacksWhenStraightLineHitsBody));
        var target = new GameObject(nameof(RangedHoldOnlyAttacksWhenStraightLineHitsBody) + "_Target");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 7f;
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();
            BoxCollider2D body = target.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.8f, 1.6f);

            agent.transform.position = Vector3.zero;
            target.transform.position = new Vector3(3f, 0f, 0f);
            Physics2D.SyncTransforms();

            Assert.That(enemy.HasClearStraightShot(target.transform, 7f), Is.True);
            Assert.That(locomotion.IsInAttackRangeOfTarget(target.transform, 7f), Is.True);

            target.transform.position = new Vector3(3f, 2.4f, 0f);
            Physics2D.SyncTransforms();

            Assert.That(enemy.HasClearStraightShot(target.transform, 7f), Is.False);
            Assert.That(locomotion.IsInAttackRangeOfTarget(target.transform, 7f), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void RangedHoldIsNotInRangeWhenFartherThanAttackRange()
    {
        var agent = new GameObject(nameof(RangedHoldIsNotInRangeWhenFartherThanAttackRange));
        var target = new GameObject(nameof(RangedHoldIsNotInRangeWhenFartherThanAttackRange) + "_Target");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 7f;
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();

            agent.transform.position = Vector3.zero;
            target.transform.position = new Vector3(20f, 0f, 0f);

            Assert.That(locomotion.IsInAttackRangeOfTarget(target.transform, 7f), Is.False);
            Assert.That(enemy.HasClearStraightShot(target.transform, 7f), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void WalkingFacesMoveDirectionNotTheTarget()
    {
        var agent = new GameObject(nameof(WalkingFacesMoveDirectionNotTheTarget));
        var target = new GameObject(nameof(WalkingFacesMoveDirectionNotTheTarget) + "_Target");
        try
        {
            agent.AddComponent<Rigidbody2D>();
            EnemyLifecycleTestUtility.AddActive(agent);
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();
            agent.transform.position = new Vector3(4f, 0f, 0f);
            target.transform.position = new Vector3(10f, 0f, 0f);
            EnemyFacingUtil.FaceHorizontal(agent.transform, 1f);

            locomotion.MoveTowardsX(0f, 6f);

            Assert.That(EnemyFacingUtil.GetFacingSign(agent.transform), Is.EqualTo(-1f));
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void LockedFacingSurvivesWalkingTheOtherWay()
    {
        var agent = new GameObject(nameof(LockedFacingSurvivesWalkingTheOtherWay));
        try
        {
            agent.AddComponent<Rigidbody2D>();
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();
            agent.transform.position = new Vector3(4f, 0f, 0f);
            EnemyFacingUtil.FaceHorizontal(agent.transform, 1f);

            enemy.LockFacing();
            locomotion.MoveTowardsX(0f, 6f);

            Assert.That(EnemyFacingUtil.GetFacingSign(agent.transform), Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(agent);
        }
    }

    [Test]
    public void ReleasingFacingLetsTheEnemyTurnAgain()
    {
        var agent = new GameObject(nameof(ReleasingFacingLetsTheEnemyTurnAgain));
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            EnemyFacingUtil.FaceHorizontal(agent.transform, 1f);

            enemy.LockFacing();
            EnemyFacingUtil.FaceHorizontal(agent.transform, -1f);
            Assert.That(EnemyFacingUtil.GetFacingSign(agent.transform), Is.EqualTo(1f));

            enemy.ReleaseFacing();
            EnemyFacingUtil.FaceHorizontal(agent.transform, -1f);
            Assert.That(EnemyFacingUtil.GetFacingSign(agent.transform), Is.EqualTo(-1f));
        }
        finally
        {
            Object.DestroyImmediate(agent);
        }
    }

    [Test]
    public void HoldCommittedFacingOutlivesTheAttackNode()
    {
        var agent = new GameObject(nameof(HoldCommittedFacingOutlivesTheAttackNode));
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            EnemyFacingUtil.FaceHorizontal(agent.transform, 1f);

            enemy.HoldCommittedFacing(10f);
            EnemyFacingUtil.FaceHorizontal(agent.transform, -1f);
            Assert.That(EnemyFacingUtil.GetFacingSign(agent.transform), Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(agent);
        }
    }

    [Test]
    public void InRangeMeleeWalksOutToRearSlotInsteadOfStoppingOnTheLead()
    {
        var target = new GameObject(nameof(InRangeMeleeWalksOutToRearSlotInsteadOfStoppingOnTheLead) + "_Target");
        var lead = new GameObject(nameof(InRangeMeleeWalksOutToRearSlotInsteadOfStoppingOnTheLead) + "_Lead");
        var rear = new GameObject(nameof(InRangeMeleeWalksOutToRearSlotInsteadOfStoppingOnTheLead) + "_Rear");
        try
        {
            target.transform.position = Vector3.zero;
            lead.transform.position = new Vector3(1.05f, 0f, 0f);
            rear.transform.position = new Vector3(1.2f, 0f, 0f);

            lead.AddComponent<Rigidbody2D>();
            rear.AddComponent<Rigidbody2D>();
            EnemyLocomotion rearLoco = BuildRegistered(rear);
            BuildRegistered(lead);

            // Both bodies sit inside the 1.5 attack band. Chase used to treat that as "arrived" and
            // freeze them on top of each other. The rear slot is further out, so this one must walk away.
            Assert.That(rearLoco.IsInAttackRangeOfTarget(target.transform, 1.5f), Is.True);
            Assert.That(rearLoco.IsAtHoldSlot(target.transform, 1.5f), Is.False);

            rearLoco.MoveTowardsTargetRange(target.transform, 1.5f, 6f, 0.16f);
            Assert.That(rear.GetComponent<Rigidbody2D>().linearVelocity.x, Is.GreaterThan(0.1f));
        }
        finally
        {
            ReleaseRegistered(lead);
            ReleaseRegistered(rear);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void RangedClosesTowardHoldRangeNotThePlayer()
    {
        var agent = new GameObject(nameof(RangedClosesTowardHoldRangeNotThePlayer));
        var target = new GameObject(nameof(RangedClosesTowardHoldRangeNotThePlayer) + "_Target");
        try
        {
            Rigidbody2D body = agent.AddComponent<Rigidbody2D>();
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 12f;
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();

            agent.transform.position = new Vector3(20f, 0f, 0f);
            target.transform.position = Vector3.zero;

            Assert.That(enemy.ResolveHoldRange(12f), Is.EqualTo(7f).Within(0.0001f));
            Assert.That(locomotion.IsAtHoldSlot(target.transform, 12f), Is.False);

            locomotion.MoveTowardsTargetRange(target.transform, 12f, 6f, 0.16f);
            Assert.That(body.linearVelocity.x, Is.LessThan(-0.1f));
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void AlliedBodiesDoNotBlockAStraightShot()
    {
        var agent = new GameObject(nameof(AlliedBodiesDoNotBlockAStraightShot));
        var blocker = new GameObject(nameof(AlliedBodiesDoNotBlockAStraightShot) + "_Blocker");
        var target = new GameObject(nameof(AlliedBodiesDoNotBlockAStraightShot) + "_Target");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 12f;
            EnemyLifecycleTestUtility.AddActive(blocker);
            BoxCollider2D wall = blocker.AddComponent<BoxCollider2D>();
            wall.size = new Vector2(1.35f, 2.4f);
            BoxCollider2D body = target.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.8f, 1.6f);

            agent.transform.position = Vector3.zero;
            blocker.transform.position = new Vector3(2f, 0f, 0f);
            target.transform.position = new Vector3(6f, 0f, 0f);
            Physics2D.SyncTransforms();

            Assert.That(enemy.HasClearStraightShot(target.transform, 12f), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(blocker);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void HuggingPlayerStillCountsAsAClearShot()
    {
        var agent = new GameObject(nameof(HuggingPlayerStillCountsAsAClearShot));
        var target = new GameObject(nameof(HuggingPlayerStillCountsAsAClearShot) + "_Target");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 12f;
            BoxCollider2D body = target.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.8f, 1.6f);

            agent.transform.position = Vector3.zero;
            // Muzzle sits at +0.85. A body at +0.4 is in front of the enemy and behind the muzzle.
            target.transform.position = new Vector3(0.4f, 0f, 0f);
            Physics2D.SyncTransforms();

            Assert.That(enemy.HasClearStraightShot(target.transform, 12f), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void WorldGeometryStillBlocksAStraightShot()
    {
        var agent = new GameObject(nameof(WorldGeometryStillBlocksAStraightShot));
        var wall = new GameObject(nameof(WorldGeometryStillBlocksAStraightShot) + "_Wall");
        var target = new GameObject(nameof(WorldGeometryStillBlocksAStraightShot) + "_Target");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(agent);
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 12f;
            BoxCollider2D geometry = wall.AddComponent<BoxCollider2D>();
            geometry.size = new Vector2(1f, 3f);
            BoxCollider2D body = target.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.8f, 1.6f);

            agent.transform.position = Vector3.zero;
            wall.transform.position = new Vector3(2f, 0f, 0f);
            target.transform.position = new Vector3(6f, 0f, 0f);
            Physics2D.SyncTransforms();

            Assert.That(enemy.HasClearStraightShot(target.transform, 12f), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(target);
        }
    }

    private static EnemyLocomotion BuildRegistered(GameObject host)
    {
        EnemyController enemy = EnemyLifecycleTestUtility.AddActive(host);
        EnemyLocomotion locomotion = host.AddComponent<EnemyLocomotion>();
        RoomEnemyRegistry.Register(enemy);
        return locomotion;
    }

    private static void ReleaseRegistered(GameObject host)
    {
        RoomEnemyRegistry.Unregister(host.GetComponent<EnemyController>());
        Object.DestroyImmediate(host);
    }
}
#endif
