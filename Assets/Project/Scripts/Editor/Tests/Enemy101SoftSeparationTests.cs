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

    private sealed class Fixture : System.IDisposable
    {
        public readonly GameObject Root = new GameObject("101 separation fixture");
        public readonly List<EnemyController> Enemies = new List<EnemyController>();
        public readonly BoxCollider2D Floor;
        public Fixture(int count)
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
                var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRoot + "enemy/Enm101/Enemy1.prefab"), holder.transform);
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
