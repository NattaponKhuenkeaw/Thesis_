#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Behavior;
using UnityEngine;
using Object = UnityEngine.Object;

public class EnemyNoticeCombatRegistrationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject cameraHost;
    private GameObject player;
    private GameObject enemyHost;
    private CameraManager camera;
    private EnemyController enemy;

    [SetUp]
    public void SetUp()
    {
        TearDownInstance();
        player = new GameObject("NoticePlayer") { tag = "Player" };
        cameraHost = new GameObject("NoticeCamera");
        camera = cameraHost.AddComponent<CameraManager>();
        // EditMode AddComponent does not invoke this non-ExecuteAlways component's Awake.
        CameraManager.instance = camera;
        Set(camera, "_playerTransform", player.transform);

        enemyHost = new GameObject("NoticeEnemy");
        enemy = EnemyLifecycleTestUtility.AddActive(enemyHost);
        RoomEnemyRegistry.Register(enemy);
    }

    [TearDown]
    public void TearDown()
    {
        if (enemyHost != null)
        {
            RoomEnemyRegistry.Unregister(enemyHost.GetComponent<EnemyController>());
            Object.DestroyImmediate(enemyHost);
        }

        Object.DestroyImmediate(cameraHost);
        Object.DestroyImmediate(player);
        TearDownInstance();
    }

    [Test]
    public void SpawnActiveDoesNotEnterCombatUntilNotice()
    {
        Assert.That(enemy.IsTargetable, Is.True);
        Assert.That(enemy.InCombat, Is.False);
        Assert.That(IsTracked(enemy), Is.False);
    }

    [Test]
    public void NoticeRegistersEvenWhenAgentFieldIsUnbound()
    {
        GameObject host = SetEnemyStateAction.ResolveCombatHost(
            new BlackboardVariable<GameObject>((GameObject)null),
            enemyHost);
        Assert.That(host, Is.EqualTo(enemyHost));

        var action = new SetEnemyStateAction
        {
            EnemyState = new BlackboardVariable<State>(State.Idle),
            TargetState = new BlackboardVariable<State>(State.Notice),
            Agent = new BlackboardVariable<GameObject>((GameObject)null)
        };
        if (AssignGraphObject(action, enemyHost))
        {
            Node.Status status = (Node.Status)typeof(SetEnemyStateAction)
                .GetMethod("OnStart", Private | BindingFlags.FlattenHierarchy)
                .Invoke(action, null);
            Assert.That(status, Is.EqualTo(Node.Status.Success));
        }
        else
        {
            enemy.NotifyStateChanged(State.Notice);
        }
        Assert.That(enemy.InCombat, Is.True);
        Assert.That(IsTracked(enemy), Is.True);
        Assert.That(camera.IsInCombat, Is.True);
        Assert.That(camera.GetNearestEngagedEnemy(player.transform.position), Is.EqualTo(enemy));
    }

    [Test]
    public void FarEngagedEnemyStaysTrackedWithZeroWeightAndNeedsRangeToSelect()
    {
        enemy.NotifyStateChanged(State.Notice);
        Assert.That(IsTracked(enemy), Is.True);

        enemyHost.transform.position = new Vector3(40f, 0f, 0f);
        InvokeLateUpdate();
        InvokeLateUpdate();

        Assert.That(enemy.InCombat, Is.True);
        Assert.That(IsTracked(enemy), Is.True);
        Assert.That(camera.IsInCombat, Is.False);
        Assert.That(GetWeight(enemy), Is.EqualTo(0f).Within(0.02f));
        Assert.That(camera.GetNearestEngagedEnemy(player.transform.position), Is.Null);
        Assert.That(camera.GetNearestEngagedEnemyInRadius(player.transform.position, 50f), Is.EqualTo(enemy));

        enemyHost.transform.position = Vector3.zero;
        InvokeLateUpdate();
        Assert.That(IsTracked(enemy), Is.True);
        Assert.That(camera.IsInCombat, Is.True);
    }

    [Test]
    public void DisabledOrDeadEnemyCannotBeSelected()
    {
        enemy.NotifyStateChanged(State.Notice);
        enemyHost.SetActive(false);
        InvokeLateUpdate();

        Assert.That(enemy.IsTargetable, Is.False);
        Assert.That(camera.GetNearestEngagedEnemy(player.transform.position), Is.Null);
        Assert.That(camera.GetNearestEngagedEnemyInRadius(player.transform.position, 20f), Is.Null);
    }

    [Test]
    public void BossLeaveIsNotCancelledByFramingKeep()
    {
        enemy.isEliteOrBoss = true;
        enemy.NotifyStateChanged(State.Notice);
        Assert.That(IsTracked(enemy), Is.True);

        enemy.SetInCombat(false);
        Assert.That(camera.GetNearestEngagedEnemy(player.transform.position), Is.Null);

        InvokeLateUpdate();
        Assert.That(IsLeaving(enemy) || !IsTracked(enemy), Is.True);
    }

    [Test]
    public void RoomEnterClearsStaleMembers()
    {
        enemy.NotifyStateChanged(State.Notice);
        Assert.That(IsTracked(enemy), Is.True);

        camera.SnapToPlayerForRoomEnter(Vector3.zero);
        Assert.That(IsTracked(enemy), Is.False);
        Assert.That(camera.IsInCombat, Is.False);
        Assert.That(camera.GetNearestEngagedEnemy(player.transform.position), Is.Null);
    }

    [Test]
    public void CameraReadySyncsExistingInCombatEnemiesOnly()
    {
        Object.DestroyImmediate(cameraHost);
        cameraHost = null;
        CameraManager.instance = null;

        var idleHost = new GameObject("IdleEnemy");
        EnemyController idle = EnemyLifecycleTestUtility.AddActive(idleHost);
        RoomEnemyRegistry.Register(idle);
        enemy.NotifyStateChanged(State.Notice);
        Assert.That(enemy.InCombat, Is.True);
        Assert.That(idle.InCombat, Is.False);

        cameraHost = new GameObject("LateCamera");
        camera = cameraHost.AddComponent<CameraManager>();
        Set(camera, "_playerTransform", player.transform);
        camera.SyncEngagedEnemiesFromRegistry();

        Assert.That(IsTracked(enemy), Is.True);
        Assert.That(IsTracked(idle), Is.False);

        RoomEnemyRegistry.Unregister(idle);
        Object.DestroyImmediate(idleHost);
    }

    private bool IsTracked(EnemyController target)
    {
        IDictionary dict = Engaged();
        return dict != null && dict.Contains(target);
    }

    private bool IsLeaving(EnemyController target)
    {
        IDictionary dict = Engaged();
        if (dict == null || !dict.Contains(target))
        {
            return false;
        }

        object ctx = dict[target];
        return (bool)ctx.GetType().GetField("isLeaving").GetValue(ctx);
    }

    private float GetWeight(EnemyController target)
    {
        IDictionary dict = Engaged();
        object ctx = dict[target];
        return (float)ctx.GetType().GetField("currentWeight").GetValue(ctx);
    }

    private IDictionary Engaged()
    {
        return typeof(CameraManager).GetField("_engagedEnemies", Private).GetValue(camera) as IDictionary;
    }

    private void InvokeLateUpdate()
    {
        typeof(CameraManager).GetMethod("LateUpdate", Private).Invoke(camera, null);
    }

    private static void Set(object obj, string name, object value)
    {
        typeof(CameraManager).GetField(name, Private).SetValue(obj, value);
    }

    private static bool AssignGraphObject(object action, GameObject host)
    {
        for (Type type = action.GetType(); type != null; type = type.BaseType)
        {
            PropertyInfo property = type.GetProperty(
                "GameObject",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (property != null && property.CanWrite)
            {
                try
                {
                    property.SetValue(action, host);
                    return true;
                }
                catch (Exception)
                {
                }
            }

            FieldInfo field = type.GetField("m_GameObject", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                ?? type.GetField("<GameObject>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                try
                {
                    field.SetValue(action, host);
                    return true;
                }
                catch (Exception)
                {
                }
            }
        }

        return false;
    }

    private static void TearDownInstance()
    {
        if (CameraManager.instance != null)
        {
            Object.DestroyImmediate(CameraManager.instance.gameObject);
        }

        CameraManager.instance = null;
    }
}
#endif
