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
        Assert.That(boss.AutoAttack, Is.True);
        Assert.That(boss.Enemy.HasBossDriver, Is.True);
        using var spawnData = new SerializedObject(boss.Enemy);
        Assert.That(spawnData.FindProperty("useSpawnPresentation").boolValue, Is.False);
        Assert.That(spawnData.FindProperty("snapSpawnToGround").boolValue, Is.False);
        using var data = new SerializedObject(boss);
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
                for (int step = 0; step < director.BiomeTemplate.Count; step++)
                {
                    var offers = (current.ZoneMode == ZoneMode.SafeZone
                        ? director.GenerateLeaveOffersFromSafeRoom(current.RoomId)
                        : director.GenerateOffersAfterClear(current.RoomId)).ToArray();
                    Assert.That(offers, Is.Not.Empty);
                    var next = offers.FirstOrDefault(o => o.Kind == RoomKind.Shop || o.Kind == RoomKind.Level || o.Kind == RoomKind.Rest);
                    if (!next.IsValid) next = offers[0];
                    director.NotifyLeavingRoom(current.RoomId);
                    if (step == 0) Assert.That(director.HasVisitedShopThisAct, Is.False, "Leaving the lobby is not a shop visit.");
                    else if (current.RoomKind == RoomKind.Shop) Assert.That(director.HasVisitedShopThisAct, Is.True);
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
            for (int i = 0; i < 2; i++)
            {
                var offers = current.ZoneMode == ZoneMode.SafeZone ? director.GenerateLeaveOffersFromSafeRoom(current.RoomId)
                    : director.GenerateOffersAfterClear(current.RoomId);
                var next = offers.First(o => o.Kind == RoomKind.Combat);
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
                Assert.That(enemy.maxHP, Is.EqualTo(new[] { 8000f, 16000f, 24000f, 26500f, 29000f, 31500f }[level]).Within(.01f));
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
        var target = new GameObject("Arena target"); target.transform.SetParent(root.transform); target.transform.position = Vector3.right * -8f;
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
            var waves = layout.ActiveLayoutInstance.GetComponentInChildren<RoomWaveManager>();
            waves.StartEncounter();
            Boss112Controller boss = null;
            yield return Until(() => (boss = Object.FindFirstObjectByType<Boss112Controller>()) != null);
            boss.SetTarget(target.transform);
            Assert.That(boss.IsSpawnRevealing, Is.True);
            yield return Until(() => boss.Enemy.IsCombatReady);
            Assert.That(boss.AutoAttack, Is.True);
            Assert.That(boss.FloorY, Is.Zero.Within(.01f));
            Assert.That(boss.ArenaLeft, Is.EqualTo(-18f).Within(.01f));
            Assert.That(boss.ArenaRight, Is.EqualTo(18f).Within(.01f));
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
            yield return Until(() => boss.Phase == Boss112Phase.SlamLocked);
            Boss112PrototypeTests.Capture("Run_02_SlamWarning");
            boss.AutoAttack = false; boss.CancelPattern(); boss.Enemy.currentHP = boss.Enemy.maxHP * .5f;
            Assert.That(boss.TryStartPattern(Boss112Pattern.SweepingBeam), Is.True);
            yield return Until(() => boss.Beam.IsDamaging);
            boss.Enemy.Die();
            Assert.That(boss.Beam.IsDamaging || boss.ImpactCollider.enabled, Is.False);
            CharacterMemoryPanel picker = null;
            yield return Until(() => (picker = Object.FindFirstObjectByType<CharacterMemoryPanel>()) != null, 12f);
            Assert.That(sequence.IsWaitingForContinue, Is.False);
            Button choose = picker.GetComponentsInChildren<Button>().Single(button => button.name.StartsWith(item.itemName));
            choose.onClick.Invoke();
            choose.onClick.Invoke();
            Assert.That(meta.GetCompletedMemorySteps(character.characterId), Is.EqualTo(1));
            picker.GetComponentsInChildren<Button>().Single(button => button.name == "Back").onClick.Invoke();
            yield return Until(() => sequence.IsWaitingForContinue, 12f);
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

    private static IEnumerator Until(System.Func<bool> condition, float timeout = 15f)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
    }
    private static void SetInstance<T>(T value) => typeof(T).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
#endif
