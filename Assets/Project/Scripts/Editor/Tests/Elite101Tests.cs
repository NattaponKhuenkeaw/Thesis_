#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class Elite101Tests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Random.State random;
    [SetUp] public void SaveRandom() => random = Random.state;
    [TearDown] public void RestoreRandom() => Random.state = random;
    [UnityTearDown] public IEnumerator ExitRuntime()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [Test]
    public void VariantHasMoreHealthTwoShortRushesAndNoBossOrShieldTreatment()
    {
        var normal = AssetDatabase.LoadAssetAtPath<GameObject>(Elite101Authoring.SourcePath);
        var elite = AssetDatabase.LoadAssetAtPath<GameObject>(Elite101Authoring.ElitePath);
        var source = normal.GetComponent<EnemyController>();
        var enemy = elite.GetComponent<EnemyController>();
        Assert.That(PrefabUtility.GetPrefabAssetType(elite), Is.EqualTo(PrefabAssetType.Variant));
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(elite), Is.EqualTo(normal));
        Assert.That(enemy.maxHP, Is.EqualTo(source.maxHP * 3f));
        Assert.That(enemy.attackDamage, Is.EqualTo(source.attackDamage));
        Assert.That(enemy.defense, Is.EqualTo(source.defense));
        Assert.That(elite.transform.localScale.x, Is.EqualTo(normal.transform.localScale.x * 1.5f));
        Assert.That(elite.transform.localScale.y, Is.EqualTo(normal.transform.localScale.y * 1.5f));
        Assert.That(enemy.isEliteOrBoss || enemy.StartArmored || enemy.HasBossDriver, Is.False);
        Assert.That(elite.GetComponent<EnemyShieldGuard>(), Is.Null);
        Assert.That(enemy.ResistsOrdinaryHitReactions, Is.True);
        Assert.That(source.ResistsOrdinaryHitReactions, Is.False);
        Assert.That(enemy.ArmorDuringCommittedSwing, Is.False, "Grab must remain usable during the rush.");
        Assert.That(enemy.Kit.BodyCharge, Is.True);
        Assert.That(enemy.Kit.ChargesPerAttack, Is.EqualTo(2));
        Assert.That(enemy.Kit.LungeDistance, Is.EqualTo(2f));
        Assert.That(enemy.Kit.HitWindowDuration, Is.EqualTo(.22f));
        Assert.That(enemy.Kit.RepeatChargeWarning, Is.EqualTo(.3f));
        Assert.That(source.Kit.ChargesPerAttack, Is.EqualTo(1));
        Assert.That(Resources.Load<EnemyKit>("EnemySo/EK_ShieldGuard").ChargesPerAttack, Is.EqualTo(1));
    }

    [Test]
    public void EliteUsesOneExternallyDrivenSharedWarningWithoutAddingPhysics()
    {
        var elite = AssetDatabase.LoadAssetAtPath<GameObject>(Elite101Authoring.ElitePath);
        var normal = AssetDatabase.LoadAssetAtPath<GameObject>(Elite101Authoring.SourcePath);
        var warnings = elite.GetComponentsInChildren<EnemyChargeTelegraph>(true);
        Assert.That(warnings.Length, Is.EqualTo(1));
        Assert.That(normal.GetComponentInChildren<EnemyChargeTelegraph>(true), Is.Null);
        using var data = new SerializedObject(warnings[0]);
        Assert.That(data.FindProperty("externallyDriven").boolValue, Is.True);
        Assert.That(warnings[0].GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(warnings[0].GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
        var shared = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/EnemyChargeTelegraph.prefab");
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(warnings[0].gameObject), Is.EqualTo(shared));
    }

    [TestCase(0, 0, false)]
    [TestCase(0, 2, false)]
    [TestCase(1, 0, true)]
    [TestCase(3, 5, true)]
    public void HardIntroducesOneEliteInTheLastChamberFromBiomeTwo(int biome, int depth, bool expectElite)
    {
        var hard = Resources.Load<EncounterSO>("EncounterSO/Encounter_Hard");
        Assert.That(hard.FinalChamberElite, Is.Not.Null);
        Assert.That(hard.EnemyTypes.Any(entry => entry.prefab == hard.FinalChamberElite), Is.False);
        var encounters = new[] { hard, hard, hard, hard };
        var quotas = encounters.Select(e => EncounterPhaseComposition.Roll(e, 8, biome, depth, true)).ToArray();
        Assert.That(EncounterPhaseComposition.ApplyFinalChamberElite(encounters, quotas, biome), Is.EqualTo(expectElite));
        Assert.That(EncounterPhaseComposition.ApplyFinalChamberElite(encounters, quotas, biome), Is.EqualTo(expectElite));
        Assert.That(quotas.Sum(q => q.Count(p => p == hard.FinalChamberElite)), Is.EqualTo(expectElite ? 1 : 0));
        for (int i = 0; i < quotas.Length; i++)
        {
            Assert.That(quotas[i].Count, Is.EqualTo(8));
            Assert.That(quotas[i].Count(p => p == hard.FinalChamberElite), Is.EqualTo(expectElite && i == quotas.Length - 1 ? 1 : 0));
        }
        Assert.That(hard.AliveCap, Is.EqualTo(5));
        Assert.That(hard.SpawnMode, Is.EqualTo(EncounterSpawnMode.Pressure));
    }

    [TestCase("Encounter_Easy")]
    [TestCase("Encounter_Medium")]
    public void EasierCardsNeverReceiveAnElite(string name)
    {
        var encounter = Resources.Load<EncounterSO>("EncounterSO/" + name);
        Assert.That(encounter.FinalChamberElite, Is.Null);
        var quotas = new[] { EncounterPhaseComposition.Roll(encounter, encounter.ChamberQuota, 0, 0, false) };
        Assert.That(EncounterPhaseComposition.ApplyFinalChamberElite(new[] { encounter }, quotas, 1), Is.False);
    }

    [Test]
    public void EmptyTrailingRoomsDoNotConsumeTheReplacement()
    {
        var hard = Resources.Load<EncounterSO>("EncounterSO/Encounter_Hard");
        var quota = Enumerable.Repeat(hard.EliteReplaces, 8).ToList();
        var quotas = new[] { new List<GameObject>(), quota, new List<GameObject>() };
        Assert.That(EncounterPhaseComposition.ApplyFinalChamberElite(new[] { hard, hard, hard }, quotas, 1), Is.True);
        Assert.That(quota.Count(p => p == hard.FinalChamberElite), Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator RushesTwiceAndRestoresItsHitbox() => RunRush(false, false);
    [UnityTest] public IEnumerator LeftFacingRushesOnlyDamagePlayersInsideTheirOwnWarning() => RunRush(false, false, -1f);
    [UnityTest] public IEnumerator ShortRushesStopAtWalls() => RunRush(true, false);
    [UnityTest] public IEnumerator NormalHitsDoNotStunButMainStyleAnchorInterruptsTheRush() => RunRush(false, true);

    [UnityTest]
    public IEnumerator GrabFinisherReleasesEliteInTheAirAndItFallsBackToTheFloor()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var root = new GameObject("Elite grab fixture");
        try
        {
            var floor = new GameObject("Floor");
            floor.transform.SetParent(root.transform);
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = Vector3.down * .5f;
            floor.AddComponent<BoxCollider2D>().size = new Vector2(40f, 1f);
            var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Elite101Authoring.ElitePath),
                root.transform).GetComponent<EnemyController>();
            enemy.SetBehaviorAgentActive(false);
            enemy.InitializeSpawn(Vector3.zero, Quaternion.identity);
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!enemy.IsCombatReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(enemy.IsCombatReady, Is.True);
            enemy.SetBehaviorAgentActive(false);
            var socket = new GameObject("Grab socket");
            socket.transform.SetParent(root.transform);
            socket.transform.position = enemy.transform.position + Vector3.up * 4f;
            var grab = HitReactionData.Default(1f, Vector2.right, 0f);
            grab.reactionKind = EnemyReactionKind.Grab;
            grab.reactionTiming = HitReactionData.CreateTiming(0, 120, 12);
            grab.grabSocket = socket.transform;
            grab.attackerTransform = socket.transform;
            grab.reactionOwnerId = root.GetInstanceID();
            enemy.TakeResolvedHit(grab, Vector2.zero);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(enemy.IsGrabbed, Is.True);
            Assert.That(enemy.bodyCollider.bounds.min.y, Is.GreaterThan(1f));

            var hit = HitReactionData.Default(1f, Vector2.right, 0f);
            hit.reactionKind = EnemyReactionKind.Knockback;
            hit.reactionTiming = HitReactionData.CreateTiming(0, 8, 8);
            hit.reactionOwnerId = grab.reactionOwnerId;
            enemy.TakeResolvedHit(hit, Vector2.zero);
            Assert.That(enemy.IsGrabbed, Is.True, "Ordinary hits should not drop a held Elite.");

            hit.breaksGrab = true;
            hit.reactionOwnerId = grab.reactionOwnerId + 1;
            enemy.TakeResolvedHit(hit, Vector2.zero);
            Assert.That(enemy.IsGrabbed, Is.True, "Another attacker must not break this grab.");
            hit.reactionOwnerId = grab.reactionOwnerId;
            enemy.TakeResolvedHit(hit, Vector2.zero);
            Assert.That(enemy.IsGrabbed, Is.False, "The owner's finisher must end the grab immediately.");
            deadline = Time.realtimeSinceStartup + 2f;
            while (enemy.bodyCollider.bounds.min.y > .1f && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();
            Assert.That(enemy.bodyCollider.bounds.min.y, Is.InRange(-.1f, .1f));
        }
        finally { Object.Destroy(root); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RealHardLayoutPlansOneEliteOnlyForItsFinalChamber()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        bool hadOffer = ActiveRouteOffer.HasSelection;
        var previousOffer = ActiveRouteOffer.Current;
        GameObject layout = null;
        GameObject progressionHost = null;
        RunProgressionManager progression = RunProgressionManager.Instance;
        int previousBiome = progression != null ? progression.currentBiomeIndex : 0;
        try
        {
            var room = Resources.Load<RoomData>("MapSO/LV/RoomData_LV_01");
            ActiveRouteOffer.Set(new RoomChoiceOffer(room, RouteDifficulty.Hard, null, null));
            if (progression == null)
            {
                progressionHost = new GameObject("Biome two progression");
                progression = progressionHost.AddComponent<RunProgressionManager>();
            }
            progression.currentBiomeIndex = 1;
            layout = Object.Instantiate(Resources.Load<GameObject>("RoomLayouts/LV/RoomLayout_LV_01"));
            var stage = layout.GetComponentInChildren<CombatStageController>(true);
            var waves = layout.GetComponentInChildren<RoomWaveManager>(true);
            Assert.That(stage.TryBegin(waves, System.Array.Empty<GameObject>()), Is.True);
            var quotas = (List<GameObject>[])typeof(CombatStageController).GetField("slices", Private).GetValue(stage);
            var elite = Resources.Load<EncounterSO>("EncounterSO/Encounter_Hard").FinalChamberElite;
            Assert.That(quotas.Select(quota => quota.Count), Is.EqualTo(new[] { 7, 7, 6 }));
            Assert.That(quotas.Take(quotas.Length - 1).SelectMany(quota => quota)
                .Any(prefab => prefab.GetComponent<EnemyController>().CombatRole == EnemyCombatRole.ShieldGuard), Is.True,
                "The Hard biome-two layout must teach the shield response before its elite.");
            Assert.That(quotas[quotas.Length - 1].Any(prefab => prefab.GetComponent<EnemyController>().CombatRole == EnemyCombatRole.ShieldGuard), Is.True,
                "The elite must retain the shield encounter's teaching role in its chamber.");
            for (int i = 0; i < quotas.Length; i++)
            {
                Assert.That(quotas[i].Count(p => p == elite), Is.EqualTo(i == quotas.Length - 1 ? 1 : 0));
            }
        }
        finally
        {
            if (layout != null) Object.Destroy(layout);
            if (progressionHost != null) Object.Destroy(progressionHost);
            else if (progression != null) progression.currentBiomeIndex = previousBiome;
            if (hadOffer) ActiveRouteOffer.Set(previousOffer); else ActiveRouteOffer.Clear();
        }
        yield return new ExitPlayMode();
    }

    private static IEnumerator RunRush(bool wall, bool grab, float direction = 1f)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture = new GameObject("Elite rush fixture");
        EnemyAttackAction action = null;
        var probes = new List<PlayerCharacter>();
        try
        {
            fixture.AddComponent<AudioListener>();
            var floor = new GameObject("Floor");
            floor.transform.SetParent(fixture.transform);
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = Vector3.down;
            floor.AddComponent<BoxCollider2D>().size = new Vector2(40f, 2f);
            var holder = new GameObject("Inactive spawn");
            holder.transform.SetParent(fixture.transform);
            holder.SetActive(false);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Elite101Authoring.ElitePath), holder.transform);
            root.transform.position = new Vector3(0f, 1f, 0f);
            var graph = root.GetComponent<BehaviorGraphAgent>();
            if (graph != null) graph.enabled = false;
            var enemy = root.GetComponent<EnemyController>();
            holder.SetActive(true);
            enemy.InitializeSpawn(root.transform.position, root.transform.rotation);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!enemy.IsCombatReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(enemy.IsCombatReady, Is.True);
            enemy.SetBehaviorAgentActive(false);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(graph == null || !graph.enabled, Is.True, "Only the manually driven action may control this fixture.");
            var target = new GameObject("Target");
            target.transform.SetParent(fixture.transform);
            target.transform.position = root.transform.position + Vector3.right * (direction * 1.6f);
            if (wall)
            {
                var blocker = new GameObject("Wall");
                blocker.transform.SetParent(fixture.transform);
                blocker.layer = floor.layer;
                blocker.transform.position = new Vector3(enemy.bodyCollider.bounds.max.x + .55f, 2f);
                blocker.AddComponent<BoxCollider2D>().size = new Vector2(.3f, 8f);
            }
            Physics2D.SyncTransforms();
            var hitbox = (BoxCollider2D)enemy.attackHitbox;
            Vector2 size = hitbox.size, offset = hitbox.offset;
            if (!grab)
            {
                probes.Add(CreateDamageProbe("Inside warning"));
                probes.Add(CreateDamageProbe("Behind the Elite"));
                probes.Add(CreateDamageProbe("Beyond warning"));
                probes.Add(CreateDamageProbe("Above warning"));
            }
            action = new EnemyAttackAction();
            action.Agent.Value = root;
            action.Target.Value = target;
            action.CanAttack.Value = true;
            Assert.That(Call(action, "OnStart"), Is.EqualTo("Running"));
            var visual = root.GetComponentInChildren<EnemyChargeTelegraph>(true);
            var lane = visual.transform.Find("background").GetComponent<LineRenderer>();
            var fill = visual.transform.Find("fill").GetComponent<LineRenderer>();
            Assert.That(lane.enabled, Is.True);
            var warnedRushes = new HashSet<int>();
            var filledRushes = new HashSet<int>();
            int rushes = 0;
            bool wasActive = false, checkedHits = false;
            float rushStart = 0f;
            deadline = Time.realtimeSinceStartup + 12f;
            string status = "Running";
            int updatedFrame = -1;
            while (status == "Running" && Time.realtimeSinceStartup < deadline)
            {
                // EditMode's test runner may resume more often than the Play-mode frame loop.
                if (updatedFrame == Time.frameCount)
                {
                    yield return null;
                    continue;
                }
                updatedFrame = Time.frameCount;
                status = Call(action, "OnUpdate");
                bool active = hitbox.enabled;
                if (lane.enabled)
                {
                    Assert.That(active, Is.False, "The warning must end before damage starts.");
                    Assert.That(Vector3.Distance(lane.GetPosition(0), lane.GetPosition(1)), Is.EqualTo(enemy.Kit.LungeDistance).Within(.001f));
                    Assert.That(lane.startWidth, Is.EqualTo(enemy.bodyCollider.bounds.size.y).Within(.001f));
                    warnedRushes.Add(rushes);
                    float filled = Vector3.Distance(fill.GetPosition(0), fill.GetPosition(1));
                    if (fill.enabled && filled > .05f && filled < enemy.Kit.LungeDistance - .01f)
                        filledRushes.Add(rushes);
                }
                if (active) Assert.That(lane.enabled, Is.False);
                if (active && !wasActive)
                {
                    rushes++;
                    rushStart = enemy.rb.position.x;
                    if (!grab)
                    {
                        Vector3 start = lane.GetPosition(0);
                        Vector3 end = lane.GetPosition(1);
                        float height = lane.startWidth;
                        Vector3 center = enemy.bodyCollider.bounds.center;
                        float rear = center.x - direction * enemy.bodyCollider.bounds.extents.x;
                        PlaceProbe(probes[0], Vector3.Lerp(start, end, .55f));
                        PlaceProbe(probes[1], new Vector3(rear - direction * .052f, start.y));
                        PlaceProbe(probes[2], end + Vector3.right * (direction * .067f));
                        PlaceProbe(probes[3], Vector3.Lerp(start, end, .55f) + Vector3.up * (height / 2f + .067f));
                        Physics2D.SyncTransforms();
                        Bounds box = hitbox.bounds;
                        Assert.That((box.center.x - center.x) * direction, Is.GreaterThan(0f));
                        Assert.That(box.size.x, Is.LessThan(enemy.bodyCollider.bounds.size.x));
                    }
                    if (grab)
                    {
                        for (int i = 0; i < 5; i++)
                            enemy.TakeResolvedHit(HitReactionData.Default(1f, Vector2.right, 3f), target.transform.position);
                        Assert.That(enemy.currentHP, Is.EqualTo(enemy.maxHP - 5f));
                        Assert.That(enemy.LastHitAppliedReaction, Is.False);
                        Assert.That(enemy.CurrentReactionState, Is.EqualTo(EnemyReactionState.None));
                        Assert.That(enemy.ignoreHitReactions, Is.False);
                        var capture = HitReactionData.Default(1f, Vector2.zero, 0f);
                        capture.actionAnchor = target.transform;
                        capture.actionAnchorHoldFrames = 24;
                        enemy.TakeResolvedHit(capture, target.transform.position);
                        Assert.That(enemy.LastHitAppliedReaction, Is.True);
                        Assert.That(enemy.hitReactionHandler.IsGrabbed, Is.True);
                        Assert.That(Call(action, "OnUpdate"), Is.EqualTo("Failure"));
                        checkedHits = true;
                        break;
                    }
                }
                if (!active && wasActive)
                {
                    float travel = (enemy.rb.position.x - rushStart) * direction;
                    Assert.That(travel, Is.LessThanOrEqualTo(wall ? .5f : enemy.Kit.LungeDistance + .15f));
                    var contacts = (RaycastHit2D[])typeof(EnemyAttackAction).GetField("m_ChargeHits", Private).GetValue(action);
                    if (!wall) Assert.That(travel, Is.GreaterThan(.3f),
                        $"Rush {rushes}: feet={enemy.bodyCollider.bounds.min.y}, body={enemy.rb.bodyType}, constraints={enemy.rb.constraints}, "
                        + string.Join("; ", contacts.Where(h => h.collider != null).Select(h => $"{h.collider.name}: distance={h.distance}, normal={h.normal}")));
                    foreach (var probe in probes)
                    {
                        float expected = probe == probes[0] && !wall ? 1000f - enemy.attackDamage : 1000f;
                        Assert.That(probe.CurrentHP, Is.EqualTo(expected).Within(.001f), $"Rush {rushes}: {probe.name}");
                    }
                }
                wasActive = active;
                yield return null;
            }
            Call(action, "OnEnd");
            action = null;
            Assert.That(hitbox.enabled, Is.False);
            Assert.That(hitbox.size, Is.EqualTo(size));
            Assert.That(hitbox.offset, Is.EqualTo(offset));
            Assert.That(visual.GetComponentsInChildren<LineRenderer>(true).All(line => !line.enabled), Is.True);
            if (grab) Assert.That(checkedHits, Is.True);
            else
            {
                Assert.That(status, Is.EqualTo("Success"));
                Assert.That(rushes, Is.EqualTo(2));
                Assert.That(warnedRushes, Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(filledRushes, Is.EquivalentTo(new[] { 0, 1 }));
            }
        }
        finally
        {
            if (action != null) Call(action, "OnEnd");
            foreach (var probe in probes) Object.Destroy(probe.gameObject);
            Object.Destroy(fixture);
        }
        yield return new ExitPlayMode();
    }

    private static PlayerCharacter CreateDamageProbe(string name)
    {
        var root = new GameObject(name);
        root.layer = LayerMask.NameToLayer("Player");
        root.transform.position = new Vector3(0f, -10f);
        root.AddComponent<BoxCollider2D>().size = new Vector2(.13f, .13f);
        var player = root.AddComponent<PlayerCharacter>();
        // Keep the hurt collider live without registering a player that has no traversal state machine.
        player.enabled = false;
        root.GetComponent<PlayerWorldUI>().enabled = false;
        typeof(PlayerCharacter).GetField("postHitInvulnerabilityDuration", Private).SetValue(player, 0f);
        return player;
    }

    private static void PlaceProbe(PlayerCharacter player, Vector3 position)
    {
        player.transform.position = position;
        typeof(PlayerCharacter).GetField("currentHP", Private).SetValue(player, 1000f);
    }

    private static string Call(EnemyAttackAction action, string method) =>
        typeof(EnemyAttackAction).GetMethod(method, Private).Invoke(action, null)?.ToString();
}
#endif
