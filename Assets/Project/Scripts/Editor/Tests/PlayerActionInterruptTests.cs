#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public class PlayerActionInterruptTests
{
    private class TestActionState : FrameDataPlayerState
    {
        private ActionFrameData testFrameData;

        public TestActionState(PlayerCharacter player, PlayerStateMachine fsm, ActionFrameData frameData)
            : base(player, fsm)
        {
            testFrameData = frameData;
        }

        public override PlayerStateId StateId => PlayerStateId.Attacking;
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
    public void FrameDataAction_InStartup_CanBeInterrupted()
    {
        var go = new GameObject(nameof(FrameDataAction_InStartup_CanBeInterrupted));
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
            actionState.SetPhaseAndFrame(ActionPhase.Startup, 2);

            IncomingAttackContext context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Normal);

            Assert.That(actionState.CanBeInterruptedByDamage(context), Is.True);
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

    [Test]
    public void TrueSuperArmor_OverridesStartup_PreventingInterruption()
    {
        var go = new GameObject(nameof(TrueSuperArmor_OverridesStartup_PreventingInterruption));
        try
        {
            PlayerCharacter player = go.AddComponent<PlayerCharacter>();
            PlayerStateMachine fsm = go.AddComponent<PlayerStateMachine>();

            player.HasSuperArmor = true;

            var frameData = new ActionFrameData
            {
                startupFrames = 5,
                activeFrames = 10,
                recoveryFrames = 5
            };

            var actionState = new TestActionState(player, fsm, frameData);
            actionState.SetPhaseAndFrame(ActionPhase.Startup, 1);

            IncomingAttackContext context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Normal);

            Assert.That(actionState.CanBeInterruptedByDamage(context), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TakeDamage_DuringActivePhase_AppliesDamageWithoutEnteringHitstun()
    {
        var go = new GameObject(nameof(TakeDamage_DuringActivePhase_AppliesDamageWithoutEnteringHitstun));
        try
        {
            PlayerCharacter player = go.AddComponent<PlayerCharacter>();
            PlayerStateMachine fsm = go.AddComponent<PlayerStateMachine>();
            fsm.playerCharacter = player;

            CharacterObject charObj = ScriptableObject.CreateInstance<CharacterObject>();
            charObj.stats = new CharacterStats { maxHP = 100, defense = 0 };
            player.SetCharacter(charObj, 100f);

            var frameData = new ActionFrameData
            {
                startupFrames = 5,
                activeFrames = 10,
                recoveryFrames = 5
            };

            var actionState = new TestActionState(player, fsm, frameData);
            actionState.SetPhaseAndFrame(ActionPhase.Active, 8);

            fsm.ChangeState(actionState);
            Assert.That(fsm.CurrentState, Is.EqualTo(actionState));

            IncomingAttackContext context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Normal);
            float initialHP = player.CurrentHP;
            bool hitSucceeded = player.TakeDamage(25f, context);

            Assert.That(hitSucceeded, Is.True);
            Assert.That(player.CurrentHP, Is.EqualTo(initialHP - 25f));
            // Action was NOT interrupted: state remains actionState, NOT HitstunState
            Assert.That(fsm.CurrentState, Is.EqualTo(actionState));
            Assert.That(fsm.IsInState(PlayerStateId.Hitstun), Is.False);

            Object.DestroyImmediate(charObj);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TakeDamage_DuringStartup_InterruptsIntoHitstun()
    {
        var go = new GameObject(nameof(TakeDamage_DuringStartup_InterruptsIntoHitstun));
        try
        {
            PlayerCharacter player = go.AddComponent<PlayerCharacter>();
            PlayerStateMachine fsm = go.AddComponent<PlayerStateMachine>();
            fsm.playerCharacter = player;

            CharacterObject charObj = ScriptableObject.CreateInstance<CharacterObject>();
            charObj.stats = new CharacterStats { maxHP = 100, defense = 0 };
            player.SetCharacter(charObj, 100f);

            var frameData = new ActionFrameData
            {
                startupFrames = 5,
                activeFrames = 10,
                recoveryFrames = 5
            };

            var actionState = new TestActionState(player, fsm, frameData);
            actionState.SetPhaseAndFrame(ActionPhase.Startup, 2);

            fsm.ChangeState(actionState);
            Assert.That(fsm.CurrentState, Is.EqualTo(actionState));

            IncomingAttackContext context = IncomingAttackContext.FromEnemy(null, EnemyAttackProperty.Normal);
            bool hitSucceeded = player.TakeDamage(15f, context);

            Assert.That(hitSucceeded, Is.True);
            // Interrupted during startup: transitions into HitstunState
            Assert.That(fsm.CurrentState, Is.EqualTo(fsm.HitstunState));
            Assert.That(fsm.IsInState(PlayerStateId.Hitstun), Is.True);

            Object.DestroyImmediate(charObj);
        }
        finally
        {
            Object.DestroyImmediate(go);
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
