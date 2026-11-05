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

public class EnemyMeleeFlankTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string PrefabPath = "Assets/Project/Character/enemy/enemy/Enm101/Enemy1.prefab";

    [TestCase("Enm101/Enemy1.prefab", true)]
    [TestCase("Enm102/Enemy102.prefab", false)]
    [TestCase("Enm103/Enemy103.prefab", false)]
    public void OnlyEnemy101OptsIntoFlanking(string path, bool enabled)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/" + path);
        using (var data = new SerializedObject(prefab.GetComponent<EnemyLocomotion>()))
            Assert.That(data.FindProperty("groundMeleeFlanking").boolValue, Is.EqualTo(enabled));
    }

    [UnityTest]
    public IEnumerator FiveReal101sApproachFromBothFlanksAndBothFrontEnemiesCanAttack()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture = new GameObject("Melee flank runtime fixture");
        var target = Target(fixture.transform);
        Floor(fixture.transform, -30f, 30f);
        var enemies = new List<EnemyController>();
        try
        {
            for (int i = 0; i < 5; i++) enemies.Add(Spawn(fixture.transform, -4f - i * 2.5f));
            float deadline = Time.realtimeSinceStartup + 3f;
            while (enemies.Any(enemy => !enemy.IsGameplayActive) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(enemies.All(enemy => enemy.IsGameplayActive), Is.True);
            var chases = enemies.Select(enemy => new ChaseAction {
                Agent = new BlackboardVariable<GameObject>(enemy.gameObject),
                Target = new BlackboardVariable<GameObject>(target),
                AttackRange = new BlackboardVariable<float>(1.5f),
                ChaseSpeed = new BlackboardVariable<float>(4f)
            }).ToArray();
            foreach (var chase in chases) typeof(ChaseAction).GetMethod("OnStart", Private).Invoke(chase, null);
            deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                foreach (var chase in chases) typeof(ChaseAction).GetMethod("OnUpdate", Private).Invoke(chase, null);
                yield return new WaitForFixedUpdate();
                foreach (var enemy in enemies.Where(enemy => Mathf.Abs(enemy.transform.position.x) < .8f))
                    Assert.That(EnemyCombatRange.IsInAttackRange(enemy.gameObject, target, 1.5f), Is.False,
                        "A flanker crossing the player's center must not start an attack");
                if (enemies.Any(enemy => enemy.transform.position.x > .5f)
                    && enemies.Count(enemy => EnemyCombatRange.IsInAttackRange(enemy.gameObject, target, 1.5f)) >= 2
                    && enemies.All(enemy => enemy.GetComponent<EnemyLocomotion>().IsAtHoldSlot(target.transform, 1.5f)))
                    break;
            }
            var front = enemies.Where(enemy => EnemyCombatRange.IsInAttackRange(enemy.gameObject, target, 1.5f)).ToArray();
            Assert.That(front.Any(enemy => enemy.transform.position.x < 0f), Is.True, "One attacker should remain on the original side");
            Assert.That(front.Any(enemy => enemy.transform.position.x > 0f), Is.True, "A peer should walk across to the other flank");
            Assert.That(enemies.Count(enemy => enemy.transform.position.x > 0f), Is.InRange(2, 3),
                "Five enemies should split into two flanks, not leave one side with the entire pack");
            Assert.That(enemies.All(enemy => Mathf.Abs(enemy.transform.position.x) > .8f), Is.True, "Do not stop inside the player's center");
            Assert.That(enemies.All(enemy => enemy.bodyCollider.bounds.min.y > -.1f), Is.True);
            foreach (var enemy in front)
            {
                var attack = new EnemyAttackAction {
                    Agent = new BlackboardVariable<GameObject>(enemy.gameObject),
                    Target = new BlackboardVariable<GameObject>(target),
                    CanAttack = new BlackboardVariable<bool>(true)
                };
                var status = typeof(EnemyAttackAction).GetMethod("OnStart", Private).Invoke(attack, null);
                Assert.That(status.ToString(), Is.EqualTo("Running"), enemy.name);
                Assert.That(enemy.FacingLocked, Is.True);
                typeof(EnemyAttackAction).GetMethod("OnEnd", Private).Invoke(attack, null);
            }
        }
        finally { Object.DestroyImmediate(fixture); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator FlankSelectionRejectsWallAndMissingFloorAndKeepsOtherEnemyCollisions()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (bool wallBlocked in new[] { false, true })
        {
            var fixture = new GameObject("Blocked melee flank fixture");
            var target = Target(fixture.transform);
            Floor(fixture.transform, -20f, wallBlocked ? 20f : .5f);
            if (wallBlocked)
            {
                var wall = new GameObject("Solid wall");
                wall.transform.SetParent(fixture.transform);
                wall.layer = LayerMask.NameToLayer("Ground");
                wall.transform.position = new Vector3(2f, 3f, 0f);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 6f);
            }
            var first = Spawn(fixture.transform, -4f);
            var second = Spawn(fixture.transform, -7f);
            try
            {
                float deadline = Time.realtimeSinceStartup + 3f;
                while ((!first.IsGameplayActive || !second.IsGameplayActive) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                var a = first.GetComponent<EnemyLocomotion>();
                var b = second.GetComponent<EnemyLocomotion>();
                a.MoveTowardsTargetRange(target.transform, 1.5f, 4f, .02f);
                b.MoveTowardsTargetRange(target.transform, 1.5f, 4f, .02f);
                Assert.That((float)typeof(EnemyLocomotion).GetField("meleeFlankSide", Private).GetValue(b), Is.EqualTo(-1f));
                Assert.That(Physics2D.GetIgnoreCollision(first.bodyCollider, second.bodyCollider), Is.True);
                var floor = fixture.GetComponentsInChildren<BoxCollider2D>().First(collider => collider.name == "Solid floor");
                Assert.That(Physics2D.GetIgnoreCollision(first.bodyCollider, floor), Is.False);
                var shield = new GameObject("Other enemy body");
                shield.transform.SetParent(fixture.transform);
                shield.layer = LayerMask.NameToLayer("Enemy");
                shield.transform.position = new Vector3(-15f, 1f, 0f);
                var shieldBody = shield.AddComponent<BoxCollider2D>();
                Assert.That(Physics2D.GetIgnoreCollision(first.bodyCollider, shieldBody), Is.False);
                a.enabled = false;
                b.enabled = false;
                yield return null;
                Assert.That(Physics2D.GetIgnoreCollision(first.bodyCollider, second.bodyCollider), Is.False,
                    "Disabling the pack must release owned collision overrides");
            }
            finally { Object.DestroyImmediate(fixture); }
            yield return null;
        }
        yield return new ExitPlayMode();
    }

    private static GameObject Target(Transform parent)
    {
        var target = new GameObject("Player target");
        target.transform.SetParent(parent);
        target.layer = LayerMask.NameToLayer("Player");
        target.AddComponent<AudioListener>();
        var body = target.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var collider = target.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.55f, 2.6f);
        collider.offset = new Vector2(0f, 1.3f);
        return target;
    }

    private static void Floor(Transform parent, float left, float right)
    {
        var floor = new GameObject("Solid floor");
        floor.transform.SetParent(parent);
        floor.layer = LayerMask.NameToLayer("Ground");
        floor.transform.position = new Vector3((left + right) * .5f, -.5f, 0f);
        floor.AddComponent<BoxCollider2D>().size = new Vector2(right - left, 1f);
    }

    private static EnemyController Spawn(Transform parent, float x)
    {
        var holder = new GameObject("Spawn holder");
        holder.transform.SetParent(parent);
        holder.SetActive(false);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), holder.transform);
        root.transform.position = new Vector3(x, .06f, 0f);
        var graph = root.GetComponent<BehaviorGraphAgent>();
        if (graph != null) graph.enabled = false;
        holder.SetActive(true);
        var enemy = root.GetComponent<EnemyController>();
        enemy.InitializeSpawn(root.transform.position, root.transform.rotation);
        return enemy;
    }

    [UnityTearDown]
    public IEnumerator ExitPlayModeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
