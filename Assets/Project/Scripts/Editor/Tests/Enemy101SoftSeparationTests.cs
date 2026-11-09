#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class Enemy101SoftSeparationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string EnemyRoot = "Assets/Project/Character/enemy/";

    [TestCase("enemy/Enm101/Enemy1", true)]
    [TestCase("enemy/EnemyCore/Enemy_Dummy", false)]
    [TestCase("enemy/Enm102/Enemy102", false)]
    [TestCase("enemy/Enm103/Enemy103", false)]
    [TestCase("Boss/Executioner/Boss_Executioner", false)]
    public void Only101EnablesSoftSeparationWithoutRetuningReaction(string path, bool enabled)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRoot + path + ".prefab");
        var locomotion = prefab.GetComponent<EnemyLocomotion>();
        if (locomotion == null)
        {
            Assert.That(enabled, Is.False);
            return;
        }
        using var movement = new SerializedObject(locomotion);
        Assert.That(movement.FindProperty("softGroundSeparation").boolValue, Is.EqualTo(enabled));
        if (!enabled) return;
        Assert.That(prefab.GetComponent<EnemyHitReactionHandler>().UsesPositionReactions, Is.True);
    }

    [UnityTest]
    public IEnumerator FiveGrounded101sSeparateGraduallyAndStayInACompactStablePack()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var fixture = new Fixture(5))
        {
            yield return fixture.Ready();
            fixture.Collapse();
            var previous = fixture.Enemies.Select(e => e.rb.position.x).ToArray();
            float previousTime = Time.fixedTime;
            for (int i = 0; i < 45; i++)
            {
                yield return PhysicsStep();
                float elapsed = Time.fixedTime - previousTime;
                for (int e = 0; e < previous.Length; e++)
                {
                    float x = fixture.Enemies[e].rb.position.x;
                    Assert.That(Mathf.Abs(x - previous[e]), Is.LessThanOrEqualTo(1.8f * elapsed + .015f));
                    Assert.That(Mathf.Abs(x), Is.LessThan(.86f), "Do not scatter a collapsed combo pack");
                    previous[e] = x;
                }
                previousTime = Time.fixedTime;
            }
            float[] settled = fixture.XPositions();
            Assert.That(settled[4] - settled[0], Is.InRange(1.4f, 1.7f));
            for (int i = 1; i < settled.Length; i++) Assert.That(settled[i] - settled[i - 1], Is.GreaterThan(.25f));
            for (int i = 0; i < 20; i++) yield return PhysicsStep();
            float[] later = fixture.XPositions();
            for (int i = 0; i < settled.Length; i++) Assert.That(later[i], Is.EqualTo(settled[i]).Within(.03f));
            Assert.That(fixture.Enemies.All(e => e.bodyCollider.bounds.min.y >= -.03f), Is.True);
            TestContext.WriteLine("Five grounded slots: " + string.Join(", ", later.Select(x => x.ToString("F3"))));
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ACollapsedPackCanResumeChasingAndReachBothFlanksWithoutStayingStacked()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var fixture = new Fixture(5))
        {
            yield return fixture.Ready();
            fixture.Collapse(-5f);
            var target = new GameObject("Player chase target");
            target.transform.SetParent(fixture.Root.transform);
            target.layer = LayerMask.NameToLayer("Player");
            target.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var targetCollider = target.AddComponent<BoxCollider2D>();
            targetCollider.size = new Vector2(1.55f, 2.6f);
            targetCollider.offset = Vector2.up * 1.3f;
            var chases = fixture.Enemies.Select(enemy => new ChaseAction {
                Agent = new BlackboardVariable<GameObject>(enemy.gameObject),
                Target = new BlackboardVariable<GameObject>(target),
                AttackRange = new BlackboardVariable<float>(1.5f),
                ChaseSpeed = new BlackboardVariable<float>(4f)
            }).ToArray();
            foreach (var chase in chases) typeof(ChaseAction).GetMethod("OnStart", Private).Invoke(chase, null);
            float until = Time.time + 8f;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline)
            {
                foreach (var chase in chases) typeof(ChaseAction).GetMethod("OnUpdate", Private).Invoke(chase, null);
                yield return PhysicsStep();
                if (fixture.Enemies.All(enemy => enemy.GetComponent<EnemyLocomotion>().IsAtHoldSlot(target.transform, 1.5f)))
                    break;
            }
            foreach (var chase in chases) typeof(ChaseAction).GetMethod("OnEnd", Private).Invoke(chase, null);
            var front = fixture.Enemies.Where(e => EnemyCombatRange.IsInAttackRange(e.gameObject, target, 1.5f)).ToArray();
            Assert.That(front.Any(e => e.rb.position.x < 0f), Is.True);
            Assert.That(front.Any(e => e.rb.position.x > 0f), Is.True);
            float[] positions = fixture.XPositions();
            for (int i = 1; i < positions.Length; i++) Assert.That(positions[i] - positions[i - 1], Is.GreaterThan(.25f));
            Assert.That(fixture.Enemies.All(e => e.bodyCollider.bounds.min.y > -.03f), Is.True);
            TestContext.WriteLine("Resumed chase slots: " + string.Join(", ", positions.Select(x => x.ToString("F3"))));
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SeparationCannotCrossAWallOrWalkOffALedge()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (bool wall in new[] { false, true })
        using (var fixture = new Fixture(2))
        {
            yield return fixture.Ready();
            fixture.Collapse();
            float rightEdge = fixture.Enemies[0].bodyCollider.bounds.max.x;
            BoxCollider2D obstacle = null;
            if (wall)
            {
                var go = new GameObject("Thin terrain wall");
                go.transform.SetParent(fixture.Root.transform);
                go.layer = LayerMask.NameToLayer("Ground");
                go.transform.position = new Vector3(rightEdge + .05f, 3f, 0f);
                obstacle = go.AddComponent<BoxCollider2D>();
                obstacle.size = new Vector2(.05f, 6f);
            }
            else
            {
                float edge = rightEdge + .05f;
                fixture.Floor.transform.position = new Vector3((edge - 10f) * .5f, -.5f, 0f);
                fixture.Floor.size = new Vector2(edge + 10f, 1f);
            }
            Physics2D.SyncTransforms();
            for (int i = 0; i < 45; i++)
            {
                yield return PhysicsStep();
                foreach (var enemy in fixture.Enemies)
                {
                    Assert.That(enemy.bodyCollider.bounds.min.y, Is.GreaterThanOrEqualTo(-.03f));
                    if (wall) Assert.That(enemy.bodyCollider.bounds.max.x, Is.LessThanOrEqualTo(obstacle.bounds.min.x + .005f));
                    else Assert.That(enemy.bodyCollider.bounds.center.x, Is.LessThanOrEqualTo(.04f));
                }
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ShinGroundComboReachesDummyAnd101WithAuthoredReactionTargets()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var table = AssetDatabase.LoadAssetAtPath<ActionResolverTable>(
            "Assets/Project/Character/Hero/test_0_Main/ActionResolverTable_main.asset");
        var action = table.comboActions.Single(combo => combo.actionId == "char-0-main.basic.0");
        foreach (string path in new[] { "enemy/EnemyCore/Enemy_Dummy.prefab", "enemy/Enm101/Enemy1.prefab" })
        {
            using (var fixture = new Fixture(1, path))
            {
                yield return fixture.Ready();
                var enemy = fixture.Enemies[0];
                enemy.rb.position = new Vector2(2.5f, enemy.rb.position.y);
                enemy.rb.linearVelocity = Vector2.zero;
                var hitSource = new GameObject("Shin combo reach test");
                hitSource.transform.SetParent(fixture.Root.transform);
                var shape = hitSource.AddComponent<BoxCollider2D>();
                shape.isTrigger = true;
                shape.enabled = false;
                var damage = hitSource.AddComponent<HitBoxDamage>();
                typeof(HitBoxDamage).GetField("sourceCategory", Private).SetValue(damage, AttackCategory.Normal);
                typeof(HitBoxDamage).GetField("sourceActionId", Private).SetValue(damage, action.actionId);
                int targetCount = 0;
                int snapCount = 0;
                Vector2 launchOrigin = default;

                for (ComboNode node = action.comboTree.rootNode; node != null; node = node.defaultNext)
                {
                    var step = node.stepData;
                    float began = Time.time;
                    float forward = step.motionData != null
                        ? step.motionData.keyframes.Where(key => key.taskType == MotionTaskType.Relative).Sum(key => key.position.x) : 0f;
                    hitSource.transform.position += Vector3.right * forward;
                    foreach (var hit in step.hitData.hitboxes.OfType<NormalHitbox>().OrderBy(hit => hit.startFrameOffset))
                    {
                        float at = began + (step.startupFrames + hit.startFrameOffset) / 60f;
                        while (Time.time < at) yield return PhysicsStep();
                        shape.offset = hit.offset;
                        shape.size = hit.size;
                        shape.enabled = true;
                        Physics2D.SyncTransforms();
                        Assert.That(shape.bounds.Intersects(enemy.bodyCollider.bounds), Is.True,
                            $"{path}, {step.stepName} frame {hit.startFrameOffset}: Shin's follow-up is out of reach.");
                        typeof(HitBoxDamage).GetField("currentNormalHitbox", Private).SetValue(damage, hit);
                        typeof(HitBoxDamage).GetField("sourceStepData", Private).SetValue(damage, step);
                        var data = (HitReactionData)typeof(HitBoxDamage).GetMethod("BuildReactionData", Private)
                            .Invoke(damage, new object[] { enemy, Vector2.right });
                        Assert.That(data.hasAuthoredReactionTarget, Is.EqualTo(!hit.useSnapToAnchor));
                        if (hit.useSnapToAnchor)
                        {
                            Assert.That(data.useSnapToAnchor && data.hasSnapWorldPosition, Is.True);
                            snapCount++;
                        }
                        else
                        {
                            targetCount++;
                            if (data.reactionKind == EnemyReactionKind.Knockup) launchOrigin = enemy.rb.position;
                        }
                        enemy.hitReactionHandler.StartReaction(data, hitSource.transform.position);
                        shape.enabled = false;
                    }
                    if (node.defaultNext != null)
                    {
                        float next = began + Mathf.Max(step.startupFrames + step.activeFrames, step.cancelWindowStartFrame) / 60f;
                        while (Time.time < next) yield return PhysicsStep();
                    }
                }

                Assert.That(targetCount, Is.EqualTo(3));
                Assert.That(snapCount, Is.EqualTo(1));
                float apex = launchOrigin.y;
                float xAtApex = launchOrigin.x;
                for (int i = 0; i < 40; i++)
                {
                    yield return PhysicsStep();
                    if (enemy.rb.position.y <= apex) continue;
                    apex = enemy.rb.position.y;
                    xAtApex = enemy.rb.position.x;
                }
                Assert.That(apex - launchOrigin.y, Is.EqualTo(1.55f).Within(.12f), path);
                Assert.That(enemy.rb.position.x - launchOrigin.x, Is.EqualTo(.65f).Within(.06f), path);
                Assert.That(enemy.rb.position.x, Is.GreaterThan(xAtApex + .05f), path);
                Assert.That(enemy.bodyCollider.bounds.min.y, Is.EqualTo(fixture.Floor.bounds.max.y).Within(.15f), path);
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ShinAirAttackPreservesLegacyTravelOnDummyAnd101()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var table = AssetDatabase.LoadAssetAtPath<ActionResolverTable>(
            "Assets/Project/Character/Hero/test_0_Main/ActionResolverTable_main.asset");
        var action = table.comboActions.Single(combo => combo.actionId == "char-0-main.basic.1");
        var node = action.comboTree.rootNode;
        Assert.That(node.defaultNext, Is.Null);
        var step = node.stepData;
        var hit = (NormalHitbox)step.hitData.hitboxes.Single();
        Assert.That(hit.useAuthoredReactionTarget, Is.True);
        Assert.That(hit.useSnapToAnchor || hit.breaksGrab, Is.False);
        Assert.That(hit.reactionKind, Is.EqualTo(EnemyReactionKind.Knockback));
        Assert.That(hit.reactionTargetOffset, Is.EqualTo(new Vector2(.75f, 0f)));
        var legacy = JsonUtility.FromJson<NormalHitbox>(JsonUtility.ToJson(hit));
        legacy.useAuthoredReactionTarget = false;

        foreach (string path in new[] { "enemy/EnemyCore/Enemy_Dummy.prefab", "enemy/Enm101/Enemy1.prefab" })
        {
            using var fixture = new Fixture(1, path);
            yield return fixture.Ready();
            var enemy = fixture.Enemies[0];
            float groundY = enemy.rb.position.y;
            var source = new GameObject("Shin air attack");
            source.transform.SetParent(fixture.Root.transform);
            var shape = source.AddComponent<BoxCollider2D>();
            shape.isTrigger = true;
            shape.enabled = false;
            var damage = source.AddComponent<HitBoxDamage>();
            typeof(HitBoxDamage).GetField("sourceCategory", Private).SetValue(damage, AttackCategory.Normal);
            typeof(HitBoxDamage).GetField("sourceActionId", Private).SetValue(damage, action.actionId);
            typeof(HitBoxDamage).GetField("sourceStepData", Private).SetValue(damage, step);

            foreach (float facing in new[] { 1f, -1f })
            {
                float targetHeight = facing > 0f ? 0f : .6f;
                float legacyTravel = 0f;
                foreach (var frame in new[] { legacy, hit })
                {
                    enemy.hitReactionHandler.ForceClearReaction();
                    enemy.rb.position = new Vector2(2.8f * facing, groundY + targetHeight);
                    enemy.rb.linearVelocity = Vector2.zero;
                    Vector2 origin = enemy.rb.position;
                    source.transform.position = new Vector2(.65f * facing, .55f);
                    shape.offset = new Vector2(frame.offset.x * facing, frame.offset.y);
                    shape.size = frame.size;
                    shape.enabled = true;
                    Physics2D.SyncTransforms();
                    Assert.That(shape.Distance(enemy.bodyCollider).isOverlapped, Is.True, path);
                    typeof(HitBoxDamage).GetField("currentNormalHitbox", Private).SetValue(damage, frame);
                    var data = (HitReactionData)typeof(HitBoxDamage).GetMethod("BuildReactionData", Private)
                        .Invoke(damage, new object[] { enemy, Vector2.right * facing });
                    Assert.That(data.hasAuthoredReactionTarget, Is.EqualTo(frame == hit));
                    enemy.hitReactionHandler.StartReaction(data, source.transform.position);
                    shape.enabled = false;

                    float highestY = origin.y;
                    for (int i = 0; i < 40; i++)
                    {
                        yield return PhysicsStep();
                        highestY = Mathf.Max(highestY, enemy.rb.position.y);
                    }

                    float travel = (enemy.rb.position.x - origin.x) * facing;
                    if (frame == legacy)
                        legacyTravel = travel;
                    else
                    {
                        Assert.That(travel, Is.EqualTo(.75f).Within(.06f), path);
                        Assert.That(travel, Is.EqualTo(legacyTravel).Within(.10f), path);
                        Assert.That(highestY, Is.LessThan(origin.y + .12f), path + ": this hit must not launch.");
                        Assert.That(enemy.bodyCollider.bounds.min.y, Is.EqualTo(fixture.Floor.bounds.max.y).Within(.15f), path);
                    }
                }
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ShinAttack4RecoilKeepsDummyAnd101InReachForAttack5GrabTransition()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var table = AssetDatabase.LoadAssetAtPath<ActionResolverTable>(
            "Assets/Project/Character/Hero/test_0_Main/ActionResolverTable_main.asset");
        var ground = table.comboActions.Single(action => action.actionId == "char-0-main.basic.0");
        var attack3 = ground.comboTree.rootNode.defaultNext.defaultNext;
        var attack4 = attack3.routes.Single(route => route.targetNode.stepData.stepName == "Attack4").targetNode;
        var step = attack4.stepData;
        var normals = step.hitData.hitboxes.OfType<NormalHitbox>().OrderBy(hit => hit.startFrameOffset).ToArray();
        var opener = normals.Single(hit => hit.startFrameOffset == 3);
        var snap = normals.Single(hit => hit.startFrameOffset == 6);
        var recoil = normals.Single(hit => hit.startFrameOffset == 11);
        var flinch = step.hitData.hitboxes.OfType<NormalHitbox>().Single(hit => hit.startFrameOffset == 14);
        var legacyOpener = JsonUtility.FromJson<NormalHitbox>(JsonUtility.ToJson(opener));
        legacyOpener.useAuthoredReactionTarget = false;
        var legacyRecoil = JsonUtility.FromJson<NormalHitbox>(JsonUtility.ToJson(recoil));
        legacyRecoil.useAuthoredReactionTarget = false;
        var grab = step.hitData.hitboxes.OfType<GrabHitbox>().Single();
        var attack5 = step.triggerWindows.Single(window => window.targetNode.stepData.stepName == "Attack5").targetNode;
        Assert.That(opener.useAuthoredReactionTarget && !opener.useSnapToAnchor, Is.True);
        Assert.That(opener.reactionTargetOffset, Is.EqualTo(Vector2.right));
        Assert.That(recoil.useAuthoredReactionTarget && !recoil.useSnapToAnchor, Is.True);
        Assert.That(recoil.reactionTargetOffset, Is.EqualTo(new Vector2(.75f, 0f)));
        Assert.That(flinch.useAuthoredReactionTarget, Is.True);
        Assert.That(flinch.reactionTargetOffset, Is.EqualTo(Vector2.zero));
        Assert.That(snap.useSnapToAnchor && !snap.useAuthoredReactionTarget, Is.True);
        Assert.That(attack5.stepData.hitData.hitboxes.OfType<NormalHitbox>()
            .All(hit => hit.useSnapToAnchor && hit.breaksGrab && !hit.useAuthoredReactionTarget), Is.True);

        foreach (string path in new[] { "enemy/EnemyCore/Enemy_Dummy.prefab", "enemy/Enm101/Enemy1.prefab" })
        {
            using var fixture = new Fixture(1, path);
            yield return fixture.Ready();
            var enemy = fixture.Enemies[0];
            Vector2 origin = new Vector2(3f, enemy.rb.position.y);
            var attacker = new GameObject("Shin Attack4");
            attacker.transform.SetParent(fixture.Root.transform);
            attacker.transform.localPosition = Vector3.left * .3f;
            var combat = attacker.AddComponent<PlayerCombat>();
            var shape = attacker.AddComponent<BoxCollider2D>();
            shape.isTrigger = true;
            shape.enabled = false;
            var damage = attacker.AddComponent<HitBoxDamage>();
            var resolver = new ActionResolver();
            resolver.ForceTransitionToNode(attack4);

            ResetTarget();
            HitNormal(legacyOpener);
            for (int i = 0; i < 70; i++) yield return PhysicsStep();
            float legacyOpenerTravel = enemy.rb.position.x - origin.x;
            TestContext.WriteLine($"{path}: frame 3 legacy full recoil = {legacyOpenerTravel:F3}");

            ResetTarget();
            HitNormal(opener);
            for (int i = 0; i < 70; i++) yield return PhysicsStep();
            float openerTravel = enemy.rb.position.x - origin.x;
            Assert.That(openerTravel, Is.EqualTo(1f).Within(.06f), path);
            Assert.That(openerTravel, Is.EqualTo(legacyOpenerTravel).Within(.06f), path);

            ResetTarget();
            HitNormal(legacyRecoil);
            for (int i = 0; i < 40; i++) yield return PhysicsStep();
            float legacyTravel = enemy.rb.position.x - origin.x;
            TestContext.WriteLine($"{path}: frame 11 legacy full recoil = {legacyTravel:F3}");

            ResetTarget();
            HitNormal(recoil);
            for (int i = 0; i < 40; i++) yield return PhysicsStep();
            float authoredTravel = enemy.rb.position.x - origin.x;
            Assert.That(authoredTravel, Is.EqualTo(.75f).Within(.06f), path);
            Assert.That(authoredTravel, Is.EqualTo(legacyTravel).Within(.06f), path);

            ResetTarget();
            int frame = normals[0].startFrameOffset;
            float recoilOriginX = 0f;
            foreach (var normal in normals)
            {
                yield return WaitFrames(normal.startFrameOffset - frame);
                frame = normal.startFrameOffset;
                if (normal == snap)
                {
                    float travel = enemy.rb.position.x - origin.x;
                    TestContext.WriteLine($"{path}: frame 3 to 6 travel = {travel:F3}");
                    Assert.That(travel, Is.InRange(.02f, openerTravel + .03f), path);
                }
                if (normal == recoil) recoilOriginX = enemy.rb.position.x;
                if (normal == flinch)
                {
                    float travel = enemy.rb.position.x - recoilOriginX;
                    TestContext.WriteLine($"{path}: frame 11 to 14 travel = {travel:F3}");
                    Assert.That(travel, Is.InRange(.02f, authoredTravel + .03f), path);
                }
                HitNormal(normal);
                Assert.That(enemy.LastHitAppliedReaction, Is.True, $"{path}: frame {frame}");
                if (normal == snap)
                {
                    var applied = (HitReactionData)typeof(EnemyHitReactionHandler).GetField("currentReaction", Private)
                        .GetValue(enemy.hitReactionHandler);
                    Assert.That(applied.useSnapToAnchor && applied.hasSnapWorldPosition, Is.True, path);
                }
            }
            float originX = enemy.rb.position.x;
            Assert.That(enemy.LastHitAppliedReaction, Is.True, path);
            Assert.That(resolver.ResolveTriggers(default, flinch.startFrameOffset), Is.Null, path);
            yield return WaitFrames(grab.startFrameOffset - flinch.startFrameOffset);
            Assert.That(enemy.rb.position.x, Is.EqualTo(originX).Within(.03f), path);

            damage.ResetActivationState();
            damage.SetGrabData(0f, false, grab, false, AttackCategory.Normal, "Ground", step, null, ground.actionId);
            Hit(grab.offset, grab.size);
            Assert.That(enemy.IsGrabbed && combat.IsHoldingEnemy(enemy), Is.True, path);
            var context = new StateContext { input = new InputContext { hasGrabbedEnemy = combat.HasGrabbedEnemy() } };
            Assert.That(resolver.ResolveTriggers(context, grab.startFrameOffset), Is.SameAs(attack5.stepData), path);

            IEnumerator WaitFrames(int count)
            {
                float deadline = Time.fixedTime + count / CombatCore.FrameDataConstants.LOGIC_FPS;
                while (Time.fixedTime + .0001f < deadline) yield return PhysicsStep();
            }

            void ResetTarget()
            {
                enemy.hitReactionHandler.ForceClearReaction();
                enemy.rb.position = origin;
                enemy.rb.linearVelocity = Vector2.zero;
            }

            void HitNormal(NormalHitbox hit)
            {
                damage.ResetActivationState();
                damage.SetHitData(1f, false, false, 0f, 0f, hit, AttackCategory.Normal, "Ground", step, null, ground.actionId);
                Hit(hit.offset, hit.size);
            }

            void Hit(Vector2 offset, Vector2 size)
            {
                shape.offset = offset;
                shape.size = size;
                shape.enabled = true;
                Physics2D.SyncTransforms();
                Assert.That(shape.Distance(enemy.bodyCollider).isOverlapped, Is.True, path);
                damage.ProcessHit(enemy, enemy.bodyCollider);
                shape.enabled = false;
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ReiAirAttackPreservesLegacyTravelOnGroundedAndAirborneTargets()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        LoadReiGroundCombo(out var rei);
        var action = rei.actionResolverTable.comboActions.Single(combo => combo.actionId == "char-1-camera_girl.basic.1");
        var node = action.comboTree.rootNode;
        Assert.That(node.defaultNext, Is.Null);
        var step = node.stepData;
        var hit = (NormalHitbox)step.hitData.hitboxes.Single();
        Assert.That(hit.startFrameOffset, Is.Zero);
        Assert.That(hit.useSnapToAnchor || hit.breaksGrab, Is.False);
        Assert.That(hit.reactionKind, Is.EqualTo(EnemyReactionKind.Knockback));
        Assert.That(hit.useAuthoredReactionTarget, Is.True);
        Assert.That(hit.reactionTargetOffset, Is.EqualTo(new Vector2(.65f, 0f)));
        Assert.That(hit.reactionTargetTravelFrames, Is.EqualTo(6));

        var legacy = JsonUtility.FromJson<NormalHitbox>(JsonUtility.ToJson(hit));
        legacy.useAuthoredReactionTarget = false;
        foreach (string path in new[] { "enemy/EnemyCore/Enemy_Dummy.prefab", "enemy/Enm101/Enemy1.prefab" })
        {
            using var fixture = new Fixture(1, path);
            yield return fixture.Ready();
            var enemy = fixture.Enemies[0];
            float groundY = enemy.rb.position.y;
            var source = new GameObject("Rei air attack probe");
            source.transform.position = new Vector2(.65f, .55f);
            var shape = source.AddComponent<BoxCollider2D>();
            shape.isTrigger = true;
            shape.enabled = false;
            var damage = source.AddComponent<HitBoxDamage>();
            try
            {
                foreach (bool airborne in new[] { false, true })
                {
                    string context = $"{path} airborne={airborne}";
                    float legacyTravel = 0f;
                    foreach (var frame in new[] { legacy, hit })
                    {
                        enemy.hitReactionHandler.ForceClearReaction();
                        enemy.rb.position = new Vector2(2.8f, groundY + (airborne ? .6f : 0f));
                        enemy.rb.linearVelocity = Vector2.zero;
                        Vector2 origin = enemy.rb.position;
                        shape.offset = frame.offset;
                        shape.size = frame.size;
                        shape.enabled = true;
                        Physics2D.SyncTransforms();
                        Assert.That(enemy.hitReactionHandler.IsGroundedForCombat, Is.EqualTo(!airborne), context);
                        Assert.That(shape.Distance(enemy.bodyCollider).isOverlapped, Is.True, context);
                        damage.ResetActivationState();
                        damage.SetHitData(1f, false, false, 0f, 0f, frame, AttackCategory.Normal, action.actionName,
                            step, null, action.actionId);
                        damage.ProcessHit(enemy, enemy.bodyCollider);
                        Assert.That(enemy.hitReactionHandler.IsInReaction, Is.True, context);
                        var reaction = (HitReactionData)typeof(EnemyHitReactionHandler)
                            .GetField("currentReaction", Private).GetValue(enemy.hitReactionHandler);
                        Assert.That(reaction.hasAuthoredReactionTarget, Is.EqualTo(frame == hit), context);
                        shape.enabled = false;

                        float highestY = origin.y;
                        for (int i = 0; i < 45; i++)
                        {
                            yield return PhysicsStep();
                            highestY = Mathf.Max(highestY, enemy.rb.position.y);
                        }
                        float travel = enemy.rb.position.x - origin.x;
                        TestContext.WriteLine($"{context} authored={frame.useAuthoredReactionTarget} X={travel:F3} rise={highestY - origin.y:F3}");
                        if (frame == legacy)
                            legacyTravel = travel;
                        else
                            Assert.That(travel, Is.EqualTo(legacyTravel).Within(.02f), context);
                        Assert.That(highestY - origin.y, Is.LessThan(.12f), context + ": knockback must not launch.");
                        Assert.That(enemy.bodyCollider.bounds.min.y, Is.EqualTo(fixture.Floor.bounds.max.y).Within(.15f), context);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ReiGroundComboMeasuresLegacyPlacementAndKeepsFollowupsInReach()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var action = LoadReiGroundCombo(out _);
        var steps = GetDefaultComboSteps(action);
        Assert.That(steps.Select(step => step.stepName), Is.EqualTo(new[] { "Attack1", "Attack2", "Attack3", "Attack4", "Attack5" }));
        var groundHits = steps.Take(4).SelectMany(step => step.hitData.hitboxes.OfType<NormalHitbox>()).ToArray();
        Assert.That(groundHits.Length, Is.EqualTo(9));
        Assert.That(groundHits.All(hit => hit.useAuthoredReactionTarget && hit.reactionTargetOffset.y == 0f), Is.True);
        var finishers = steps[4].hitData.hitboxes.OfType<NormalHitbox>().ToArray();
        Assert.That(finishers.Select(hit => hit.startFrameOffset), Is.EqualTo(new[] { 0, 20 }));
        Assert.That(finishers.All(hit => hit.useSnapToAnchor && !hit.useAuthoredReactionTarget), Is.True);

        yield return VerifyGroundPositionCombo(action, steps, 1.15f,
            "enemy/EnemyCore/Enemy_Dummy.prefab", "enemy/Enm101/Enemy1.prefab");
        yield return new ExitPlayMode();
    }

    [Test]
    public void ReiAttack4UsesOriginalHitDataDuringQteOnly()
    {
        var action = LoadReiGroundCombo(out var rei);
        var table = rei.actionResolverTable;
        var attack4 = GetDefaultComboSteps(action).Single(step => step.stepName == "Attack4");
        Assert.That(rei.qteSwap.TryResolveIntro(table, out _, out var qteStep), Is.True);
        Assert.That(qteStep, Is.SameAs(attack4));
        Assert.That(rei.qteSwap.GetIntroComboTree(table)?.rootNode, Is.Null);

        var ground = attack4.hitData.hitboxes.Cast<NormalHitbox>().ToArray();
        var qte = attack4.qteHitDataOverride.hitboxes.Cast<NormalHitbox>().ToArray();
        Assert.That(ground.Length, Is.EqualTo(qte.Length));
        Assert.That(qte.Select(hit => hit.startFrameOffset), Is.EqualTo(new[] { 7, 11, 16, 19, 24, 25 }));
        Assert.That(qte.Select(hit => hit.horizontalSpeed), Is.EqualTo(new[] { 10f, 3f, 3f, 0f, 0f, 4f }));

        var probe = new GameObject("Rei QTE hit data probe");
        try
        {
            var combat = probe.AddComponent<PlayerCombat>();
            var damage = probe.AddComponent<HitBoxDamage>();
            combat.hitBoxDamage = damage;
            foreach (bool isQte in new[] { false, true })
            {
                combat.ExecuteStep(attack4, 4, false, AttackCategory.Normal, isAutoChain: true, isQteIntro: isQte);
                for (int i = 0; i < ground.Length; i++)
                {
                    combat.ProcessFrameFromState(attack4.ActivePhaseStart + ground[i].startFrameOffset, ActionPhase.Active);
                    Assert.That(damage.IsConfiguredFor(isQte ? qte[i] : ground[i]), Is.True,
                        $"QTE={isQte}, hit {ground[i].startFrameOffset}");
                }
            }

            for (int i = 0; i < ground.Length; i++)
            {
                Assert.That(ground[i], Is.Not.SameAs(qte[i]));
                Assert.That(qte[i].useAuthoredReactionTarget || qte[i].useSnapToAnchor, Is.False);
                Assert.That(ground[i].reactionTargetTravelFrames, Is.EqualTo(6));
                var comparable = JsonUtility.FromJson<NormalHitbox>(JsonUtility.ToJson(ground[i]));
                comparable.useAuthoredReactionTarget = false;
                comparable.reactionTargetOffset = Vector2.zero;
                comparable.reactionTargetTravelFrames = 0;
                Assert.That(JsonUtility.ToJson(comparable), Is.EqualTo(JsonUtility.ToJson(qte[i])));
            }
        }
        finally
        {
            Object.DestroyImmediate(probe);
        }
    }

    private static ComboAction LoadReiGroundCombo(out CharacterObject rei)
    {
        rei = AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_1_CameraGirl/CharacterObject_cg.asset");
        return rei.actionResolverTable.comboActions.Single(combo => combo.actionId == "char-1-camera_girl.basic.0");
    }

    private static List<AttackStepData> GetDefaultComboSteps(ComboAction action)
    {
        var steps = new List<AttackStepData>();
        for (var node = action.comboTree.rootNode; node != null; node = node.defaultNext)
            steps.Add(node.stepData);
        return steps;
    }

    private static IEnumerator VerifyGroundPositionCombo(
        ComboAction action, IReadOnlyList<AttackStepData> steps, float startingDistance, params string[] targetPrefabs)
    {
        var candidates = steps.SelectMany(step => step.hitData.hitboxes.OfType<NormalHitbox>()
            .Where(hit => hit.useAuthoredReactionTarget).Select(hit => (step, hit))).ToArray();
        Assert.That(candidates, Is.Not.Empty);
        foreach (string path in targetPrefabs)
        {
            using var fixture = new Fixture(1, path);
            yield return fixture.Ready();
            var enemy = fixture.Enemies[0];
            float floorY = enemy.rb.position.y;
            var source = new GameObject("Ground combo melee probe");
            source.transform.SetParent(fixture.Root.transform);
            var shape = source.AddComponent<BoxCollider2D>();
            shape.isTrigger = true;
            shape.enabled = false;
            var damage = source.AddComponent<HitBoxDamage>();

            foreach (var (step, hit) in candidates)
            {
                var legacy = JsonUtility.FromJson<NormalHitbox>(JsonUtility.ToJson(hit));
                legacy.useAuthoredReactionTarget = false;
                Vector2 legacyTravel = default;
                foreach (var frame in new[] { legacy, hit })
                {
                    enemy.hitReactionHandler.ForceClearReaction();
                    enemy.rb.position = new Vector2(Mathf.Max(.5f, hit.offset.x), floorY);
                    enemy.rb.linearVelocity = Vector2.zero;
                    source.transform.localPosition = Vector3.zero;
                    Vector2 origin = enemy.rb.position;
                    Assert.That(TryHit(step, frame), Is.True, step.stepName);
                    float highest = origin.y;
                    for (int i = 0; i < 55; i++)
                    {
                        yield return PhysicsStep();
                        highest = Mathf.Max(highest, enemy.rb.position.y);
                    }
                    var travel = new Vector2(enemy.rb.position.x - origin.x, highest - origin.y);
                    TestContext.WriteLine($"{path} {step.stepName}:{hit.startFrameOffset} authored={frame == hit} X={travel.x:F3} Y={travel.y:F3}");
                    if (frame == legacy)
                        legacyTravel = travel;
                    else
                        Assert.That(Vector2.Distance(travel, legacyTravel), Is.LessThan(.06f), step.stepName);
                }
            }

            enemy.hitReactionHandler.ForceClearReaction();
            enemy.rb.position = new Vector2(startingDistance, floorY);
            enemy.rb.linearVelocity = Vector2.zero;
            Vector2 stepOrigin = Vector2.zero;
            var connections = new List<string>();
            foreach (var step in steps)
            {
                var hits = step.hitData.hitboxes.OfType<NormalHitbox>().OrderBy(hit => hit.startFrameOffset).ToArray();
                var landed = new HashSet<NormalHitbox>();
                int duration = Mathf.Max(step.startupFrames + step.activeFrames, step.cancelWindowStartFrame);
                for (int frame = 1; frame <= duration; frame++)
                {
                    source.transform.localPosition = stepOrigin + MotionAt(step, frame - 1);
                    int active = frame - step.ActivePhaseStart;
                    var current = hits.LastOrDefault(hit => hit.startFrameOffset <= active);
                    if (current != null && active < current.startFrameOffset + current.activeFrames &&
                        !landed.Contains(current) && TryHit(step, current))
                    {
                        landed.Add(current);
                        connections.Add($"{step.stepName}:{current.startFrameOffset}");
                    }
                    yield return PhysicsStep();
                }
                stepOrigin += MotionAt(step, duration);
            }
            TestContext.WriteLine($"{path}: {string.Join(", ", connections)}");
            var expected = steps.SelectMany(step => step.hitData.hitboxes.OfType<NormalHitbox>()
                .OrderBy(hit => hit.startFrameOffset).Select(hit => $"{step.stepName}:{hit.startFrameOffset}"));
            Assert.That(connections, Is.EqualTo(expected), path);

            bool TryHit(AttackStepData step, NormalHitbox hit)
            {
                shape.offset = hit.offset;
                shape.size = hit.size;
                shape.enabled = true;
                Physics2D.SyncTransforms();
                bool overlaps = shape.Distance(enemy.bodyCollider).isOverlapped;
                if (overlaps)
                {
                    damage.ResetActivationState();
                    damage.SetHitData(1f, false, false, 0f, 0f, hit, AttackCategory.Normal, "Ground", step, null, action.actionId);
                    source.transform.SetParent(null, true);
                    damage.ProcessHit(enemy, enemy.bodyCollider);
                    source.transform.SetParent(fixture.Root.transform, true);
                }
                shape.enabled = false;
                return overlaps;
            }
        }
        static Vector2 MotionAt(AttackStepData step, int frame)
        {
            Vector2 offset = Vector2.zero;
            if (step.motionData == null) return offset;
            foreach (var key in step.motionData.keyframes)
            {
                Assert.That(key.taskType, Is.EqualTo(MotionTaskType.Relative));
                if (frame < key.startFrame) continue;
                float progress = key.startFrame == key.endFrame ? 1f :
                    Mathf.Clamp01((frame - key.startFrame) / (float)(key.endFrame - key.startFrame));
                offset += (Vector2)key.position * progress;
            }
            return offset;
        }
    }

    [UnityTest]
    public IEnumerator SkillAndUltimatePositionMigration() => ProbePositionMigration("skills");

    [UnityTest]
    public IEnumerator DimensionCutUnboundPositionMigration() => ProbePositionMigration("cut");

    [UnityTest]
    public IEnumerator ProjectileAndAngeliquePositionMigration() => ProbePositionMigration("projectiles");

    [UnityTest]
    public IEnumerator QtePositionMigration() => ProbePositionMigration("qte");

    [UnityTest]
    public IEnumerator UranagiStillConnectsToItsSnapAtTheSameFrame()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(
            HeroRoot + "test_15_goenitz/CharacterObject_Goenitz.asset");
        Assert.That(character.qteSwap.TryResolveIntro(character.actionResolverTable, out var action, out var step), Is.True);
        Assert.That(action.actionId, Is.EqualTo("goenitz.qte"));
        Assert.That(action.comboTree.rootNode.defaultNext, Is.Null);
        var hits = step.hitData.hitboxes.OfType<NormalHitbox>().OrderBy(h => h.startFrameOffset).ToArray();
        Assert.That(hits.Select(h => h.startFrameOffset), Is.EqualTo(new[] { 0, 12 }));
        Assert.That(hits[0].useAuthoredReactionTarget && hits[1].useSnapToAnchor, Is.True);
        var oldOpening = JsonUtility.FromJson<NormalHitbox>(JsonUtility.ToJson(hits[0]));
        oldOpening.useAuthoredReactionTarget = false;
        var movement = step.motionData.keyframes.Single();
        Assert.That(movement.taskType, Is.EqualTo(MotionTaskType.Relative));
        Assert.That(movement.easing, Is.EqualTo(LeanTweenType.easeInOutQuint));

        foreach (string prefab in new[] { "enemy/EnemyCore/Enemy_Dummy.prefab", "enemy/Enm101/Enemy1.prefab" })
        {
            using var fixture = new Fixture(1, prefab);
            yield return fixture.Ready();
            var enemy = fixture.Enemies[0];
            float floorY = enemy.rb.position.y;
            var source = new GameObject("Uranagi collision probe");
            var shape = source.AddComponent<BoxCollider2D>();
            shape.isTrigger = true;
            var damage = source.AddComponent<HitBoxDamage>();
            try
            {
                foreach (float gap in new[] { .35f, 2.4f })
                {
                    int oldSnapFrame = -1;
                    foreach (var opening in new[] { oldOpening, hits[0] })
                    {
                        enemy.hitReactionHandler.ForceClearReaction();
                        enemy.rb.position = new Vector2(gap, floorY);
                        enemy.rb.linearVelocity = Vector2.zero;
                        bool opened = false;
                        int snapFrame = -1;
                        for (int frame = 1; frame <= step.TotalFrames && snapFrame < 0; frame++)
                        {
                            float progress = Mathf.InverseLerp(movement.startFrame, movement.endFrame, frame - 1);
                            // PlayerMovement's easing switch falls back to linear for easeInOutQuint.
                            source.transform.position = movement.position * progress;
                            int active = frame - step.ActivePhaseStart;
                            var hit = active < hits[1].startFrameOffset ? opening : hits[1];
                            if (active >= hit.startFrameOffset && active < hit.startFrameOffset + hit.activeFrames
                                && (hit != opening || !opened))
                            {
                                shape.offset = hit.offset;
                                shape.size = hit.size;
                                shape.enabled = true;
                                Physics2D.SyncTransforms();
                                if (shape.Distance(enemy.bodyCollider).isOverlapped)
                                {
                                    damage.ResetActivationState();
                                    damage.SetHitData(1f, false, false, 0f, 0f, hit, AttackCategory.Normal, action.actionName,
                                        step, null, action.actionId);
                                    damage.ProcessHit(enemy, enemy.bodyCollider);
                                    if (hit == opening) opened = true;
                                    else snapFrame = frame;
                                }
                                shape.enabled = false;
                            }
                            yield return PhysicsStep();
                        }
                        Assert.That(opened && snapFrame >= 0, Is.True, $"{prefab} gap={gap}");
                        if (opening == oldOpening) oldSnapFrame = snapFrame;
                        else Assert.That(snapFrame, Is.EqualTo(oldSnapFrame), $"{prefab} gap={gap}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }
        yield return new ExitPlayMode();
    }

    private const string HeroRoot = "Assets/Project/Character/Hero/";
    private const string ShinTable = "test_0_Main/ActionResolverTable_main.asset";
    private const string ReiTable = "test_1_CameraGirl/ActionResolverTable_cg.asset";
    private const string AngeliqueTable = "test_15_goenitz/ActionResolverTable_char_5_goenitz.asset";

    private static IEnumerator ProbePositionMigration(string group)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var hits = GetPositionMigrationHits(group).ToArray();
        Assert.That(hits, Is.Not.Empty);
        int expectedMigrated = group switch { "skills" => 5, "cut" => 1, "projectiles" => 29, "qte" => 1, _ => 0 };
        Assert.That(hits.Count(entry => entry.hit.useAuthoredReactionTarget && entry.hit.preserveReactionMotionTiming),
            Is.EqualTo(expectedMigrated));
        float priorScale = Time.timeScale;
        Time.timeScale = 6f;
        try
        {
            foreach (string prefab in new[] { "enemy/EnemyCore/Enemy_Dummy.prefab", "enemy/Enm101/Enemy1.prefab" })
            {
                using var fixture = new Fixture(1, prefab);
                yield return fixture.Ready();
                var enemy = fixture.Enemies[0];
                float floorY = enemy.rb.position.y;
                var source = new GameObject("Reaction migration probe");
                source.transform.SetParent(fixture.Root.transform);
                var damage = source.AddComponent<HitBoxDamage>();
                var build = typeof(HitBoxDamage).GetMethod("BuildReactionData", Private);
                var current = typeof(EnemyHitReactionHandler).GetField("currentReaction", Private);

                foreach (var (key, category, step, hit) in hits)
                {
                    var legacy = JsonUtility.FromJson<NormalHitbox>(JsonUtility.ToJson(hit));
                    legacy.useAuthoredReactionTarget = false;
                    foreach (bool airborne in new[] { false, true })
                    foreach (float distance in category == AttackCategory.Normal ? new[] { .25f, 3f } : new[] { 3f })
                    {
                        Vector2 original = default;
                        int originalTiming = 0;
                        EnemyReactionKind originalKind = default;
                        foreach (var input in hit.useAuthoredReactionTarget ? new[] { legacy, hit } : new[] { legacy })
                        {
                            enemy.hitReactionHandler.ForceClearReaction();
                            enemy.rb.position = new Vector2(distance, floorY + (airborne ? .6f : 0f));
                            enemy.rb.linearVelocity = Vector2.zero;
                            Physics2D.SyncTransforms();
                            Vector2 origin = enemy.rb.position;
                            damage.SetHitData(1f, false, false, 0f, 0f, input, category, key, step, null, key);
                            var data = (HitReactionData)build.Invoke(damage, new object[] { enemy, Vector2.right });
                            enemy.hitReactionHandler.StartReaction(data, source.transform.position);
                            var actual = (HitReactionData)current.GetValue(enemy.hitReactionHandler);
                            float apex = 0f;
                            float travel = 0f;
                            float sampleUntil = Time.fixedTime + Mathf.Max(1.1f, actual.TotalReactionFrames / 60f + .65f);
                            while (Time.fixedTime < sampleUntil)
                            {
                                yield return PhysicsStep();
                                travel = enemy.rb.position.x - origin.x;
                                apex = Mathf.Max(apex, enemy.rb.position.y - origin.y);
                            }
                            var result = new Vector2(travel, apex);
                            string context = $"{key} {prefab} air={airborne} gap={distance}";
                            TestContext.WriteLine(System.FormattableString.Invariant(
                                $"MEASURE {context} authored={input.useAuthoredReactionTarget} X={travel:F4} Y={apex:F4} timing={actual.TotalReactionFrames} kind={actual.reactionKind}"));
                            if (input == legacy)
                            {
                                original = result;
                                originalTiming = actual.TotalReactionFrames;
                                originalKind = actual.reactionKind;
                            }
                            else
                            {
                                bool needsPointBlankSeparation = category == AttackCategory.Normal && distance < 1f
                                    && original.x > hit.reactionTargetOffset.x + .01f;
                                Assert.That(actual.hasAuthoredReactionTarget, Is.EqualTo(!needsPointBlankSeparation), context);
                                Assert.That(result.x, Is.EqualTo(original.x).Within(.055f), context);
                                Assert.That(result.y, Is.EqualTo(original.y).Within(.08f), context);
                                Assert.That(actual.TotalReactionFrames, Is.EqualTo(originalTiming), context);
                                Assert.That(actual.reactionKind, Is.EqualTo(originalKind), context);
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            Time.timeScale = priorScale;
        }
        yield return new ExitPlayMode();
    }

    private static IEnumerable<(string key, AttackCategory category, AttackStepData step, NormalHitbox hit)>
        GetPositionMigrationHits(string group)
    {
        var paths = new[] { ShinTable, ReiTable, AngeliqueTable };
        foreach (string path in paths)
        {
            var table = AssetDatabase.LoadAssetAtPath<ActionResolverTable>(HeroRoot + path);
            var actions = group switch
            {
                "skills" when path != AngeliqueTable => table.skillActions.Cast<CombatAction>().Concat(table.ultimateActions),
                "cut" when path == ShinTable => table.standaloneActions.Where(action => action.actionId == "main.dash.dimension_cut").Cast<CombatAction>(),
                "projectiles" when path == AngeliqueTable => table.comboActions.Cast<CombatAction>().Concat(table.skillActions).Concat(table.ultimateActions),
                "qte" => table.qteActions.Cast<CombatAction>(),
                _ => Enumerable.Empty<CombatAction>()
            };
            foreach (var action in actions)
            {
                var pending = new Queue<ComboNode>();
                var seen = new HashSet<ComboNode>();
                pending.Enqueue(action.comboTree?.rootNode);
                while (pending.Count > 0)
                {
                    var node = pending.Dequeue();
                    if (node == null || !seen.Add(node))
                        continue;
                    var step = node.stepData;
                    if (step?.hitData?.hitboxes != null)
                        foreach (var hit in step.hitData.hitboxes.OfType<NormalHitbox>().Where(CanProbePosition))
                            if (!hit.useAuthoredReactionTarget || group != "projectiles" || step.stepName != "Yamidokoku Exit")
                                yield return ($"{action.actionId}/{step.stepName}/{hit.startFrameOffset}", action.Category, step, hit);
                    pending.Enqueue(node.defaultNext);
                    pending.Enqueue(node.onGrabSuccessNode);
                    if (node.routes != null)
                        foreach (var route in node.routes)
                            pending.Enqueue(route?.targetNode);
                    if (step?.triggerWindows != null)
                        foreach (var trigger in step.triggerWindows)
                            pending.Enqueue(trigger?.targetNode);
                }
            }
        }

        if (group == "qte")
        {
            var step = GetDefaultComboSteps(LoadReiGroundCombo(out _)).Single(s => s.stepName == "Attack4");
            foreach (var hit in step.ResolveHitData(true).hitboxes.OfType<NormalHitbox>().Where(CanProbePosition))
                yield return ($"rei.qte/{hit.startFrameOffset}", AttackCategory.Normal, step, hit);
        }
        if (group != "projectiles")
            yield break;

        foreach (string guid in AssetDatabase.FindAssets("t:ProjectileData", paths
                     .Select(path => HeroRoot + path.Substring(0, path.IndexOf('/'))).ToArray()))
        {
            var projectile = AssetDatabase.LoadAssetAtPath<ProjectileData>(AssetDatabase.GUIDToAssetPath(guid));
            var category = projectile.name.Contains("_basic") ? AttackCategory.Normal
                : projectile.name.Contains("_skill") ? AttackCategory.Skill : AttackCategory.Ultimate;
            if (projectile.ResolveSpawningHitbox() is NormalHitbox spawn && CanProbePosition(spawn))
                yield return ($"{projectile.name}/spawn", category, null, spawn);
            if (projectile.ResolveRoutineHitbox() is NormalHitbox routine && CanProbePosition(routine))
                yield return ($"{projectile.name}/routine", category, null, routine);
        }

        static bool CanProbePosition(NormalHitbox hit) => hit.useDeterministicReaction && !hit.useSnapToAnchor
            && !hit.breaksGrab && (hit.reactionKind == EnemyReactionKind.Knockup
                || hit.reactionKind == EnemyReactionKind.Knockback && hit.horizontalSpeed > 0f);
    }

    private sealed class Fixture : System.IDisposable
    {
        public readonly GameObject Root = new GameObject("101 separation fixture");
        public readonly List<EnemyController> Enemies = new List<EnemyController>();
        public readonly BoxCollider2D Floor;
        public Fixture(int count, string enemyPath = "enemy/Enm101/Enemy1.prefab")
        {
            var floor = new GameObject("Ground");
            floor.transform.SetParent(Root.transform);
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = Vector3.down * .5f;
            Floor = floor.AddComponent<BoxCollider2D>();
            Floor.size = new Vector2(50f, 1f);
            Root.AddComponent<AudioListener>();
            var holder = new GameObject("Inactive spawn holder");
            holder.transform.SetParent(Root.transform);
            holder.SetActive(false);
            for (int i = 0; i < count; i++)
            {
                var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRoot + enemyPath), holder.transform);
                instance.transform.localPosition = new Vector3(-3f + i * 1.5f, .06f, 0f);
                var graph = instance.GetComponent<BehaviorGraphAgent>();
                if (graph != null) graph.enabled = false;
                Enemies.Add(instance.GetComponent<EnemyController>());
            }
            holder.SetActive(true);
            foreach (var enemy in Enemies) enemy.InitializeSpawn(enemy.transform.position, enemy.transform.rotation);
        }
        public IEnumerator Ready()
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (Enemies.Any(e => !e.IsCombatReady) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Enemies.All(e => e.IsCombatReady), Is.True);
            foreach (var enemy in Enemies) enemy.SetBehaviorAgentActive(false);
            for (int i = 0; i < 5; i++) yield return PhysicsStep();
        }
        public void Collapse(float x = 0f)
        {
            foreach (var enemy in Enemies)
            {
                enemy.ReleaseFacing();
                enemy.rb.position = new Vector2(x, enemy.rb.position.y);
                enemy.rb.linearVelocity = Vector2.zero;
            }
            Physics2D.SyncTransforms();
        }
        public float[] XPositions() => Enemies.Select(e => e.rb.position.x).OrderBy(x => x).ToArray();
        public void Dispose()
        {
            foreach (var enemy in Enemies)
            {
                var ui = typeof(EnemyController).GetField("worldUI", Private).GetValue(enemy) as Component;
                if (ui != null) Object.DestroyImmediate(ui.gameObject);
            }
            Object.DestroyImmediate(Root);
        }
    }

    private static IEnumerator PhysicsStep()
    {
        float before = Time.fixedTime;
        while (Time.fixedTime <= before) yield return null;
    }

    [UnityTearDown]
    public IEnumerator ExitAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
