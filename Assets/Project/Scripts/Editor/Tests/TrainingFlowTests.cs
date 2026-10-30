#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CombatCore;
using NUnit.Framework;
using Project.Save;
using Project.Training;
using Project.UI;
using Project.UI.Training;
using Unity.Behavior;
using Unity.Cinemachine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class TrainingFlowTests
{
    private const string Report = "Docs_JJK/3_Outputs/Reports/TrainingScene/";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);

    [UnityTest]
    public IEnumerator MenuTrainingConfigurationDamageCameraAndExitPreserveSaves()
    {
        EditorSceneManager.OpenScene("Assets/Project/Scenes/MainMenu.unity");
        yield return new EnterPlayMode();
        yield return new WaitForSecondsRealtime(1f);
        Directory.CreateDirectory(Report);
        var saved = CaptureSaveData();
        int activeSlot = SaveSlotStore.ActiveSlot;
        ScreenCapture.CaptureScreenshot(Report + "main-menu.png");
        yield return null;
        Object.FindFirstObjectByType<MainMenuController>().OnTrainingClicked();
        yield return new WaitForSecondsRealtime(2f);
        float timeout = Time.realtimeSinceStartup + 12f;
        while (InputLockManager.IsLocked && Time.realtimeSinceStartup < timeout) yield return null;

        var session = TrainingSession.Instance;
        Assert.That(session, Is.Not.Null);
        Assert.That(session.IsActive, Is.True);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(TrainingSession.SceneName));
        Assert.That(TrainingDebugManager.CollectStats, Is.True);
        Assert.That(TrainingDebugManager.ShowStatsHud, Is.False);
        Assert.That(TrainingDebugManager.DrawColliders, Is.False);
        var camera = Get<CinemachineCamera>(CameraManager.instance, "_combatCam");
        Assert.That(camera.name, Is.EqualTo("CinemachineCamera_Training"));
        Assert.That(camera.Lens.OrthographicSize, Is.EqualTo(10f));
        var brain = Camera.main.GetComponent<CinemachineBrain>();
        Assert.That(brain.ActiveVirtualCamera.Name, Is.EqualTo(camera.name));
        var dummy = Object.FindFirstObjectByType<EnemyController>();
        timeout = Time.realtimeSinceStartup + 5f;
        while (dummy != null && !dummy.IsTargetable && Time.realtimeSinceStartup < timeout) yield return null;
        ScreenCapture.CaptureScreenshot(Report + "training-entry.png");
        yield return null;
        Assert.That(dummy.IsTargetable, Is.True,
            $"Dummy {dummy.name}: enabled={dummy.enabled}, active={dummy.gameObject.activeInHierarchy}, lifecycle={dummy.LifecycleState}, HP={dummy.currentHP}, timeScale={Time.timeScale}");
        var agent = dummy.GetComponent<BehaviorGraphAgent>();
        Assert.That(agent.enabled, Is.False);
        Assert.That(dummy.bodyCollider.enabled, Is.True);
        int originalLevel = Get<int>(session.Inventory, "_globalLevel");
        string metaBefore = session.Progression.ExportJson();

        var panel = TrainingConfigurationPanel.Instance;
        panel.ToggleStats();
        Assert.That(TrainingDebugManager.ShowStatsHud, Is.True);
        panel.Open();
        Assert.That(panel.IsOpen, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        int targetLevel = originalLevel == 10 ? 11 : 10;
        Get<Slider>(panel, "teamLevel").value = targetLevel;
        Get<Slider>(panel, "skill").value = 0;
        Get<Slider>(panel, "ultimate").value = 0;
        Get<Slider>(panel, "passive").value = 0;
        ScreenCapture.CaptureScreenshot(Report + "configuration.png");
        yield return null;
        panel.Apply();
        Assert.That(panel.IsOpen, Is.False);
        Assert.That(InputLockManager.IsLocked, Is.False);
        Assert.That(session.Inventory.GlobalLevel, Is.EqualTo(targetLevel));
        Assert.That(Get<int>(session.Inventory, "_globalLevel"), Is.EqualTo(originalLevel));
        Assert.That(session.Progression.ExportJson(), Is.EqualTo(metaBefore));

        // Exercise the real accepted-damage/reaction path, not a direct stats injection.
        float hpBefore = dummy.currentHP;
        float damage = dummy.TakeResolvedHit(new HitReactionData
        {
            damage = 1000f, sourceCharacter = TeamLoader.Instance.CurrentCharacter,
            reactionKind = EnemyReactionKind.Flinch,
            reactionTiming = HitReactionData.CreateTiming(0, 1, 1),
            juggleStunScale = 1f, juggleLaunchScale = 1f
        }, PlayerCharacter.Instance.transform.position);
        Assert.That(damage, Is.GreaterThan(0f));
        Assert.That(dummy.currentHP, Is.LessThan(hpBefore));
        Assert.That(TrainingDebugStats.Instance.totalDamageOnCurrentTarget, Is.EqualTo(damage));
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(agent.enabled, Is.False, "Recovery must not resume dummy AI.");
        Assert.That(TrainingDebugStats.Instance.currentDPS, Is.GreaterThan(0f));
        panel.ToggleStats();
        Assert.That(TrainingDebugManager.ShowStatsHud, Is.False);
        Assert.That(TrainingDebugStats.Instance.totalDamageOnCurrentTarget, Is.EqualTo(damage));
        panel.ToggleStats();
        ScreenCapture.CaptureScreenshot(Report + "training-stats.png");
        yield return null;

        CameraManager.instance.SetUltimateZoom(true, 5f);
        yield return new WaitForSecondsRealtime(1.2f);
        CameraManager.instance.SetUltimateZoom(false);
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.That(brain.ActiveVirtualCamera.Name, Is.EqualTo(camera.name));
        Assert.That(camera.Lens.OrthographicSize, Is.EqualTo(10f));
        panel.Open();
        Get<Slider>(panel, "teamLevel").value = 1;
        panel.Close();
        Assert.That(session.Inventory.GlobalLevel, Is.EqualTo(targetLevel), "Cancel must discard the draft.");
        session.ResetDamageSample();
        Assert.That(TrainingDebugStats.Instance.totalDamageOnCurrentTarget, Is.Zero);
        session.Inventory.SaveBossLobbyCheckpoint();
        session.Inventory.PublishActiveSaveHeader();
        session.Progression.SavePersistentState();
        AssertSaveData(saved);

        PauseMenuController.Instance.ReturnFromTraining();
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        Assert.That(TrainingSession.Instance, Is.Null);
        Assert.That(Project.Core.RootPersistentManager.Instance, Is.Null);
        Assert.That(SaveSlotStore.ActiveSlot, Is.EqualTo(activeSlot));
        AssertSaveData(saved);

        // An interrupted/direct scene load must release overrides and its persistent root too.
        Object.FindFirstObjectByType<MainMenuController>().OnTrainingClicked();
        yield return new WaitForSecondsRealtime(1.5f);
        var second = TrainingSession.Instance;
        var secondInventory = second.Inventory;
        var draft = second.CaptureConfiguration();
        draft.teamLevel = 2;
        Assert.That(second.TryApply(draft, out _), Is.True);
        SceneManager.LoadScene("MainMenu");
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(TrainingSession.Instance, Is.Null);
        Assert.That(Project.Core.RootPersistentManager.Instance, Is.Null);
        Assert.That(secondInventory.GlobalLevel, Is.EqualTo(originalLevel));
        Assert.That(Project.Core.ZoneState.RunProgressionManager.Instance, Is.Null);
        AssertSaveData(saved);
        yield return new ExitPlayMode();
    }

    private static Dictionary<string, string> CaptureSaveData()
    {
        var values = new Dictionary<string, string>();
        for (int i = 0; i < SaveSlotStore.SlotCount; i++)
            foreach (string key in new[] { SaveSlotStore.MetaKey(i), SaveSlotStore.BossRunKey(i), SaveSlotStore.BossMetaKey(i),
                $"project.save.slot{i}.header", $"project.save.slot{i}.occupied" })
                values[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, "<non-string>") + ":" + PlayerPrefs.GetInt(key, -1) : "<missing>";
        return values;
    }

    private static void AssertSaveData(Dictionary<string, string> before)
    {
        var after = CaptureSaveData();
        foreach (var pair in before) Assert.That(after[pair.Key], Is.EqualTo(pair.Value), "Training changed " + pair.Key);
    }
}
#endif
