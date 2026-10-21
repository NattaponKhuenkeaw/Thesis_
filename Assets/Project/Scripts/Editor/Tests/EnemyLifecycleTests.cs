#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

internal static class EnemyLifecycleTestUtility
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public static EnemyController AddActive(GameObject host)
    {
        var enemy = host.AddComponent<EnemyController>();
        InitializeComponents(enemy);
        BeginSpawn(enemy);
        Activate(enemy);
        return enemy;
    }

    public static void BeginSpawn(EnemyController enemy)
        => typeof(EnemyController).GetMethod("Start", Flags).Invoke(enemy, null);

    public static void InitializeComponents(EnemyController enemy)
        => typeof(EnemyController).GetMethod("Awake", Flags).Invoke(enemy, null);

    public static void Activate(EnemyController enemy)
        => typeof(EnemyController).GetMethod("TransitionToActiveLifecycle", Flags).Invoke(enemy, null);
}

public class EnemyLifecycleTests
{
    [TestCase(1.2f)]
    [TestCase(2f)]
    public void GroundSpawnAlignsColliderFeetEvenWhenSpawnPointIsHigh(float scale)
    {
        var host = new GameObject("GroundSpawnAlignment");
        var floor = new GameObject("SpawnFloor");
        try
        {
            host.transform.position = new Vector3(2000f, 5f, 0f);
            host.transform.localScale = Vector3.one * scale;
            var body = host.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var collider = host.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 2f);
            collider.offset = new Vector2(0f, 1f);
            var enemy = host.AddComponent<EnemyController>();
            enemy.bodyCollider = collider;
            EnemyLifecycleTestUtility.InitializeComponents(enemy);
            var data = new UnityEditor.SerializedObject(enemy);
            data.FindProperty("spawnGroundLayers").intValue = ~0;
            data.ApplyModifiedPropertiesWithoutUndo();
            floor.transform.position = new Vector3(2000f, -.5f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(20f, 1f);
            Physics2D.SyncTransforms();
            Assert.That(enemy.TryPlaceSpawnOnGround(), Is.True);
            Assert.That(collider.bounds.min.y, Is.EqualTo(.01f).Within(.002f));
            Assert.That(host.transform.position.x, Is.EqualTo(2000f));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(floor);
        }
    }

    [Test]
    public void GroundSpawnWithNoTerrainDoesNotMoveToAnInventedFloor()
    {
        var host = new GameObject("UnsupportedSpawn");
        try
        {
            host.transform.position = new Vector3(2000f, 500f, 0f);
            host.AddComponent<BoxCollider2D>();
            var enemy = host.AddComponent<EnemyController>();
            Vector3 before = host.transform.position;
            Assert.That(enemy.TryPlaceSpawnOnGround(), Is.False);
            Assert.That(host.transform.position, Is.EqualTo(before));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase("enemy/Enm101/Enemy1")]
    [TestCase("enemy/Enm102/Enemy102")]
    [TestCase("enemy/Enm103/Enemy103")]
    [TestCase("Boss/Executioner/Boss_Executioner")]
    public void ReleaseEnemiesHaveLifecycleVfxMaterials(string path)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/" + path + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        var effect = prefab.GetComponent<EnemyLifecycleVFX>();
        Assert.That(effect, Is.Not.Null);
        var data = new UnityEditor.SerializedObject(effect);
        Assert.That(data.FindProperty("dissolveMaterial").objectReferenceValue, Is.Not.Null);
        Assert.That(data.FindProperty("particleMaterial").objectReferenceValue, Is.Not.Null);
        var dissolveSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Project/Audio/SFX/Shared_EnemyDissolve_ShopBuySuccess.wav");
        Assert.That(dissolveSound, Is.Not.Null);
        Assert.That(data.FindProperty("deathDissolveSfx").objectReferenceValue, Is.SameAs(dissolveSound));
        Assert.That(data.FindProperty("deathDissolveSfxVolume").floatValue, Is.GreaterThan(0f));
        Assert.That(effect.SpawnDuration, Is.GreaterThan(0f));
    }

