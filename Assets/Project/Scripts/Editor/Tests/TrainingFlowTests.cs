#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using CombatCore;
using NUnit.Framework;
using Project.Save;
using Project.Training;
using Project.UI;
using Project.UI.Training;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using Project.InventorySystem.Items;
using Project.InventorySystem.Artifacts;
using Project.Core.ZoneState;
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
        var inventoryBeforeTraining = Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory");
        var rosterBeforeTraining = inventoryBeforeTraining.GetTeamSnapshot();
        var artifactIdsBeforeTraining = inventoryBeforeTraining.ArtifactInstances.Select(a => a.instanceId).ToArray();
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
        int originalLevel = inventoryBeforeTraining.GlobalLevel;
        Assert.That(session.Inventory, Is.TypeOf<TrainingInventoryState>());
        Assert.That(session.Progression, Is.TypeOf<TrainingProgressionState>());
        Assert.That(session.Inventory, Is.Not.SameAs(inventoryBeforeTraining));
        Assert.That(TeamLoader.Instance.RunInventory, Is.SameAs(session.Inventory));
        Assert.That(TeamLoader.Instance.MetaProgression, Is.SameAs(session.Progression));
        var managers = Object.FindFirstObjectByType<Project.Core.RootPersistentManager>();
        Assert.That(managers.GetComponentInChildren<Project.Combat.VFXManager>(), Is.Not.Null);
        Assert.That(managers.GetComponentInChildren<Project.Combat.ProjectileManager>(), Is.Not.Null);
        Assert.That(managers.GetComponentInChildren<Project.Combat.ProjectileVFXManager>(), Is.Not.Null);
        Assert.That(GameAudioManager.Instance, Is.Not.Null, "The MainMenu audio singleton survives; its duplicate scene copy may be removed.");
        var savedProgression = Resources.Load<Project.Progression.PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
        string metaBefore = savedProgression.ExportJson();

        var panel = TrainingConfigurationPanel.Instance;
        var drawer = panel.GetComponent<TrainingToolsDrawer>();
        Assert.That(drawer, Is.Not.Null);
        Assert.That(drawer.IsPresented, Is.False, "Only Config is visible at entry.");
        Assert.That(panel.transform.Find("TeamBuilder Button"), Is.Null, "Player_UI owns the TeamBuilder button.");
        Assert.That(panel.transform.Find("Inspector Button"), Is.Null, "Player_UI owns the Inspector button.");
        var drawerRect = Get<RectTransform>(drawer, "toolsPanel");
        var drawerGroup = Get<CanvasGroup>(drawer, "toolsGroup");
        Time.timeScale = 0f;
        float hiddenX = drawerRect.anchoredPosition.x;
        drawer.ConfigButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.06f);
        Assert.That(drawerRect.anchoredPosition.x, Is.GreaterThan(hiddenX), "Opening slides in from the left even at zero timescale.");
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(drawer.IsVisible, Is.True);
        Assert.That(drawerGroup.alpha, Is.EqualTo(1f));
        ScreenCapture.CaptureScreenshot(Report + "training-tools-open.png");
        yield return null;
        float openX = drawerRect.anchoredPosition.x;
        drawer.ConfigButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.06f);
        Assert.That(drawerRect.anchoredPosition.x, Is.GreaterThan(openX), "Closing slides right.");
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(drawer.IsPresented, Is.False);
        Assert.That(drawerGroup.blocksRaycasts, Is.False, "Closed drawer cannot eat combat clicks.");
        drawer.Toggle(); drawer.Toggle(); drawer.Toggle();
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(drawer.IsVisible && drawer.IsPresented, Is.True);
        Assert.That(drawerRect.anchoredPosition.x, Is.EqualTo(openX).Within(.01f), "Rapid reversals must not drift.");
        drawer.SetVisible(false);
        yield return new WaitForSecondsRealtime(.25f);
        Time.timeScale = 1f;
        var zone = Resources.Load<ZoneStateProvider>("CombatSO/ZoneStateProvider");
        Assert.That(zone.CurrentMode, Is.EqualTo(ZoneMode.CombatZone), "Practice must remain a combat room.");
        var catalog = Resources.LoadAll<BaseItem>("ItemSO");
        foreach (var item in catalog)
        {
            if (item is CharacterItem character && character.characterObjectData != null)
                Assert.That(session.Inventory.ownedItems.OfType<CharacterItem>().Any(c => c.characterObjectData.characterId == character.characterObjectData.characterId), Is.True);
            if (item is ArtifactDefinition definition && !string.IsNullOrWhiteSpace(definition.id))
                Assert.That(session.Inventory.ArtifactInstances.Any(a => a.definitionId == definition.id), Is.True);
        }
        drawer.ConfigButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.25f);
        Get<Button>(panel, "statsButton").onClick.Invoke();
        Assert.That(drawer.IsVisible, Is.True, "Stats must not close Config.");
        Assert.That(TrainingDebugManager.ShowStatsHud, Is.True);
        ClickPlayerNavigation("OpenTeamBuilder");
        yield return new WaitForSecondsRealtime(.35f);
        Assert.That(drawer.IsVisible && drawer.IsPresented, Is.True, "TeamBuilder must not close Config.");
        Assert.That(TrainingDebugHUD.IsPointerOverHud(new Vector2(Screen.width * .5f, Screen.height - 10f)), Is.True,
            "Stats must remain drawn and interactive alongside Config and TeamBuilder.");
        drawer.ConfigButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(drawer.IsPresented, Is.False, "Config's button can close it while loadout holds the input lock.");
        drawer.ConfigButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(drawer.IsPresented, Is.True, "Config's button can reopen it alongside another window.");
        var builder = Object.FindFirstObjectByType<TeamBuilderUI>();
        Assert.That(panel.IsLoadoutOpen, Is.True);
        Assert.That(builder.CurrentMode, Is.EqualTo(ZoneMode.SafeZone));
        Assert.That(InputLockManager.IsLocked, Is.True);
        var slots = Get<TeamSlotUI[]>(builder, "teamSlots");
        var practiceCharacter = session.Inventory.ownedItems.OfType<CharacterItem>()
            .FirstOrDefault(c => c.characterObjectData != null && !session.Inventory.GetTeamSnapshot().Contains(c.characterObjectData));
        Assert.That(practiceCharacter, Is.Not.Null, "Catalog should offer a character beyond the entry team.");
        slots[0].SetCharacter(practiceCharacter);
        builder.OnSlotChanged();
        var practiceArtifact = session.Inventory.ArtifactInstances.First();
        Assert.That(builder.TryEquipArtifact(practiceArtifact), Is.True);
        builder.OnDeployConfirmed();
        Assert.That(TeamLoader.Instance.ContainsCharacter(practiceCharacter.characterObjectData), Is.True);
        Assert.That(session.Inventory.GetEquippedArtifactInstancesAt(0).Any(a => a != null && a.instanceId == practiceArtifact.instanceId), Is.True);
        builder.ToggleArtifactGridPanel();
        ScreenCapture.CaptureScreenshot(Report + "training-artifact-inventory.png");
        yield return null;
        Object.FindFirstObjectByType<UIManager>().CloseAll();
        yield return new WaitForSecondsRealtime(.35f);
        ClickPlayerNavigation("OpenInspector");
        yield return new WaitForSecondsRealtime(.35f);
        Assert.That(drawer.IsVisible && drawer.IsPresented, Is.True, "Inspector must not close Config.");
        Assert.That(TrainingDebugHUD.IsPointerOverHud(new Vector2(Screen.width * .5f, Screen.height - 10f)), Is.True);
        Assert.That(panel.IsLoadoutOpen, Is.True);
        var inspector = Object.FindFirstObjectByType<InspectScreenUI>();
        Assert.That(inspector, Is.Not.Null);
        var unassigned = session.Inventory.ownedItems.OfType<CharacterItem>()
            .First(c => c.characterObjectData != null && !TeamLoader.Instance.ContainsCharacter(c.characterObjectData));
        inspector.Open(unassigned);
        Assert.That(inspector.CurrentCharacter, Is.EqualTo(unassigned));
        inspector.Close();
        inspector.Open(unassigned);
        yield return new WaitForSecondsRealtime(.35f);
        Assert.That(inspector.isActiveAndEnabled, Is.True, "A previous close fade must not dismiss a reopened Inspector.");
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(InputLockManager.IsLocked, Is.True);
        ScreenCapture.CaptureScreenshot(Report + "training-inspector.png");
        yield return null;
        Object.FindFirstObjectByType<UIManager>().CloseAll();
        yield return new WaitForSecondsRealtime(.35f);
        Assert.That(InputLockManager.IsLocked, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        drawer.SetVisible(true);
        yield return new WaitForSecondsRealtime(.25f);
        Get<Button>(panel, "statsButton").onClick.Invoke();
        Assert.That(TrainingDebugManager.ShowStatsHud, Is.False);
        Assert.That(drawer.IsVisible, Is.True);
        Get<Button>(panel, "statsButton").onClick.Invoke();
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(TrainingDebugManager.ShowStatsHud, Is.True);
        drawer.SetVisible(true);
        yield return new WaitForSecondsRealtime(.25f);
        Get<Button>(panel, "openButton").onClick.Invoke();
        Assert.That(panel.IsOpen, Is.True);
        yield return null;
        Assert.That(drawer.IsVisible && drawer.IsPresented, Is.True, "Training Config must not close its drawer.");
        Assert.That(TrainingDebugManager.ShowStatsHud, Is.True, "Training Config must preserve the player's Stats selection.");
        Assert.That(TrainingStatsCanvas.Instance.DisplayCanvas.enabled, Is.True);
        var configContent = Get<GameObject>(panel, "content");
        Assert.That(configContent.GetComponentInParent<Canvas>().sortingOrder,
            Is.GreaterThan(TrainingStatsCanvas.Instance.DisplayCanvas.sortingOrder));
        Assert.That(TrainingDebugHUD.IsPointerOverHud(new Vector2(Screen.width * .5f, Screen.height - 10f)), Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        int targetLevel = originalLevel == 10 ? 11 : 10;
        Assert.That(configContent.transform.Find("Training Configuration Controls/Previous upgrades"), Is.Null);
        Assert.That(configContent.transform.Find("Training Panel").gameObject.activeSelf, Is.False);
        Get<Slider>(panel, "teamLevel").value = targetLevel;
        ScreenCapture.CaptureScreenshot(Report + "configuration.png");
        yield return null;
        panel.Apply();
        Assert.That(panel.IsOpen, Is.False);
        Assert.That(InputLockManager.IsLocked, Is.False);
        Assert.That(session.Inventory.GlobalLevel, Is.EqualTo(targetLevel));
        Assert.That(inventoryBeforeTraining.GlobalLevel, Is.EqualTo(originalLevel));
        Assert.That(savedProgression.ExportJson(), Is.EqualTo(metaBefore));

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
        drawer.SetVisible(true);
        yield return new WaitForSecondsRealtime(.25f);
        Get<Button>(panel, "statsButton").onClick.Invoke();
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(TrainingDebugManager.ShowStatsHud, Is.False);
        Assert.That(TrainingDebugStats.Instance.totalDamageOnCurrentTarget, Is.EqualTo(damage));
        drawer.SetVisible(true);
        yield return new WaitForSecondsRealtime(.25f);
        Get<Button>(panel, "statsButton").onClick.Invoke();
        yield return new WaitForSecondsRealtime(.25f);
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
        CollectionAssert.AreEqual(rosterBeforeTraining, inventoryBeforeTraining.GetTeamSnapshot());
        CollectionAssert.AreEqual(artifactIdsBeforeTraining, inventoryBeforeTraining.ArtifactInstances.Select(a => a.instanceId).ToArray());
        Assert.That(TrainingAccessPolicy.LoadoutMode(ZoneMode.CombatZone), Is.EqualTo(ZoneMode.CombatZone));
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
        Assert.That(secondInventory == null, Is.True, "The practice inventory is destroyed with its scene.");
        Assert.That(inventoryBeforeTraining.GlobalLevel, Is.EqualTo(originalLevel));
        Assert.That(Project.Core.ZoneState.RunProgressionManager.Instance, Is.Null);
        AssertSaveData(saved);
        yield return new ExitPlayMode();
    }

    private static void ClickPlayerNavigation(string method)
    {
        foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentMethodName(i) == method
                    && button.transform.parent != null && button.transform.parent.name == "Navigation"
                    && button.onClick.GetPersistentTarget(i) is UIManager)
                {
                    Assert.That(button.IsInteractable(), Is.True);
                    button.onClick.Invoke();
                    return;
                }
        Assert.Fail("Player_UI navigation button is missing or unbound: " + method);
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
