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
#endif