    [Test]
    public void LifecycleVfxWaitsForSpawnAndDeathAnimationBeforeCompleting()
    {
        var host = new GameObject("LifecycleVfxTiming");
        try
        {
            var effect = host.AddComponent<EnemyLifecycleVFX>();
            effect.BeginSpawn();
            Assert.That(effect.TickSpawn(effect.SpawnDuration * .5f), Is.False);
            Assert.That(effect.TickSpawn(effect.SpawnDuration), Is.True);
            effect.FinishSpawn();
            effect.BeginDeath();
            Assert.That(effect.TickDeath(2f, false), Is.False);
            Assert.That(effect.IsDeathDissolving, Is.False);
            Assert.That(effect.TickDeath(2f, true), Is.False);
            Assert.That(effect.IsDeathDissolving, Is.True);
            Assert.That(effect.TickDeath(2f + effect.DeathFadeDuration * .5f, true), Is.False);
            Assert.That(typeof(EnemyLifecycleVFX).GetField("deathFadeStarted", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(effect), Is.EqualTo(2f), "Repeated ticks must not restart the dissolve or its audio.");
            Assert.That(effect.TickDeath(2f + effect.DeathFadeDuration + .01f, true), Is.True);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void LifecycleVfxRestoresOriginalSpriteMaterialAndProperties()
    {
        var host = new GameObject("LifecycleVfxRestore");
        try
        {
            var sprite = host.AddComponent<SpriteRenderer>();
            Material original = sprite.sharedMaterial;
            var block = new MaterialPropertyBlock();
            block.SetFloat("_TestValue", 7f);
            sprite.SetPropertyBlock(block);
            var effect = host.AddComponent<EnemyLifecycleVFX>();
            var data = new UnityEditor.SerializedObject(effect);
            data.FindProperty("dissolveMaterial").objectReferenceValue =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Project/Resources/EnemyLifecycleDissolve.mat");
            data.ApplyModifiedPropertiesWithoutUndo();
            effect.BeginSpawn();
            effect.TickSpawn(.2f);
            effect.FinishSpawn();
            Assert.That(sprite.sharedMaterial, Is.SameAs(original));
            sprite.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_TestValue"), Is.EqualTo(7f));
        }
        finally { Object.DestroyImmediate(host); }
    }

    private class RoomTraversalTestState : PlayerState
    {
        private readonly PlayerStateId id;
        public RoomTraversalTestState(PlayerCharacter player, PlayerStateMachine fsm, PlayerStateId id)
            : base(player, fsm) { this.id = id; }
        public override PlayerStateId StateId => id;
    }

    [TestCase(PlayerStateId.Intro, false)]
    [TestCase(PlayerStateId.Exit, false)]
    [TestCase(PlayerStateId.Idle, true)]
    [TestCase(PlayerStateId.Attacking, true)]
    [TestCase(PlayerStateId.Hitstun, true)]
    [TestCase(PlayerStateId.Ultimate, true)]
    public void EnemyCombatWaitsForTraversalButNotNormalPlayerActions(PlayerStateId stateId, bool ready)
    {
        PlayerCharacter previous = PlayerCharacter.Instance;
        var playerHost = new GameObject("RoomTraversalPlayer");
        var enemyHost = new GameObject("RoomTraversalEnemy");
        var hitboxHost = new GameObject("RoomTraversalHitbox");
        try
        {
            var player = playerHost.AddComponent<PlayerCharacter>();
            var state = playerHost.AddComponent<PlayerStateMachine>();
            player.StateMachine = state;
            typeof(PlayerCharacter).GetProperty("Instance").SetValue(null, player);
            typeof(PlayerStateMachine).GetProperty("CurrentState").SetValue(state,
                new RoomTraversalTestState(player, state, stateId));
            enemyHost.AddComponent<Rigidbody2D>().gravityScale = 0f;
            enemyHost.AddComponent<BoxCollider2D>();
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(enemyHost);
            hitboxHost.transform.SetParent(enemyHost.transform);
            enemy.attackHitbox = hitboxHost.AddComponent<BoxCollider2D>();
            Assert.That(enemy.IsGameplayActive, Is.True);
            Assert.That(enemy.IsCombatReady, Is.EqualTo(ready));
            enemy.SetAttackHitboxActive(true);
            Assert.That(enemy.attackHitbox.enabled, Is.EqualTo(ready));
            var locomotion = enemyHost.AddComponent<EnemyLocomotion>();
            locomotion.SetHorizontalSpeed(3f);
            Assert.That(enemy.rb.linearVelocity.x, Is.EqualTo(ready ? 3f : 0f).Within(.001f));
        }
        finally
        {
            Object.DestroyImmediate(hitboxHost);
            Object.DestroyImmediate(enemyHost);
            Object.DestroyImmediate(playerHost);
            typeof(PlayerCharacter).GetProperty("Instance").SetValue(null, previous);
        }
    }

    [Test]
    public void DeferredEnemyBrainRequestResumesWhenIntroEnds()
    {
        PlayerCharacter previous = PlayerCharacter.Instance;
        var playerHost = new GameObject("DeferredBrainPlayer");
        var enemyHost = new GameObject("DeferredBrainEnemy");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var player = playerHost.AddComponent<PlayerCharacter>();
            var state = playerHost.AddComponent<PlayerStateMachine>();
            player.StateMachine = state;
            typeof(PlayerCharacter).GetProperty("Instance").SetValue(null, player);
            typeof(PlayerStateMachine).GetProperty("CurrentState").SetValue(state,
                new RoomTraversalTestState(player, state, PlayerStateId.Intro));
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(enemyHost);
            var pending = typeof(EnemyController).GetField("resumeBehaviorAfterRoomTraversal", flags);
            var tick = typeof(EnemyController).GetMethod("UpdateRoomTraversalPause", flags);
            Assert.That(pending.GetValue(enemy), Is.EqualTo(true));
            tick.Invoke(enemy, null);
            Assert.That(pending.GetValue(enemy), Is.EqualTo(true));
            typeof(PlayerStateMachine).GetProperty("CurrentState").SetValue(state,
                new RoomTraversalTestState(player, state, PlayerStateId.Idle));
            tick.Invoke(enemy, null);
            Assert.That(enemy.IsCombatReady, Is.True);
            Assert.That(pending.GetValue(enemy), Is.EqualTo(false));
        }
        finally
        {
            Object.DestroyImmediate(enemyHost);
            Object.DestroyImmediate(playerHost);
            typeof(PlayerCharacter).GetProperty("Instance").SetValue(null, previous);
        }
    }

    [Test]
    public void ExecutionerPrefabWaitsForGroundBeforeActivatingAI()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab");
        Assert.That(prefab, Is.Not.Null);
        var enemy = prefab.GetComponent<EnemyController>();
        Assert.That(enemy, Is.Not.Null);
        var data = new UnityEditor.SerializedObject(enemy);
        Assert.That(data.FindProperty("useSpawnPresentation").boolValue, Is.True);
        int mask = data.FindProperty("spawnGroundLayers").intValue;
        Assert.That(mask & LayerMask.GetMask("Ground"), Is.Not.Zero);
        var body = prefab.GetComponent<Rigidbody2D>();
        Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
        Assert.That(body.simulated, Is.True);
        Assert.That(body.gravityScale, Is.GreaterThan(0f));
        Assert.That(body.constraints & RigidbodyConstraints2D.FreezePositionY,
            Is.EqualTo(RigidbodyConstraints2D.None));
        Assert.That(enemy.bodyCollider, Is.Not.Null);
        Assert.That(enemy.bodyCollider.enabled, Is.True);
        Assert.That(enemy.bodyCollider.isTrigger, Is.False);
    }

    [Test]
    public void AuthoredAnimationWaitsForCompletionAndRestoresCulling()
    {
        var host = new GameObject("AnimatedEnemy");
        var clip = new AnimationClip();
        var graph = new AnimatorController();
        AnimatorStateMachine machine = null;
        AnimatorState state = null;
        try
        {
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 0f));
            graph.AddLayer("Base Layer");
            machine = graph.layers[0].stateMachine;
            state = machine.AddState("SpawnGround");
            state.motion = clip;
            var animator = host.AddComponent<Animator>();
            animator.runtimeAnimatorController = graph;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            var handler = host.AddComponent<AnimationHandler_Enemy>();
            handler.anim = animator;
            animator.Rebind();
            Assert.That(handler.PlayLifecycleAnimation("Base Layer.SpawnGround"), Is.True);
            typeof(AnimationHandler_Enemy).GetField("lifecycleAnimationFrame", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(handler, -1);
            animator.Update(0.25f);
            Assert.That(handler.IsLifecycleAnimationComplete(), Is.False);
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
            animator.Update(1f);
            Assert.That(handler.IsLifecycleAnimationComplete(), Is.True);
            handler.EndLifecycleAnimation();
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely));
        }
        finally
        {
            Object.DestroyImmediate(host);
            if (state != null) Object.DestroyImmediate(state);
            if (machine != null) Object.DestroyImmediate(machine);
            Object.DestroyImmediate(graph);
            Object.DestroyImmediate(clip);
        }
    }

    private static void SetField(EnemyController enemy, string name, object value)
        => typeof(EnemyController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, value);

    private static bool TickSpawn(EnemyController enemy, float elapsed)
        => (bool)typeof(EnemyController).GetMethod("TickSpawnPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(enemy, new object[] { elapsed });

    [Test]
    public void GroundSpawnUsesColliderFeetAndDoesNotWaitForANewLanding()
    {
        var host = new GameObject("GroundSpawn");
        var floor = new GameObject("Floor");
        try
        {
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(10f, 1f);
            var body = host.AddComponent<Rigidbody2D>();
            var collider = host.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2(0f, 0.5f);
            var enemy = host.AddComponent<EnemyController>();
            enemy.bodyCollider = collider;
            EnemyLifecycleTestUtility.InitializeComponents(enemy);
            SetField(enemy, "useSpawnPresentation", true);
            Physics2D.SyncTransforms();
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            Assert.That(body.simulated, Is.False, "Ground entrance should not fall.");
            Assert.That(TickSpawn(enemy, 0.1f), Is.True, "Without a clip, grounded legacy fallback can finish.");
            Assert.That(enemy.IsTargetable, Is.False, "Only the lifecycle transition may unlock gameplay.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(floor);
        }
    }

    [Test]
    public void AirSpawnWaitsForFloorAndIgnoresWall()
    {
        var host = new GameObject("AirSpawn");
        var floor = new GameObject("Floor");
        var wall = new GameObject("Wall");
        try
        {
            floor.layer = wall.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(10f, 1f);
            wall.transform.position = new Vector3(1f, 3f, 0f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 8f);
            host.transform.position = new Vector3(0f, 3f, 0f);
            var body = host.AddComponent<Rigidbody2D>();
            var collider = host.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2(0f, 0.5f);
            var enemy = host.AddComponent<EnemyController>();
            enemy.bodyCollider = collider;
            EnemyLifecycleTestUtility.InitializeComponents(enemy);
            SetField(enemy, "useSpawnPresentation", true);
            Physics2D.SyncTransforms();
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            Assert.That(body.simulated, Is.True);
            Assert.That(TickSpawn(enemy, 0.1f), Is.False);
            Assert.That(enemy.TakeResolvedHit(new HitReactionData { damage = 10f }, Vector2.zero), Is.Zero);
            body.position = Vector2.zero;
            Physics2D.SyncTransforms();
            Assert.That(TickSpawn(enemy, 0.2f), Is.True, "Landing unlocks without a landing clip.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(wall);
        }
    }

    [Test]
    public void DeathExcludesPlayerOnAllCollidersButPreservesGround()
    {
        var host = new GameObject("Corpse");
        try
        {
            var body = host.AddComponent<BoxCollider2D>();
            var child = new GameObject("ExtraBody");
            child.transform.SetParent(host.transform);
            var extra = child.AddComponent<CircleCollider2D>();
            var enemy = EnemyLifecycleTestUtility.AddActive(host);
            enemy.Die();
            foreach (var collider in new Collider2D[] { body, extra })
            {
                Assert.That(collider.enabled, Is.True);
                Assert.That(collider.excludeLayers.value & LayerMask.GetMask("Player"), Is.Not.Zero);
                Assert.That(collider.excludeLayers.value & LayerMask.GetMask("Ground"), Is.Zero);
            }
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void SpawnRejectsDamageAndDoesNotConsumeHitActivation()
    {
        var host = new GameObject("SpawnEnemy");
        var hitboxObject = new GameObject("Hitbox");
        try
        {
            var enemy = host.AddComponent<EnemyController>();
            var hitbox = hitboxObject.AddComponent<HitBoxDamage>();
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            float hp = enemy.currentHP;
            Assert.That(enemy.IsAlive, Is.True);
            Assert.That(enemy.IsTargetable, Is.False);
            Assert.That(enemy.TakeResolvedHit(new HitReactionData { damage = 10f }, Vector2.zero), Is.Zero);
            Assert.That(enemy.currentHP, Is.EqualTo(hp));
            hitbox.ProcessHit(enemy);
            var hits = (HashSet<EnemyController>)typeof(HitBoxDamage)
                .GetField("hitEnemiesThisActivation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hitbox);
            Assert.That(hits, Is.Empty);
            EnemyLifecycleTestUtility.Activate(enemy);
            Assert.That(enemy.IsTargetable, Is.True);
            Assert.That(enemy.TakeResolvedHit(new HitReactionData { damage = 10f }, Vector2.zero), Is.EqualTo(10f));
        }
        finally
        {
            Object.DestroyImmediate(hitboxObject);
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void SpawnWaitsPastEntryFrameAndRestoresPhysics()
    {
        var host = new GameObject("SpawnPhysics");
        try
        {
            var body = host.AddComponent<Rigidbody2D>();
            var enemy = host.AddComponent<EnemyController>();
            EnemyLifecycleTestUtility.InitializeComponents(enemy);
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            Assert.That(body.simulated, Is.False);
            typeof(EnemyController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, null);
            Assert.That(enemy.LifecycleState, Is.EqualTo(EnemyLifecycleStateId.Spawn));
            EnemyLifecycleTestUtility.Activate(enemy);
            Assert.That(body.simulated, Is.True);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void DeathBeforeStartIsOnceOnlyAndCannotBeReinitialized()
    {
        var host = new GameObject("EarlyDeath");
        try
        {
            var enemy = host.AddComponent<EnemyController>();
            int deaths = 0;
            enemy.OnDied += () => deaths++;
            enemy.Die();
            enemy.Die();
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            enemy.InitializeSpawn(Vector3.one, Quaternion.identity);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(enemy.IsTargetable, Is.False);
            Assert.That(enemy.LifecycleState, Is.EqualTo(EnemyLifecycleStateId.Die));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void DisabledActiveEnemyIsNotTargetable()
    {
        var host = new GameObject("DisabledEnemy");
        try
        {
            var enemy = EnemyLifecycleTestUtility.AddActive(host);
            Assert.That(enemy.IsTargetable, Is.True);
            enemy.enabled = false;
            Assert.That(enemy.IsAlive, Is.True);
            Assert.That(enemy.IsTargetable, Is.False);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void RegistryKeepsSpawnForWaveCountingButNotForTargeting()
    {
        var host = new GameObject("RegistrySpawn");
        try
        {
            var enemy = host.AddComponent<EnemyController>();
            RoomEnemyRegistry.Register(enemy);
            var found = new List<EnemyController>();
            RoomEnemyRegistry.FillLiving(found);
            Assert.That(found, Has.Member(enemy));
            RoomEnemyRegistry.FillTargetable(found);
            Assert.That(found, Has.No.Member(enemy));
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            EnemyLifecycleTestUtility.Activate(enemy);
            RoomEnemyRegistry.FillTargetable(found);
            Assert.That(found, Has.Member(enemy));
        }
        finally
        {
            RoomEnemyRegistry.Unregister(host.GetComponent<EnemyController>());
            Object.DestroyImmediate(host);
        }
    }
}
#endif
