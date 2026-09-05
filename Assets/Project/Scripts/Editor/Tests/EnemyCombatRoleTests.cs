#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public class EnemyCombatRoleTests
{
    [Test]
    public void CommitMeleeLocksWindupButNotIdleProfile()
    {
        var go = new GameObject(nameof(CommitMeleeLocksWindupButNotIdleProfile));
        try
        {
            EnemyController enemy = go.AddComponent<EnemyController>();
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
            EnemyController enemy = go.AddComponent<EnemyController>();
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
            EnemyController enemy = go.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 12f;
            blocker.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
            EnemyController enemy = agent.AddComponent<EnemyController>();
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
        EnemyController enemy = host.AddComponent<EnemyController>();
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
