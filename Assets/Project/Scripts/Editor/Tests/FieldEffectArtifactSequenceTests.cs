#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using UnityEngine;

public sealed class FieldEffectArtifactSequenceTests
{
    private GameObject _host;
    private GameObject _enemyHost;
    private TeamLoader _team;
    private ArtifactPassiveRuntimeSystem _passives;
    private PlayerRunInventory _inventory;
    private CharacterObject _wearer;
    private CharacterObject _ally;
    private CharacterItem _allyItem;
    private GameObject _playerHost;
    private TeamLoader _previousTeam;
    private PlayerCharacter _previousPlayer;

    [SetUp]
    public void SetUp()
    {
        _previousPlayer = PlayerCharacter.Instance;
        _host = new GameObject("FieldArtifactSequenceTest");
        _team = _host.AddComponent<TeamLoader>();
        _previousTeam = TeamLoader.Instance;
        typeof(TeamLoader).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, _team);
        _passives = _host.AddComponent<ArtifactPassiveRuntimeSystem>();
        _inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        _team.SetRunInventory(_inventory);
        CharacterItem mainItem = Resources.Load<CharacterItem>("ItemSO/char_0_Main");
        _wearer = mainItem.characterObjectData;
        _ally = Character("field-ally");
        _allyItem = ScriptableObject.CreateInstance<CharacterItem>();
        _allyItem.id = "field-ally-item";
        _allyItem.characterObjectData = _ally;
        _inventory.AddItem(mainItem);
        _inventory.AddItem(_allyItem);
    }

    [TearDown]
    public void TearDown()
    {
        if (_enemyHost != null)
        {
            RoomEnemyRegistry.Unregister(_enemyHost.GetComponent<EnemyController>());
            Object.DestroyImmediate(_enemyHost);
        }
        if (_playerHost != null) Object.DestroyImmediate(_playerHost);
        typeof(PlayerCharacter).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, _previousPlayer);
        Object.DestroyImmediate(_host);
        typeof(TeamLoader).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, _previousTeam);
        Object.DestroyImmediate(_inventory);
        Object.DestroyImmediate(_ally);
        Object.DestroyImmediate(_allyItem);
    }

    [Test]
    public void PersistentRecipeIconFollowsActivePlayerAndExpiresWhileOwnerIsOffField()
    {
        _inventory.DeployTeam(new[] { _wearer, _ally }, 0);
        _playerHost = new GameObject("Persistent icon recipient");
        PlayerCharacter player = _playerHost.AddComponent<PlayerCharacter>();
        player.characterObject = _wearer;
        player.SetCurrentHP(player.MaxHP);
        RuntimeEffectReceiver receiver = _playerHost.AddComponent<RuntimeEffectReceiver>();
        player.RuntimeEffects = receiver;
        typeof(RuntimeEffectReceiver).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(receiver, null);
        var authored = _wearer.kitDefinition.GetNodeById("main.skill.lv4").runtimeEffects
            .Single(effect => effect.recipe?.recipeId == "main.memory.chain_summon").recipe;
        // Isolate presentation from projectile services; retain the saved icon and ownership policy.
        var recipe = JsonUtility.FromJson<RuntimeEffectRecipe>(JsonUtility.ToJson(authored));
        recipe.definition = UnityEditor.AssetDatabase.LoadAssetAtPath<AdditionalDamageOnHitRuntimeEffectDefinition>(
            "Assets/Project/Character/Hero/RuntimeEffect/RuntimeEffect_AdditionalDamage.asset");
        _inventory.ApplyRuntimeEffect(_wearer, recipe, _wearer, _wearer.stats);
        var view = _playerHost.GetComponentInChildren<FieldEffectVisual>(true);
        Assert.That(view, Is.Not.Null);
        Assert.That(view.style, Is.EqualTo(FieldEffectVisual.VisualStyle.FloatingIcon));
        _inventory.SetActiveTeamSlotIndex(1);
        player.SetCharacter(_ally, 1000f);
        Assert.That(view.IsFinished, Is.False);
        RebuildHud(player);
        Assert.That(view.Phase, Is.Not.EqualTo(FieldEffectVisual.AnimationPhase.Ending),
            "HUD refreshes for the incoming character must not remove a team-visible effect.");
        typeof(FieldEffectVisual).GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, new object[] { .2f });
        Vector3 previous = view.transform.position;
        _playerHost.transform.position += Vector3.right * 4f;
        typeof(FieldEffectVisual).GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, new object[] { .2f });
        Assert.That(view.transform.position.x - previous.x, Is.EqualTo(4f).Within(.01f));
        Assert.That(_inventory.GetActiveRuntimeEffects(_ally), Is.Empty, "The visual must not transfer the source effect to the ally.");
        _inventory.TickRuntimeEffects(15.1f);
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Ending));
        typeof(FieldEffectVisual).GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, new object[] { .5f });
        Assert.That(view.IsFinished, Is.True);
    }

    private void RebuildHud(PlayerCharacter player)
    {
        var root = new GameObject("Runtime effect HUD regression", typeof(RectTransform));
        root.SetActive(false);
        var hud = root.AddComponent<PlayerHUD>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(PlayerHUD).GetField("playerCharacter", flags).SetValue(hud, player);
        typeof(PlayerHUD).GetField("teamLoader", flags).SetValue(hud, _team);
        typeof(PlayerHUD).GetField("runtimeEffectContainer", flags).SetValue(hud, root.transform);
        try
        {
            for (int refresh = 0; refresh < 3; refresh++)
                typeof(PlayerHUD).GetMethod("RebuildRuntimeEffects", flags).Invoke(hud, null);
        }
        finally
        {
            typeof(PlayerHUD).GetMethod("OnDisable", flags).Invoke(hud, null);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PlayerFieldVisualsFollowInventoryAndSwapsWithoutAStatusHud()
    {
        Equip("P1_01_BlindsBoundlessRibbon");
        _playerHost = new GameObject("PlayerWithoutHUD");
        PlayerCharacter player = _playerHost.AddComponent<PlayerCharacter>();
        player.characterObject = _wearer;
        player.SetCurrentHP(player.MaxHP);
        RuntimeEffectReceiver receiver = _playerHost.AddComponent<RuntimeEffectReceiver>();
        player.RuntimeEffects = receiver;
        typeof(RuntimeEffectReceiver).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(receiver, null);
        CombatEventHub.Shared.Publish(new ActionStartedEvent(_wearer, AttackCategory.Ultimate,
            "hudless-ult", "Ultimate", null));
        FieldEffectVisual existing = _playerHost.GetComponentInChildren<FieldEffectVisual>(true);
        Assert.That(existing, Is.Not.Null, "The saved Ribbon currently targets all allies, including its wearer.");
        RuntimeEffectInstance wearerToxic = Effect(_wearer, "P1-A01-TOXIC");
        _inventory.RemoveRuntimeEffect(_wearer, wearerToxic.definition, wearerToxic.recipe as RuntimeEffectRecipe);
        typeof(FieldEffectVisual).GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(existing, new object[] { 0.5f });
        Assert.That(existing.IsFinished, Is.True);

        player.SetCharacter(_ally, 1000f);
        FieldEffectVisual view = _playerHost.GetComponentInChildren<FieldEffectVisual>(true);
        Assert.That(view, Is.Not.Null);
        Assert.That(view.style, Is.EqualTo(FieldEffectVisual.VisualStyle.DarkFlame));
        Assert.That(view.gameObject.activeSelf, Is.True);
        player.SetCharacter(_wearer, player.MaxHP);
        Assert.That(view.IsFinished, Is.True, "Toxic must not leave an ending flame on a different recipient.");
        Assert.That(Effect(_ally, "P1-A01-TOXIC"), Is.Not.Null, "Swapping must not erase off-field Toxic.");
        player.SetCharacter(_ally, 1000f);
        Assert.That(view.IsFinished, Is.False);
        _inventory.TickRuntimeEffects(10.1f);
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Ending));
    }

    [Test]
    public void RibbonUltimateGivesAuthoredAlliesDarkFlamesForTheExistingDuration()
    {
        Equip("P1_01_BlindsBoundlessRibbon");
        CombatEventHub.Shared.Publish(new ActionStartedEvent(_wearer, AttackCategory.Ultimate,
            "field-ult", "Ultimate", null));
        RuntimeEffectInstance toxic = Effect(_ally, "P1-A01-TOXIC");
        Assert.That(toxic, Is.Not.Null);
        Assert.That(Effect(_wearer, "P1-A01-TOXIC"), Is.Not.Null);
        Assert.That(toxic.remainingSeconds, Is.EqualTo(10f));
        AssertStyle(toxic, FieldEffectVisual.VisualStyle.DarkFlame);
        Assert.That(ArtifactPassiveRuntimeSystem.ModifyOutgoingDescriptor(_ally,
            new DamageDescriptor(DamageDelivery.Primary, ElementType.None)).elementType,
            Is.EqualTo(ElementType.Darkness));
        _inventory.TickRuntimeEffects(10.1f);
        Assert.That(Effect(_ally, "P1-A01-TOXIC"), Is.Null);
    }

    [Test]
    public void ShroudSwitchOutGivesPowerUpToTheIncomingCharacter()
    {
        Equip("P1_07_ShroudOfDeprivation");
        CombatEventHub.Shared.Publish(new CharacterSwitchCompletedEvent(_wearer, _ally, 0, 1, Vector2.zero));
        RuntimeEffectInstance support = Effect(_ally, "P1-A07-SUPPORT");
        Assert.That(support, Is.Not.Null);
        Assert.That(Effect(_wearer, "P1-A07-SUPPORT"), Is.Null);
        Assert.That(support.remainingSeconds, Is.EqualTo(10f));
        AssertStyle(support, FieldEffectVisual.VisualStyle.PowerUp);
    }

    [Test]
    public void ShardStillAppliesIndefiniteAfflictionWithoutAContinuousFieldVisual()
    {
        Equip("P1_05_AuraResonatingShard");
        EnemyController enemy = Enemy();
        CombatEventHub.Shared.Publish(new CharacterSwitchCompletedEvent(_ally, _wearer, 1, 0, Vector2.zero));
        RuntimeEffectInstance affliction = enemy.RuntimeEffects.ActiveEffects.FirstOrDefault(
            effect => effect.recipe?.recipeId == "P1-A05-AFFLICTION");
        Assert.That(affliction, Is.Not.Null);
        Assert.That(affliction.UsesTimer, Is.False);
        AssertStyle(affliction, FieldEffectVisual.VisualStyle.PowerDown);
        Assert.That(enemy.GetComponentsInChildren<FieldEffectVisual>(true).Length, Is.Zero);
    }

    [Test]
    public void MaskDamagingParticlePulsesGivePowerUpToAlliesWithOneVisualAsStacksGrow()
    {
        ArtifactInstance artifact = Equip("P1_02_FiendFleshMask");
        EnemyController enemy = Enemy();
        DamageResolvedEvent pulse = new DamageResolvedEvent(
            new DamageProvenance(1, 10, 10, 1, artifact.instanceId, CombatDamageTag.Artifact | CombatDamageTag.Particle),
            new DamageDescriptor(DamageDelivery.Additional, ElementType.None), DamageProcPolicy.TriggerArtifactRules,
            _wearer, null, enemy, AttackCategory.Normal, "field-pulse", 100f, 100f, false, false, Vector2.zero);
        CombatEventHub.Shared.Publish(pulse);
        RuntimeEffectInstance first = Effect(_wearer, "P1-A02-ATK");
        RuntimeEffectInstance second = Effect(_ally, "P1-A02-ATK");
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        AssertStyle(first, FieldEffectVisual.VisualStyle.PowerUp);
        AssertStyle(second, FieldEffectVisual.VisualStyle.PowerUp);
        Assert.That(second.remainingSeconds, Is.EqualTo(4f), "Preserve the saved recipe duration, rather than the generator's old value.");
        _playerHost = new GameObject("MaskRecipientPresentation");
        RuntimeEffectReceiver receiver = _playerHost.AddComponent<RuntimeEffectReceiver>();
        receiver.SyncVisuals(_inventory.GetActiveRuntimeEffects(_ally));
        FieldEffectVisual view = _playerHost.GetComponentInChildren<FieldEffectVisual>(true);
        typeof(FieldEffectVisual).GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, new object[] { 0.5f });
        CombatEventHub.Shared.Publish(pulse);
        Assert.That(second.stackCount, Is.EqualTo(2));
        receiver.SyncVisuals(_inventory.GetActiveRuntimeEffects(_ally));
        Assert.That(_playerHost.GetComponentsInChildren<FieldEffectVisual>(true).Length, Is.EqualTo(1));
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Loop));
    }

    [Test]
    public void ChronoMarksThenConsumesIntoBloodBurstAndTheExistingSevenSecondClaw()
    {
        Equip("P1_04_BrokenChronoRelic");
        EnemyController enemy = Enemy();
        enemy.transform.position = new Vector3(2000f, 2000f, 0f);
        Physics2D.SyncTransforms();
        enemy.currentHP = enemy.maxHP = 10000f;
        DamageResolvedEvent skill = Hit(enemy, AttackCategory.Skill, 101, "chrono-skill");
        CombatEventHub.Shared.Publish(skill);
        RuntimeEffectInstance mark = enemy.RuntimeEffects.ActiveEffects.First(effect => effect.recipe?.recipeId == "P1-A04-MARK-INJURY");
        Assert.That(mark.remainingSeconds, Is.EqualTo(10f));
        Assert.That(RuntimeEffectRuntimeUtility.GetKind(mark), Is.EqualTo(RuntimeEffectKind.Debuff));
        FieldEffectVisual glyph = enemy.GetComponentInChildren<FieldEffectVisual>(true);
        Assert.That(glyph.style, Is.EqualTo(FieldEffectVisual.VisualStyle.Mark));
        CombatEventHub.Shared.Publish(Hit(enemy, AttackCategory.Normal, 102, "chrono-follow-up"));
        Assert.That(enemy.RuntimeEffects.ActiveEffects.Any(effect => effect.recipe?.recipeId == "P1-A04-MARK-INJURY"), Is.False);
        Assert.That(glyph.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Ending));
        ArtifactAreaVisual blood = _host.GetComponentInChildren<ArtifactAreaVisual>(true);
        Assert.That(blood.style, Is.EqualTo(ArtifactAreaVisual.VisualStyle.BloodExplosion));
        ExplosionSplashDefinition definition = Resources.Load<ExplosionSplashDefinition>("ItemSO/Artifacts/RaritySSR/P1_A04_MarkExplosion");
        Assert.That(blood.HitRadius, Is.EqualTo(definition.radius));
        Assert.That(blood.transform.position, Is.EqualTo((Vector3)skill.hitPosition));
        RuntimeEffectInstance claw = Effect(_wearer, "P1-A04-CLAW");
        Assert.That(claw, Is.Not.Null);
        Assert.That(claw.remainingSeconds, Is.EqualTo(7f));
        AssertStyle(claw, FieldEffectVisual.VisualStyle.Claw);
    }

    [Test]
    public void ChronoMarkExpiryEndsItsGlyphWithoutGivingClawOrBlood()
    {
        Equip("P1_04_BrokenChronoRelic");
        EnemyController enemy = Enemy();
        CombatEventHub.Shared.Publish(Hit(enemy, AttackCategory.Skill, 201, "chrono-expiring"));
        FieldEffectVisual glyph = enemy.GetComponentInChildren<FieldEffectVisual>(true);
        typeof(RuntimeEffectReceiver).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(enemy.RuntimeEffects, new object[] { 10.1f });
        Assert.That(enemy.RuntimeEffects.ActiveEffects.Any(effect => effect.recipe?.recipeId == "P1-A04-MARK-INJURY"), Is.False);
        Assert.That(glyph.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Ending));
        Assert.That(Effect(_wearer, "P1-A04-CLAW"), Is.Null);
        Assert.That(_host.GetComponentsInChildren<ArtifactAreaVisual>(true), Is.Empty);
    }

    [Test]
    public void ScalesEmptyPulseDoesNotAnimateAnAttackButConfirmedDamageDoes()
    {
        Equip("P1_03_SoulMerchantsScales");
        ParticleBehaviorDefinition shieldDefinition = Resources.Load<ParticleBehaviorDefinition>("ItemSO/Artifacts/Phase1/P1_A03_ThroneBarrier");
        ParticleBehaviorRuntime runtime = new ParticleBehaviorRuntime();
        try
        {
            runtime.Initialize(_passives.ActiveRuntimes[0], shieldDefinition, null, _team);
            ArtifactAreaVisual shield = runtime.Visualizer.GetComponent<ArtifactAreaVisual>();
            typeof(ParticleBehaviorRuntime).GetMethod("Pulse", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runtime, new object[] { new Vector2(2000f, 2000f) });
            Assert.That(shield.AttackCount, Is.Zero);
            EnemyController enemy = Enemy();
            _enemyHost.AddComponent<BoxCollider2D>().size = Vector2.one * 0.2f;
            _enemyHost.transform.position = new Vector3(2000f, 2000f, 0f);
            enemy.currentHP = enemy.maxHP = 10000f;
            Physics2D.SyncTransforms();
            float before = enemy.currentHP;
            typeof(ParticleBehaviorRuntime).GetMethod("Pulse", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runtime, new object[] { new Vector2(2000f, 2000f) });
            Assert.That(enemy.currentHP, Is.LessThan(before));
            Assert.That(shield.AttackCount, Is.EqualTo(1));
            Assert.That(shieldDefinition.volume.centerOffset, Is.EqualTo(Vector2.zero));
            Assert.That(shield.HitRadius, Is.EqualTo(2f));
            Assert.That(shield.HitRadii, Is.EqualTo(new Vector2(2f, 2f)));
            Vector2 center = new Vector2(2000f, 2000f);
            _enemyHost.transform.position = new Vector3(2000f, 2001.8f, 0f);
            Physics2D.SyncTransforms();
            before = enemy.currentHP;
            typeof(ParticleBehaviorRuntime).GetMethod("Pulse", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runtime, new object[] { center });
            Assert.That(enemy.currentHP, Is.LessThan(before), "The circular area must damage a target inside its upper edge.");
            Assert.That(shield.AttackCount, Is.EqualTo(2));
            _enemyHost.transform.position = new Vector3(2001.6f, 2001.6f, 0f);
            Physics2D.SyncTransforms();
            before = enemy.currentHP;
            typeof(ParticleBehaviorRuntime).GetMethod("Pulse", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runtime, new object[] { center });
            Assert.That(enemy.currentHP, Is.EqualTo(before), "A broad-phase box corner must not count as a circular hit.");
            Assert.That(shield.AttackCount, Is.EqualTo(2));
            runtime.Reset();
            Assert.That(shield.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Ending));
            runtime.Initialize(_passives.ActiveRuntimes[0], shieldDefinition, null, _team);
            Assert.That(runtime.Visualizer.GetComponent<ArtifactAreaVisual>(), Is.SameAs(shield));
            Assert.That(shield.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Enter));
        }
        finally { runtime.Reset(true); runtime.CleanupVisualizer(); }
    }

    [Test]
    public void ChronoExplosionDamagesCircularAreaWhileRejectingCorners()
    {
        Equip("P1_04_BrokenChronoRelic");
        EnemyController original = Enemy();
        _enemyHost.transform.position = new Vector3(2000f, 2000f, 0f);
        original.currentHP = original.maxHP = 10000f;
        EnemyController insideCircle = AreaEnemy(new Vector2(2001f, 2001f));
        EnemyController corner = AreaEnemy(new Vector2(2001.6f, 2001.6f));
        Physics2D.SyncTransforms();
        CombatEventHub.Shared.Publish(Hit(original, AttackCategory.Skill, 301, "tall-mark"));
        CombatEventHub.Shared.Publish(Hit(original, AttackCategory.Normal, 302, "tall-consume"));
        Assert.That(insideCircle.currentHP, Is.LessThan(10000f));
        Assert.That(corner.currentHP, Is.EqualTo(10000f));
        ArtifactAreaVisual blood = _host.GetComponentInChildren<ArtifactAreaVisual>(true);
        Assert.That(blood.HitRadii, Is.EqualTo(new Vector2(2f, 2f)));
        Assert.That(Resources.Load<ExplosionSplashDefinition>("ItemSO/Artifacts/RaritySSR/P1_A04_MarkExplosion").centerOffset,
            Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void DaggerSavedProcCreatesGroundThunderAndItsExactRadius()
    {
        Equip("P1_09_ShinsRustyDagger");
        EnemyController original = Enemy();
        original.transform.position = new Vector3(2000f, 2002f, 0f);
        original.currentHP = original.maxHP = 10000f;
        EnemyController inside = AreaEnemy(new Vector2(2003f, 2000f));
        EnemyController outside = AreaEnemy(new Vector2(2004f, 2000f));
        GameObject ground = new GameObject("ThunderSequenceGround");
        ground.transform.SetParent(_host.transform);
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.transform.position = new Vector3(2000f, 1999.5f, 0f);
        ground.AddComponent<BoxCollider2D>().size = new Vector2(20f, 1f);
        Physics2D.SyncTransforms();
        for (ulong i = 0; i < 5; i++)
            CombatEventHub.Shared.Publish(Hit(original, AttackCategory.Normal, 400 + i, "dagger-focus", true,
                new Vector2(2000f, 2002f)));
        // The existing runtime evaluates this armed rule immediately at its saved threshold.
        Assert.That(_passives.ActiveRuntimes[0].Rules[0].armed, Is.False);
        ParticleBehaviorPool pool = _host.GetComponent<ParticleBehaviorPool>();
        var active = (System.Collections.Generic.List<ParticleBehaviorRuntime>)typeof(ParticleBehaviorPool)
            .GetField("_active", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
        Assert.That(active.Count, Is.EqualTo(1));
        active[0].Tick(Time.time);
        Assert.That(active[0].AreaCenter, Is.EqualTo(new Vector2(2000f, 2000f)));
        Assert.That(inside.currentHP, Is.LessThan(10000f));
        Assert.That(outside.currentHP, Is.EqualTo(10000f));
        ArtifactAreaVisual thunder = active[0].Visualizer.GetComponent<ArtifactAreaVisual>();
        Assert.That(thunder.style, Is.EqualTo(ArtifactAreaVisual.VisualStyle.ThunderStrike));
        Assert.That(thunder.HitRadii, Is.EqualTo(Vector2.one * 3.5f));
        Assert.That(thunder.AttackCount, Is.EqualTo(1));
        ground.transform.position += Vector3.up;
        Physics2D.SyncTransforms();
        active[0].Tick(Time.time + 0.2f);
        Assert.That(active[0].AreaCenter, Is.EqualTo(new Vector2(2000f, 2000f)), "An emitted world strike must keep its original impact center.");
        Assert.That(thunder.AttackCount, Is.EqualTo(1));
        Assert.That(_passives.ActiveRuntimes[0].Rules[0].armed, Is.False);
        CombatEventHub.Shared.Publish(Hit(original, AttackCategory.Normal, 405, "dagger-follow-up"));
        Assert.That(active.Count, Is.EqualTo(1), "A following hit must not duplicate the consumed thunder proc.");
        Assert.That(_host.GetComponentsInChildren<FieldEffectVisual>(true), Is.Empty,
            "Permanent Focus must not gain a sustained buff visual.");
    }

    [Test]
    public void ScalesDamageAndExpansionStartAtThePlayerColliderCenter()
    {
        Equip("P1_03_SoulMerchantsScales");
        _playerHost = new GameObject("OffsetPlayerCollider");
        BoxCollider2D body = _playerHost.AddComponent<BoxCollider2D>();
        body.offset = new Vector2(0.2f, 0.75f);
        body.size = new Vector2(1f, 2f);
        _playerHost.transform.position = new Vector3(5000f, 5000f, 0f);
        PlayerCharacter player = _playerHost.AddComponent<PlayerCharacter>();
        typeof(PlayerCharacter).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, player);
        player.characterObject = _wearer;
        player.SetCurrentHP(player.MaxHP);
        EnemyController victim = AreaEnemy(new Vector2(5000.2f, 5002.5f));
        Physics2D.SyncTransforms();
        ParticleBehaviorDefinition definition = Resources.Load<ParticleBehaviorDefinition>("ItemSO/Artifacts/Phase1/P1_A03_ThroneBarrier");
        var runtime = new ParticleBehaviorRuntime();
        try
        {
            runtime.Initialize(_passives.ActiveRuntimes[0], definition, null, _team);
            Assert.That(definition.volume.anchorToGround, Is.False);
            Vector2 center = body.bounds.center;
            Assert.That(runtime.AreaCenter, Is.EqualTo(center));
            float before = victim.currentHP;
            runtime.Tick(Time.time);
            ArtifactAreaVisual view = runtime.Visualizer.GetComponent<ArtifactAreaVisual>();
            Assert.That(victim.currentHP, Is.LessThan(before));
            Assert.That(view.AttackCount, Is.EqualTo(1));
            Assert.That((Vector2)view.attackRing.transform.position, Is.EqualTo(center));
            Assert.That((Vector2)view.shell.transform.position, Is.EqualTo(center));
            _playerHost.transform.position += Vector3.right;
            Physics2D.SyncTransforms();
            runtime.Tick(Time.time + 0.5f);
            Assert.That(runtime.AreaCenter, Is.EqualTo((Vector2)body.bounds.center));
        }
        finally { runtime.Reset(true); runtime.CleanupVisualizer(); }
    }

    [Test]
    public void CrestSkillProcFollowsEnemyDealsSavedPulsesAndEndsItsFlames()
    {
        Equip("P1_06_RivalsBrokenCrest");
        EnemyController victim = AreaEnemy(new Vector2(6000f, 6000f));
        victim.bodyCollider.offset = new Vector2(0f, 0.7f);
        EnemyController nearby = AreaEnemy(new Vector2(6002.7f, 6000.7f));
        EnemyController outside = AreaEnemy(new Vector2(6003.4f, 6000.7f));
        Physics2D.SyncTransforms();
        CombatEventHub.Shared.Publish(Hit(victim, AttackCategory.Skill, 801, "crest-skill"));
        var pool = _host.GetComponent<ParticleBehaviorPool>();
        var active = (System.Collections.Generic.List<ParticleBehaviorRuntime>)typeof(ParticleBehaviorPool)
            .GetField("_active", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
        Assert.That(active.Count, Is.EqualTo(1));
        ParticleBehaviorRuntime runtime = active[0];
        ArtifactAreaVisual view = runtime.Visualizer.GetComponent<ArtifactAreaVisual>();
        Assert.That(view.style, Is.EqualTo(ArtifactAreaVisual.VisualStyle.DarkFlameMark));
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Enter));
        float start = Time.time;
        float before = victim.currentHP;
        runtime.Tick(start);
        Assert.That(victim.currentHP, Is.LessThan(before));
        Assert.That(nearby.currentHP, Is.LessThan(10000f));
        Assert.That(outside.currentHP, Is.EqualTo(10000f));
        Assert.That(view.AttackCount, Is.EqualTo(1));
        Assert.That((Vector2)view.attackRing.transform.position, Is.EqualTo((Vector2)victim.bodyCollider.bounds.center));
        Assert.That(view.motes.particleCount, Is.InRange(1, 12));
        before = victim.currentHP;
        runtime.Tick(start + 1.9f);
        Assert.That(victim.currentHP, Is.EqualTo(before));
        victim.transform.position += Vector3.up;
        Physics2D.SyncTransforms();
        runtime.Tick(start + 2.01f);
        Assert.That(victim.currentHP, Is.LessThan(before));
        Assert.That(runtime.AreaCenter, Is.EqualTo((Vector2)victim.bodyCollider.bounds.center));
        Assert.That(view.AttackCount, Is.EqualTo(2));
        CombatEventHub.Shared.Publish(Hit(victim, AttackCategory.Skill, 802, "crest-cooldown"));
        Assert.That(active.Count, Is.EqualTo(1));
        Assert.That(runtime.Tick(start + 5.01f), Is.False);
        runtime.Reset();
        Assert.That(view.Phase, Is.EqualTo(FieldEffectVisual.AnimationPhase.Ending));
        typeof(ArtifactAreaVisual).GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, new object[] { view.exitSeconds + 0.01f });
        Assert.That(view.IsFinished, Is.True);
        Assert.That(view.motes.particleCount, Is.Zero);
    }

    private EnemyController AreaEnemy(Vector2 position)
    {
        GameObject host = new GameObject("AreaSequenceEnemy");
        host.transform.SetParent(_host.transform);
        host.transform.position = position;
        host.AddComponent<BoxCollider2D>().size = Vector2.one * 0.2f;
        EnemyController enemy = EnemyLifecycleTestUtility.AddActive(host);
        enemy.currentHP = enemy.maxHP = 10000f;
        return enemy;
    }

    private DamageResolvedEvent Hit(EnemyController enemy, AttackCategory category, ulong id, string action, bool critical = false, Vector2? position = null) =>
        new DamageResolvedEvent(new DamageProvenance(id, id, id, 0, null, CombatDamageTag.None),
            new DamageDescriptor(DamageDelivery.Primary, ElementType.None), DamageProcPolicy.TriggerArtifactRules,
            _wearer, null, enemy, category, action, 100f, 100f, critical, false, position ?? new Vector2(2000f, 2000f));

    private ArtifactInstance Equip(string name)
    {
        ArtifactDefinition definition = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/RaritySSR/" + name);
        Assert.That(_inventory.TryAcquireArtifact(definition, out ArtifactInstance artifact, 1), Is.True);
        _inventory.DeployTeam(new[] { _wearer, _ally }, 0);
        _inventory.SetEquippedArtifactRefsAt(0, new[] { new ArtifactInstanceRef(artifact.instanceId) });
        _team.RefreshRuntimeState();
        _passives.Initialize(_team);
        Assert.That(_passives.ActiveRuntimes.Count, Is.EqualTo(1));
        return artifact;
    }

    private EnemyController Enemy()
    {
        _enemyHost = new GameObject("FieldSequenceEnemy");
        EnemyController enemy = EnemyLifecycleTestUtility.AddActive(_enemyHost);
        RoomEnemyRegistry.Register(enemy);
        return enemy;
    }

    private RuntimeEffectInstance Effect(CharacterObject character, string id) =>
        _inventory.GetActiveRuntimeEffects(character).FirstOrDefault(effect => effect.recipe?.recipeId == id);

    private static CharacterObject Character(string id)
    {
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        character.characterId = id;
        character.stats = new CharacterStats { maxHP = 1000f, attack = 100f, defense = 100f };
        return character;
    }

    private static void AssertStyle(RuntimeEffectInstance effect, FieldEffectVisual.VisualStyle style) =>
        Assert.That(effect.recipe.presentation.fieldVisualPrefab.GetComponent<FieldEffectVisual>().style, Is.EqualTo(style));
}
#endif
