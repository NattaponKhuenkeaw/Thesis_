#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.AIBehaviour.Boss;
using Project.Core.ZoneState;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;
using Project.Training;
using Project.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class Boss112RunIntegrationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void ProductionAssetsUseTheCombatShellAndAnAutomaticVariantNotTheTrainingScene()
    {
        var room = Resources.Load<RoomData>("MapSO/RoomData_Boss_02");
        var first = Resources.Load<RoomData>("MapSO/RoomData_Boss_01");
        Assert.That(Resources.Load<MapSO>("MapSO/Test_MapSO").GetRoomById("Boss_02"), Is.SameAs(room));
        Assert.That(room.RoomKind, Is.EqualTo(RoomKind.Boss));
        Assert.That(room.IncludeInRandomOffers, Is.False);
        Assert.That(room.SceneName, Is.EqualTo(first.SceneName));
        Assert.That(room.BackgroundMusic, Is.EqualTo(first.BackgroundMusic));
        Assert.That(room.Ambience, Is.EqualTo(first.Ambience));
        Assert.That(EditorBuildSettings.scenes.Any(s => s.path == Boss112PrototypeAuthoring.ScenePath), Is.False);
        Assert.That(room.LayoutPrefab.GetComponentInChildren<Boss112TestRoom>(true), Is.Null);
        Assert.That(room.LayoutPrefab.GetComponentInChildren<TrainingSession>(true), Is.Null);
        Assert.That(room.LayoutPrefab.GetComponentInChildren<EntrancePoint>(true).EntranceId, Is.EqualTo("Default"));
        Assert.That(room.LayoutPrefab.GetComponent<Boss112Arena>(), Is.Not.Null);
        Assert.That(room.LayoutPrefab.GetComponentsInChildren<CombatStageController>(true), Is.Empty);
        var waves = room.Encounter.Waves;
        Assert.That(waves.Count, Is.EqualTo(1));
        Assert.That(waves[0].enemies.Count, Is.EqualTo(1));
        var boss = waves[0].enemies[0].enemyPrefab.GetComponent<Boss112Controller>();
        var executioner = first.Encounter.Waves[0].enemies[0].enemyPrefab.GetComponent<BossPatternController>();
        using var waveData = new SerializedObject(executioner);
        var worm = boss.GetComponent<Boss112WormBody>();
        Assert.That(worm, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(boss.gameObject), Is.EqualTo(Boss112RunAuthoring.BossPath));
        using (var wormData = new SerializedObject(worm))
        {
            var pieces = wormData.FindProperty("segments");
            Assert.That(pieces.arraySize, Is.EqualTo(5));
            for (int i = 0; i < pieces.arraySize; i++)
            {
                var piece = (SpriteRenderer)pieces.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(piece.GetComponent<CircleCollider2D>().isTrigger, Is.True);
            }
        }
        Assert.That(boss.AutoAttack, Is.True);
        Assert.That(boss.Enemy.HasBossDriver, Is.True);
        using var spawnData = new SerializedObject(boss.Enemy);
        Assert.That(spawnData.FindProperty("useSpawnPresentation").boolValue, Is.False);
        Assert.That(spawnData.FindProperty("snapSpawnToGround").boolValue, Is.False);
        using var data = new SerializedObject(boss);
        Assert.That(data.FindProperty("shockwavePrefab").objectReferenceValue,
            Is.SameAs(waveData.FindProperty("groundWavePrefab").objectReferenceValue));
        Assert.That(data.FindProperty("shockwaveSpeed").floatValue,
            Is.EqualTo(waveData.FindProperty("groundWaveSpeed").floatValue));
        Assert.That(data.FindProperty("useRunBossFlow").boolValue, Is.True);
        Assert.That(data.FindProperty("spawnPanDuration").floatValue, Is.EqualTo(.5f));
        Assert.That(data.FindProperty("spawnRevealDuration").floatValue, Is.EqualTo(1.2f));
        Assert.That(data.FindProperty("spawnReturnDuration").floatValue, Is.EqualTo(.6f));
        var testBoss = AssetDatabase.LoadAssetAtPath<Boss112Controller>(Boss112PrototypeAuthoring.BossPath);
        Assert.That(testBoss.AutoAttack, Is.False);
        using var testData = new SerializedObject(testBoss);
        Assert.That(testData.FindProperty("useRunBossFlow").boolValue, Is.False);
    }

    [Test]
    public void EightLevelRoutesAlternateBossesAndTrackShopVisitsIndependently()
    {
        var root = new GameObject("Boss rotation route");
        var previous = RunProgressionManager.Instance;
        var previousDirector = RoomChoiceDirector.Instance;
        var random = Random.state;
        SetInstance<RunProgressionManager>(null); SetInstance<RoomChoiceDirector>(null);
        try
        {
            var progression = root.AddComponent<RunProgressionManager>();
            var director = root.AddComponent<RoomChoiceDirector>();
            var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
            for (int level = 0; level < 8; level++)
            {
                Assert.That(progression.CurrentBiomeIndex, Is.EqualTo(level));
                var current = map.GetRoomById("Test_Lobby");
                bool leftShop = false;
                for (int step = 0; step < director.BiomeTemplate.Count; step++)
                {
                    var offers = (current.ZoneMode == ZoneMode.SafeZone
                        ? director.GenerateLeaveOffersFromSafeRoom(current.RoomId)
                        : director.GenerateOffersAfterClear(current.RoomId)).ToArray();
                    Assert.That(offers, Is.Not.Empty);
                    var next = offers.FirstOrDefault(o => o.Kind == RoomKind.Shop || o.Kind == RoomKind.Level || o.Kind == RoomKind.Rest);
                    if (!next.IsValid) next = offers[0];
                    director.NotifyLeavingRoom(current.RoomId);
                    leftShop |= current.RoomKind == RoomKind.Shop && current.IncludeInRandomOffers;
                    Assert.That(director.HasVisitedShopThisAct, Is.EqualTo(leftShop));
                    director.NotifyRoomEntered(next.RoomId);
                    current = next.Room;
                }
                string expected = level % 2 == 0 ? "Boss_01" : "Boss_02";
                Assert.That(current.RoomId, Is.EqualTo(expected));
                Assert.That(progression.BossRoomId, Is.EqualTo(expected));
                Assert.That(progression.TryResolveForcedBossRoom(map, out var boss, requireDue: false), Is.True);
                Assert.That(boss.RoomId, Is.EqualTo(expected));
                var bonus = director.GenerateOffersAfterClear(current.RoomId);
                Assert.That(bonus.Any(o => o.Kind == RoomKind.Rest), Is.True);
                Assert.That(progression.CurrentBiomeIndex, Is.EqualTo(level + 1));
                Assert.That(director.HasVisitedShopThisAct, Is.False);
                director.NotifyRoomEntered(bonus.First(o => o.Kind == RoomKind.Rest).RoomId);
            }
        }
        finally
        {
            Object.DestroyImmediate(root); Random.state = random;
            SetInstance(previous); SetInstance(previousDirector);
        }
    }

    [Test]
    public void SkippingTheShopDoesNotPretendItWasVisitedAndBoss112UsesTheBossStatCurve()
    {
        var root = new GameObject("Shop skip route");
        var previous = RunProgressionManager.Instance;
        var previousDirector = RoomChoiceDirector.Instance;
        SetInstance<RunProgressionManager>(null); SetInstance<RoomChoiceDirector>(null);
        try
        {
            var progression = root.AddComponent<RunProgressionManager>();
            var director = root.AddComponent<RoomChoiceDirector>();
            var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
            var current = map.GetRoomById("Test_Lobby");
            Set(director, "optionalSafeRoomChance", 0f);
            for (int i = 0; i < 3; i++)
            {
                var offers = current.ZoneMode == ZoneMode.SafeZone ? director.GenerateLeaveOffersFromSafeRoom(current.RoomId)
                    : director.GenerateOffersAfterClear(current.RoomId);
                var next = offers.First(o => o.Kind != RoomKind.Shop);
                if (i == 2) Assert.That(next.Kind, Is.EqualTo(RoomKind.Level).Or.EqualTo(RoomKind.Rest));
                director.NotifyLeavingRoom(current.RoomId); director.NotifyRoomEntered(next.RoomId); current = next.Room;
                Assert.That(director.HasVisitedShopThisAct, Is.False);
            }
            var holder = new GameObject("Inactive boss holder"); holder.transform.SetParent(root.transform); holder.SetActive(false);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Boss112RunAuthoring.BossPath);
            for (int level = 0; level < 6; level++)
            {
                progression.SetCurrentBiomeIndex(level);
                var enemy = Object.Instantiate(source, holder.transform).GetComponent<EnemyController>();
                RunProgressionManager.ApplyBiomeDifficultyToEnemy(enemy);
                Assert.That(enemy.maxHP, Is.EqualTo(4000f * RunProgressionManager.ResolveBossHealthMultiplier(level)).Within(.01f));
                Assert.That(enemy.defense, Is.Zero);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }
        finally
        {
            Object.DestroyImmediate(root); SetInstance(previous); SetInstance(previousDirector);
        }
    }

    [UnityTest]
    public IEnumerator ProductionArenaSpawnsAnAutomaticBossAndItsDeathAdvancesToLevelThree()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var root = new GameObject("Live boss 112 fixture");
        var managers = new GameObject("Managers"); managers.transform.SetParent(root.transform);
        var progression = managers.AddComponent<RunProgressionManager>(); progression.SetCurrentBiomeIndex(1);
        var director = managers.AddComponent<RoomChoiceDirector>();
        var cameraHost = new GameObject("Boss run camera", typeof(Camera), typeof(CinemachineBrain));
        cameraHost.transform.SetParent(root.transform); cameraHost.tag = "MainCamera";
        cameraHost.GetComponent<Camera>().orthographic = true;
        cameraHost.GetComponent<Camera>().GetUniversalAdditionalCameraData();
        var light = new GameObject("Arena test light").AddComponent<Light2D>(); light.transform.SetParent(root.transform);
        light.lightType = Light2D.LightType.Global; light.intensity = 1f;
        var target = new GameObject("Arena player probe"); target.transform.SetParent(root.transform); target.tag = "Player";
        target.layer = LayerMask.NameToLayer("Player");
        var targetBody = target.AddComponent<Rigidbody2D>();
        targetBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        var targetCollider = target.AddComponent<BoxCollider2D>();
        targetCollider.size = new Vector2(.8f, 1.5f);
        targetCollider.offset = Vector2.up * .75f;
        var explore = new GameObject("Explore camera").AddComponent<CinemachineCamera>();
        explore.transform.SetParent(root.transform); explore.transform.position = new Vector3(-8f, 4f, -17f);
        var lens = explore.Lens; lens.OrthographicSize = 6.5f; lens.ModeOverride = LensSettings.OverrideModes.Orthographic; explore.Lens = lens;
        var provider = ScriptableObject.CreateInstance<ZoneStateProvider>();
        var cameraRig = new GameObject("Camera manager"); cameraRig.transform.SetParent(root.transform); cameraRig.SetActive(false);
        var manager = cameraRig.AddComponent<CameraManager>();
        Set(manager, "_exploreCam", explore);
        Set(manager, "_brain", cameraHost.GetComponent<CinemachineBrain>());
        Set(manager, "_zoneStateProvider", provider);
        Set(manager, "_playerTransform", target.transform);
        cameraRig.SetActive(true);
        var host = new GameObject("Layout host"); host.transform.SetParent(root.transform);
        var layout = host.AddComponent<RoomLayoutHost>();
        var death = new GameObject("Boss run death sequence"); death.transform.SetParent(root.transform);
        var sequence = death.AddComponent<BossDeathSequence>();
        var rewardFixture = new GameObject("Boss reward inventory");
        rewardFixture.transform.SetParent(root.transform);
        rewardFixture.SetActive(false);
        var previousTeamLoader = TeamLoader.Instance;
        var teamLoader = rewardFixture.AddComponent<TeamLoader>();
        var inventory = ScriptableObject.CreateInstance<CharacterInventoryState>();
        var meta = ScriptableObject.CreateInstance<CharacterProgressionState>();
        var character = ScriptableObject.CreateInstance<CharacterObject>();
        character.characterId = "boss-memory-test";
        var item = ScriptableObject.CreateInstance<CharacterItem>();
        item.itemName = "Boss reward test character";
        character.metaData.displayName = item.itemName;
        item.characterObjectData = character;
        inventory.ownedItems.Add(item);
        Set(teamLoader, "runInventory", inventory);
        Set(teamLoader, "metaProgression", meta);
        SetInstance(teamLoader);
        using (var data = new SerializedObject(sequence))
        {
            data.FindProperty("hitStopFrames").intValue = 0;
            data.FindProperty("slowMoDuration").floatValue = 0f;
            data.FindProperty("postSlowMoHold").floatValue = 0f;
            data.FindProperty("dissolveShakeStrength").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        try
        {
            Assert.That(TrainingSession.IsTrainingContext, Is.False);
            Assert.That(layout.MountRoom(Resources.Load<RoomData>("MapSO/RoomData_Boss_02")), Is.True);
            var arenaCamera = layout.ActiveLayoutInstance.GetComponentInChildren<CinemachineCamera>();
            var cameraPosition = arenaCamera.transform.position;
            float cameraSize = arenaCamera.Lens.OrthographicSize;
            int cameraPriority = arenaCamera.Priority.Value;
            var entrance = layout.ActiveLayoutInstance.GetComponentInChildren<EntrancePoint>();
            targetBody.position = entrance.SpawnPosition;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(Object.FindFirstObjectByType<Boss112Controller>(), Is.Null);
            Assert.That(arenaCamera.enabled, Is.False);
            Boss112Controller boss = null;
            yield return Until(() =>
            {
                boss = Object.FindFirstObjectByType<Boss112Controller>();
                if (boss != null) return true;
                targetBody.linearVelocity = new Vector2(12f, targetBody.linearVelocity.y);
                return false;
            }, waitingFor: "The player probe must walk down the corridor and cross the fight trigger.");
            targetBody.linearVelocity = Vector2.zero;
            boss.SetTarget(target.transform);
            Assert.That(boss.IsSpawnRevealing, Is.True);
            yield return Until(() => boss.Enemy.IsCombatReady);
            Assert.That(boss.AutoAttack, Is.True);
            Assert.That(boss.FloorY, Is.Zero.Within(.01f));
            Assert.That(boss.ArenaLeft, Is.EqualTo(-23.678f).Within(.01f));
            Assert.That(boss.ArenaRight, Is.EqualTo(23.678f).Within(.01f));
            Assert.That(boss.Enemy.maxHP, Is.EqualTo(16000f).Within(.01f));
            yield return Until(() => manager.IsBossSpawnFocusActive(boss.transform));
            var focus = (CinemachineCamera)typeof(CameraManager).GetField("_focusCam", Private).GetValue(manager);
            Assert.That(focus.Priority.Value, Is.GreaterThan(cameraPriority), "Intro must win over the authored priority-250 arena camera.");
            Assert.That(boss.TryStartPattern(Boss112Pattern.HeavySlam), Is.False);
            float revealStarted = Time.unscaledTime;
            while (Time.unscaledTime - revealStarted < .75f) yield return null;
            Assert.That(cameraHost.GetComponent<CinemachineBrain>().ActiveVirtualCamera, Is.SameAs(focus));
            Directory.CreateDirectory("Logs/Boss112");
            Boss112PrototypeTests.Capture("Run_00_SpawnReveal");
            while (manager.IsBossSpawnFocusActive(boss.transform))
            {
                Assert.That(boss.IsSpawnRevealing, Is.True);
                Assert.That(boss.Phase, Is.EqualTo(Boss112Phase.Idle));
                Assert.That(boss.Beam.IsDamaging || boss.ImpactCollider.enabled, Is.False);
                yield return null;
            }
            yield return Until(() => !boss.IsSpawnRevealing);
            Assert.That(Time.unscaledTime - revealStarted, Is.GreaterThanOrEqualTo(2.2f));
            Assert.That(arenaCamera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(arenaCamera.Lens.OrthographicSize, Is.EqualTo(cameraSize));
            Assert.That(arenaCamera.Priority.Value, Is.EqualTo(cameraPriority));
            yield return null;
            Assert.That(cameraHost.GetComponent<CinemachineBrain>().ActiveVirtualCamera, Is.SameAs(arenaCamera));
            Boss112PrototypeTests.Capture("Run_00_ReturnedToArena");
            yield return Until(() => boss.Phase == Boss112Phase.BeamLocked);
            Directory.CreateDirectory("Logs/Boss112");
            Boss112PrototypeTests.Capture("Run_01_AutomaticBeamWarning");
            var worm = boss.GetComponent<Boss112WormBody>();
            yield return Until(() => worm.SegmentHurtboxes.All(piece => piece.enabled));
            var attacks = AssetDatabase.LoadAssetAtPath<ActionResolverTable>(
                "Assets/Project/Character/Hero/test_0_Main/ActionResolverTable_main.asset");
            var normal = attacks.comboActions.First().comboTree.rootNode.stepData.hitData.hitboxes.OfType<NormalHitbox>().First();
            var attackObject = new GameObject("Ground attack probe"); attackObject.transform.SetParent(root.transform);
            var attackCollider = attackObject.AddComponent<BoxCollider2D>();
            attackCollider.isTrigger = true;
            attackCollider.size = normal.size;
            var hit = attackObject.AddComponent<HitBoxDamage>();

            void StrikeFromGround(float damageFraction)
            {
                float groundX = worm.GroundHurtbox.bounds.center.x - 3f;
                targetBody.position = new Vector2(groundX, boss.FloorY);
                attackObject.transform.position = new Vector3(groundX + normal.offset.x, boss.FloorY + normal.offset.y, 0f);
                Physics2D.SyncTransforms();
                Assert.That(attackCollider.Distance(worm.GroundHurtbox).isOverlapped, Is.True);
                if (!boss.IsStaggered)
                    Assert.That(attackCollider.Distance(boss.Enemy.bodyCollider).isOverlapped, Is.False);
                hit.ResetActivationState();
                hit.SetHitData(damageFraction * boss.Enemy.maxHP, false, false, 0f, 1f, normal, AttackCategory.Normal, "RunGroundAttack", null, null);
                float hp = boss.Enemy.currentHP;
                foreach (var piece in worm.SegmentHurtboxes)
                    if (piece.enabled && attackCollider.Distance(piece).isOverlapped) hit.ProcessHit(boss.Enemy, piece);
                Assert.That(boss.Enemy.currentHP, Is.LessThan(hp));
                Assert.That(boss.Enemy.currentHP, Is.EqualTo(Mathf.Max(0f, hp - hit.DamageValue)).Within(.02f));
            }

            StrikeFromGround(.1875f);
            yield return Until(() => boss.Phase == Boss112Phase.SlamLocked);
            Boss112PrototypeTests.Capture("Run_02_SlamWarning");
            Assert.That(worm.SegmentHurtboxes.All(piece => !piece.enabled), Is.True);
            yield return Until(() => boss.IsStaggered && worm.SegmentHurtboxes.All(piece => piece.enabled));
            for (int i = 0; i < 4 && !boss.IsSecondPhase; i++) StrikeFromGround(.1953125f);
            Assert.That(boss.IsSecondPhase, Is.True);
            yield return Until(() => boss.Phase == Boss112Phase.SlamFeint && boss.Pattern == Boss112Pattern.DelayedSlam,
                waitingFor: "After reaching half health, the automatic boss must use the delayed slam.");
            yield return Until(() => boss.Phase == Boss112Phase.SlamImpact);
            var shockwaves = Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None);
            Assert.That(shockwaves.Length, Is.EqualTo(2));
            var segmentVisuals = worm.SegmentHurtboxes.Select(piece => piece.GetComponent<SpriteRenderer>()).ToArray();
            foreach (bool prioritized in new[] { false, true })
            {
                boss.Enemy.SetPriority(prioritized);
                yield return null;
                foreach (var shockwave in shockwaves)
                {
                    var waveVisual = shockwave.GetComponent<SpriteRenderer>();
                    Assert.That(waveVisual.sortingOrder, Is.GreaterThan(segmentVisuals.Max(piece => piece.sortingOrder)));
                    Assert.That(segmentVisuals.All(piece => piece.sortingLayerID == waveVisual.sortingLayerID), Is.True);
                }
            }
            Boss112PrototypeTests.Capture("Run_02_ShockwavesInFront");
            yield return Until(() => boss.IsStaggered && worm.SegmentHurtboxes.All(piece => piece.enabled));
            Boss112PrototypeTests.Capture("Run_02_RecoverySegments");
            for (int i = 0; i < 5 && boss.Enemy.IsAlive; i++) StrikeFromGround(.1953125f);
            Assert.That(boss.Enemy.IsAlive, Is.False);
            Assert.That(boss.Beam.IsDamaging || boss.ImpactCollider.enabled, Is.False);
            var picker = default(ArtifactRewardPickerUI);
            var portrait = default(Button);
            yield return Until(() =>
            {
                picker = Object.FindFirstObjectByType<ArtifactRewardPickerUI>();
                portrait = picker != null
                    ? picker.transform.Find("Living Characters")?.GetComponentsInChildren<Button>()
                        .FirstOrDefault(button => button.transform.Find("Title")?.GetComponent<TMPro.TMP_Text>()?.text == item.itemName)
                    : null;
                return portrait != null && picker.GetComponent<CanvasGroup>().interactable;
            }, 12f, "Defeating the boss should open the Memory reward.");
            Assert.That(sequence.IsWaitingForContinue, Is.False);
            Assert.That(TeamLoader.Instance, Is.SameAs(teamLoader));
            Assert.That(TeamLoader.Instance.MetaProgression, Is.SameAs(meta));
            portrait.onClick.Invoke();
            Button upgrade = null;
            yield return Until(() =>
            {
                upgrade = picker.transform.Find("Memory Choices")?.GetComponentInChildren<Button>();
                return upgrade != null && upgrade.IsInteractable() && picker.GetComponent<CanvasGroup>().interactable;
            }, waitingFor: "Selecting a character should open the Memory upgrade choices.");
            var prompt = picker.transform.Find("PullLabel").GetComponent<TMPro.TMP_Text>();
            upgrade.onClick.Invoke();
            Assert.That(prompt.text, Does.StartWith("Select again to claim"));
            yield return null;
            Assert.That(upgrade.IsInteractable(), Is.True);
            upgrade.onClick.Invoke();
            Assert.That(prompt.text, Is.EqualTo("Memory acquired"));
            yield return Until(() => meta.GetCompletedMemorySteps(character.characterId) == 1,
                waitingFor: "Confirming the Memory card should grant one step.");
            yield return Until(() => sequence.IsWaitingForContinue, 12f, "The Continue prompt should appear after claiming Memory.");
            Assert.That(progression.CurrentBiomeIndex, Is.EqualTo(2));
            Assert.That(progression.BossRoomId, Is.EqualTo("Boss_01"));
            Assert.That(director.ActIndex, Is.Zero);
            Assert.That(Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None), Is.Empty);
            Boss112PrototypeTests.Capture("Run_03_DefeatedContinuePrompt");
        }
        finally
        {
            SetInstance(previousTeamLoader);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(meta);
            Object.DestroyImmediate(item);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(provider);
            if (Project.UI.BossHealthHUD.Instance != null) Object.DestroyImmediate(Project.UI.BossHealthHUD.Instance.gameObject);
            if (Project.UI.BossDeathPromptUI.Instance != null) Object.DestroyImmediate(Project.UI.BossDeathPromptUI.Instance.gameObject);
            Time.timeScale = 1f;
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DisablingTheBossDuringRevealReleasesTheCameraAndDoesNotReplayTheIntro()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var root = new GameObject("Cancelled boss reveal");
        var provider = ScriptableObject.CreateInstance<ZoneStateProvider>();
        var rig = new GameObject("Camera rig"); rig.transform.SetParent(root.transform); rig.SetActive(false);
        var manager = rig.AddComponent<CameraManager>();
        var explore = new GameObject("Explore").AddComponent<CinemachineCamera>(); explore.transform.SetParent(root.transform);
        explore.transform.position = new Vector3(-8f, 4f, -17f);
        var arena = new GameObject("Arena").AddComponent<CinemachineCamera>(); arena.transform.SetParent(root.transform); arena.Priority = 250;
        Set(manager, "_exploreCam", explore); Set(manager, "_zoneStateProvider", provider);
        rig.SetActive(true);
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Boss112RunAuthoring.BossPath);
            var boss = Object.Instantiate(source, root.transform).GetComponent<Boss112Controller>();
            boss.ConfigureArena(-18f, 18f, 0f, 23f, arena);
            yield return Until(() => manager.IsBossSpawnFocusActive(boss.transform));
            boss.gameObject.SetActive(false);
            Assert.That(boss.IsSpawnRevealing, Is.False);
            Assert.That(manager.IsBossSpawnFocusActive(boss.transform), Is.False);
            Assert.That(typeof(CameraManager).GetField("_focusCam", Private).GetValue(manager), Is.Null);
            Assert.That(arena.Priority.Value, Is.EqualTo(250));
            boss.gameObject.SetActive(true);
            yield return null;
            Assert.That(boss.IsSpawnRevealing, Is.False);
            Assert.That(manager.IsBossSpawnFocusActive(boss.transform), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(provider);
            if (Project.UI.BossHealthHUD.Instance != null) Object.DestroyImmediate(Project.UI.BossHealthHUD.Instance.gameObject);
        }
        yield return new ExitPlayMode();
    }

    private static IEnumerator Until(System.Func<bool> condition, float timeout = 15f, string waitingFor = null)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), waitingFor); yield return null; }
    }
    private static void SetInstance<T>(T value) => typeof(T).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
#endif
