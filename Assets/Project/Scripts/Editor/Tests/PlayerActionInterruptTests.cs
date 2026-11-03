#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public class PlayerActionInterruptTests
{
    private class TestActionState : FrameDataPlayerState
    {
        private ActionFrameData testFrameData;
        private readonly PlayerStateId stateId;

        public TestActionState(PlayerCharacter player, PlayerStateMachine fsm, ActionFrameData frameData,
            PlayerStateId stateId = PlayerStateId.Attacking)
            : base(player, fsm)
        {
            testFrameData = frameData;
            this.stateId = stateId;
        }

        public override PlayerStateId StateId => stateId;
        public override PlayerStatePriority Priority => PlayerStatePriority.NormalAttack;

        protected override ActionFrameData GetFrameData() => testFrameData;

        public void SetPhaseAndFrame(ActionPhase phase, int frame)
        {
            currentPhase = phase;
            currentFrame = frame;
        }
    }

    [Test]
    public void BaseState_AllowsInterruptionByDamage()
    {
        var go = new GameObject(nameof(BaseState_AllowsInterruptionByDamage));
        try
        {
            PlayerCharacter player = go.AddComponent<PlayerCharacter>();
            PlayerStateMachine fsm = go.AddComponent<PlayerStateMachine>();

            PlayerIdleState idle = new PlayerIdleState(player, fsm);
            IncomingAttackContext context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Normal);

            Assert.That(idle.CanBeInterruptedByDamage(context), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void FrameDataAction_InActivePhase_HasPseudoSuperArmor()
    {
        var go = new GameObject(nameof(FrameDataAction_InActivePhase_HasPseudoSuperArmor));
        try
        {
            PlayerCharacter player = go.AddComponent<PlayerCharacter>();
            PlayerStateMachine fsm = go.AddComponent<PlayerStateMachine>();

            var frameData = new ActionFrameData
            {
                startupFrames = 5,
                activeFrames = 10,
                recoveryFrames = 5
            };

            var actionState = new TestActionState(player, fsm, frameData);
            actionState.SetPhaseAndFrame(ActionPhase.Active, 7);

            IncomingAttackContext context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Normal);

            // In active phase past the starting window, pseudo super armor prevents interruption
            Assert.That(actionState.CanBeInterruptedByDamage(context), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void FrameDataAction_InRecoveryPhase_HasPseudoSuperArmor()
    {
        var go = new GameObject(nameof(FrameDataAction_InRecoveryPhase_HasPseudoSuperArmor));
        try
        {
            PlayerCharacter player = go.AddComponent<PlayerCharacter>();
            PlayerStateMachine fsm = go.AddComponent<PlayerStateMachine>();

            var frameData = new ActionFrameData
            {
                startupFrames = 5,
                activeFrames = 10,
                recoveryFrames = 5
            };

            var actionState = new TestActionState(player, fsm, frameData);
            actionState.SetPhaseAndFrame(ActionPhase.Recovery, 16);

            IncomingAttackContext context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Normal);

            Assert.That(actionState.CanBeInterruptedByDamage(context), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void FrameDataAction_InCancelPhase_HasPseudoSuperArmor()
    {
        var go = new GameObject(nameof(FrameDataAction_InCancelPhase_HasPseudoSuperArmor));
        try
        {
            PlayerCharacter player = go.AddComponent<PlayerCharacter>();
            PlayerStateMachine fsm = go.AddComponent<PlayerStateMachine>();

            var frameData = new ActionFrameData
            {
                startupFrames = 5,
                activeFrames = 10,
                recoveryFrames = 10,
                cancelWindowStartFrame = 16
            };

            var actionState = new TestActionState(player, fsm, frameData);
            actionState.SetPhaseAndFrame(ActionPhase.Cancel, 18);

            IncomingAttackContext context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Normal);

            Assert.That(actionState.CanBeInterruptedByDamage(context), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void HitstunState_AlwaysAllowsInterruptionByDamage()
    {
        var go = new GameObject(nameof(HitstunState_AlwaysAllowsInterruptionByDamage));
        try
        {
            PlayerCharacter player = go.AddComponent<PlayerCharacter>();
            PlayerStateMachine fsm = go.AddComponent<PlayerStateMachine>();

            PlayerHitstunState hitstun = new PlayerHitstunState(player, fsm);
            IncomingAttackContext context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Normal);

            Assert.That(hitstun.CanBeInterruptedByDamage(context), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [TestCase(ActionPhase.None, -1, 5)]
    [TestCase(ActionPhase.Startup, 2, 5)]
    [TestCase(ActionPhase.Active, 1, 0)]
    [TestCase(ActionPhase.Active, 8, 5)]
    public void TakeDamage_DuringAction_AppliesDamageWithoutEnteringHitstun(
        ActionPhase phase, int frame, int startupFrames)
    {
        var go = new GameObject(nameof(TakeDamage_DuringAction_AppliesDamageWithoutEnteringHitstun));
        var charObj = ScriptableObject.CreateInstance<CharacterObject>();
        try
        {
            PlayerCharacter player = go.AddComponent<PlayerCharacter>();
            PlayerStateMachine fsm = go.AddComponent<PlayerStateMachine>();
            fsm.playerCharacter = player;

            charObj.stats = new CharacterStats { maxHP = 100, defense = 0 };
            player.SetCharacter(charObj, 100f);

            var frameData = new ActionFrameData
            {
                startupFrames = startupFrames,
                activeFrames = 10,
                recoveryFrames = 5
            };

            var actionState = new TestActionState(player, fsm, frameData);
            fsm.ChangeState(actionState);
            actionState.SetPhaseAndFrame(phase, frame);

            IncomingAttackContext context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Normal);
            float initialHP = player.CurrentHP;

            Assert.That(actionState.CanBeInterruptedByDamage(context), Is.False);
            Assert.That(player.TakeDamage(25f, context), Is.True);
            Assert.That(player.CurrentHP, Is.EqualTo(initialHP - 25f));
            Assert.That(fsm.CurrentState, Is.SameAs(actionState));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(charObj);
        }
    }

    [TestCase(PlayerStateId.Ultimate)]
    [TestCase(PlayerStateId.QteIntro)]
    public void TakeDamage_ProtectedActionBlocksDamageUntilExit(PlayerStateId stateId)
    {
        var go = new GameObject(nameof(TakeDamage_ProtectedActionBlocksDamageUntilExit));
        var charObj = ScriptableObject.CreateInstance<CharacterObject>();
        try
        {
            var player = go.AddComponent<PlayerCharacter>();
            var fsm = go.AddComponent<PlayerStateMachine>();
            fsm.playerCharacter = player;
            player.StateMachine = fsm;
            charObj.stats = new CharacterStats { maxHP = 100, defense = 0 };
            player.SetCharacter(charObj, 100f);

            var action = new TestActionState(player, fsm, new ActionFrameData(), stateId);
            fsm.ChangeState(action);
            var context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Undodgeable);
            float initialHP = player.CurrentHP;

            foreach (var phase in new[] { ActionPhase.None, ActionPhase.Startup, ActionPhase.Active,
                         ActionPhase.Recovery, ActionPhase.Cancel, ActionPhase.Done })
            {
                action.SetPhaseAndFrame(phase, 1);
                Assert.That(player.TakeDamage(1000f, context), Is.False, phase.ToString());
                Assert.That(player.CurrentHP, Is.EqualTo(initialHP), phase.ToString());
            }

            fsm.ChangeState(new TestActionState(player, fsm, new ActionFrameData()));
            Assert.That(player.TakeDamage(25f, context), Is.True);
            Assert.That(player.CurrentHP, Is.EqualTo(initialHP - 25f));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(charObj);
        }
    }

    [Test]
    public void TakeDamage_LethalDamage_AlwaysInterruptsAndKills()
    {
        var go = new GameObject(nameof(TakeDamage_LethalDamage_AlwaysInterruptsAndKills));
        try
        {
            PlayerCharacter player = go.AddComponent<PlayerCharacter>();
            PlayerStateMachine fsm = go.AddComponent<PlayerStateMachine>();
            fsm.playerCharacter = player;

            CharacterObject charObj = ScriptableObject.CreateInstance<CharacterObject>();
            charObj.stats = new CharacterStats { maxHP = 100, defense = 0 };
            player.SetCharacter(charObj, 50f);

            var frameData = new ActionFrameData
            {
                startupFrames = 5,
                activeFrames = 10,
                recoveryFrames = 5
            };

            var actionState = new TestActionState(player, fsm, frameData);
            actionState.SetPhaseAndFrame(ActionPhase.Active, 8);

            fsm.ChangeState(actionState);

            IncomingAttackContext context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Normal);
            bool hitSucceeded = player.TakeDamage(100f, context);

            Assert.That(hitSucceeded, Is.True);
            Assert.That(player.IsDead, Is.True);
            Assert.That(player.CurrentHP, Is.EqualTo(0f));

            Object.DestroyImmediate(charObj);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
public class PlayerMovementWallSweepTests
{
    [TestCase(1f)]
    [TestCase(-1f)]
    public void FastTravelStopsAtThinWallInEitherDirection(float sign)
    {
        var player = new GameObject("SweepPlayer");
        var wall = new GameObject("ThinRoomDivider");
        try
        {
            player.transform.position = new Vector3(2000f, 20f);
            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            player.AddComponent<BoxCollider2D>().size = Vector2.one;
            var movement = player.AddComponent<PlayerMovement>();
            typeof(PlayerMovement).GetField("rb", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).SetValue(movement, body);
            typeof(PlayerMovement).GetField("enemyLayer", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).SetValue(movement, LayerMask.NameToLayer("Enemy"));
            wall.transform.position = player.transform.position + Vector3.right * sign * 2f;
            var divider = wall.AddComponent<BoxCollider2D>();
            divider.size = new Vector2(.01f, 10f);
            Physics2D.SyncTransforms();
            float velocity = (float)typeof(PlayerMovement).GetMethod("ClipHorizontalVelocity",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(movement, new object[] { sign * 10f / Time.fixedDeltaTime });
            Assert.That(velocity * Time.fixedDeltaTime * sign, Is.InRange(1.46f, 1.49f));
            Vector3 moved = movement.TeleportHorizontally(body.position.x + sign * 10f);
            Assert.That(moved.x * sign, Is.InRange(1.46f, 1.49f));
            Assert.That(player.GetComponent<Collider2D>().bounds.Intersects(divider.bounds), Is.False);
            Vector3 away = movement.TeleportHorizontally(body.position.x - sign);
            Assert.That(away.x * sign, Is.EqualTo(-1f).Within(.001f));
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(wall);
        }
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void OpenDividerDoesNotBlockTravel(bool enabled, bool trigger)
    {
        var player = new GameObject("SweepPlayer");
        var wall = new GameObject("OpenRoomDivider");
        try
        {
            player.transform.position = new Vector3(2000f, 20f);
            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            player.AddComponent<BoxCollider2D>().size = Vector2.one;
            var movement = player.AddComponent<PlayerMovement>();
            typeof(PlayerMovement).GetField("rb", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).SetValue(movement, body);
            typeof(PlayerMovement).GetField("enemyLayer", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).SetValue(movement, LayerMask.NameToLayer("Enemy"));
            wall.transform.position = player.transform.position + Vector3.right * 2f;
            var divider = wall.AddComponent<BoxCollider2D>();
            divider.size = new Vector2(.01f, 10f);
            divider.enabled = enabled;
            divider.isTrigger = trigger;
            Physics2D.SyncTransforms();
            Vector3 moved = movement.TeleportHorizontally(body.position.x + 10f);
            Assert.That(moved.x, Is.EqualTo(10f).Within(.001f));
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(wall);
        }
    }
}
#endif
