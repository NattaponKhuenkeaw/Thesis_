#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ArtifactUiPolishPlayTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);

    [UnityTest]
    public IEnumerator RepeatedDiscardSettingsAndTwoClickPickupWorkWhileGameplayIsPaused()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        var root = new GameObject("Artifact UI regression", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var gridHost = new GameObject("Inventory", typeof(RectTransform));
        gridHost.transform.SetParent(root.transform, false);
        gridHost.SetActive(false);
        var panel = gridHost.AddComponent<ArtifactGridPanel>();
        ArtifactRewardPickerUI picker = null;
        GameObject hudHost = null;
        Action releasePause = GameFreezeManager.AcquirePauseHold();
        int skipBefore = PlayerPrefs.GetInt("project.artifact.skipDiscardConfirmation", 0);
        try
        {
            PlayerPrefs.SetInt("project.artifact.skipDiscardConfirmation", 0);
            Set(panel, "gridParent", gridHost.transform);
            Set(panel, "gridItemPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/UI/TeamArtifactInventoryCard.prefab"));
            panel.Init(null, root.GetComponent<Canvas>(), inventory);
            var common = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/RarityR/R_RustedOathRing");
            for (int i = 0; i < 3; i++) Assert.That(inventory.TryAcquireArtifact(common, out _, i + 1), Is.True);
            gridHost.SetActive(true);
            panel.PopulateGrid(inventory.ArtifactInstances);
            for (int i = 0; i < 3; i++)
            {
                var cell = panel.GetComponentsInChildren<ArtifactGridItemUI>().First();
                string id = cell.ArtifactData.instanceId;
                var rightClick = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right, position = new Vector2(400, 300) };
                cell.OnPointerClick(rightClick);
                yield return new WaitForSecondsRealtime(.2f);
                var menu = Get<ArtifactCopyMenuUI>(panel, "activeMenu");
                Assert.That(menu, Is.Not.Null, "Context menu must reopen after every deletion.");
                menu.transform.Find("Window/Discard").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.4f);
                var dialog = Get<ArtifactCopyMenuUI>(panel, "activeMenu");
                Assert.That(dialog, Is.Not.Null);
                dialog.transform.Find("Window/Discard").GetComponent<Button>().onClick.Invoke();
                Assert.That(dialog.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(inventory.TryGetArtifactInstance(id, out _), Is.False);
                Assert.That(Get<ArtifactCopyMenuUI>(panel, "activeMenu"), Is.Null);
                Assert.That(root.GetComponentsInChildren<ArtifactCopyMenuUI>(true).Length, Is.Zero);
            }

            hudHost = new GameObject("Navigation test", typeof(RectTransform), typeof(Canvas));
            hudHost.SetActive(false);
            var nav = new GameObject("Navigation", typeof(RectTransform)); nav.transform.SetParent(hudHost.transform, false);
            var settingsHost = new GameObject("Setting", typeof(RectTransform), typeof(Image), typeof(Button));
            settingsHost.transform.SetParent(nav.transform, false);
            var hud = hudHost.AddComponent<PlayerHUD>();
            Set(hud, "navigationRoot", nav);
            hudHost.SetActive(true);
            typeof(PlayerHUD).GetMethod("EnsureNavigationClickBlocked", Private).Invoke(hud, null);
            var pause = PauseMenuController.Instance;
            Assert.That(pause, Is.Not.Null);
            settingsHost.GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(PauseMenuController.IsOpen, Is.True);
            Assert.That(Get<GameObject>(pause, "settingsPanel").activeSelf, Is.True);
            pause.OnResumeClicked();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(PauseMenuController.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.Zero, "The underlying gameplay pause hold survives Settings.");

            picker = Object.Instantiate(Resources.Load<ArtifactRewardPickerUI>(ArtifactRewardPickerUI.ResourcePath));
            var generator = new ArtifactRollGenerator(inventory.ArtifactRollTable);
            var definitions = new[] { common,
                Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/RaritySR/SR_10_ProtectionOfJoseph"),
                Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/Phase1/P1_04_BrokenChronoRelic") };
            var choices = definitions.Select((d, i) => generator.Generate(d, 0, 71 + i, 2)).ToArray();
            int commits = 0;
            bool finished = false;
            IEnumerator Present()
            {
                yield return picker.Present(choices, inventory, 1, 1, i => { commits++; return inventory.TryCommitArtifact(choices[i], 0, out _); });
                finished = true;
            }
            picker.StartCoroutine(Present());
            yield return new WaitForSecondsRealtime(.8f);
            var selected = picker.transform.Find("Card2").GetComponent<Button>();
            var rect = (RectTransform)selected.transform;
            float restingY = rect.anchoredPosition.y;
            selected.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(commits, Is.Zero);
            Assert.That(rect.anchoredPosition.y, Is.GreaterThan(restingY));
            Assert.That(picker.transform.Find("Confirm"), Is.Null);
            Assert.That(picker.transform.Find("Card1").GetComponent<Image>().color,
                Is.Not.EqualTo(selected.GetComponent<Image>().color), "Rarity colors the actual card background.");
            Assert.That(picker.transform.Find("Card2").GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Passive").richText, Is.True);
            selected.onClick.Invoke(); selected.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.55f);
            Assert.That(commits, Is.EqualTo(1));
            Assert.That(finished, Is.True);
            Assert.That(picker.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(picker.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(inventory.TryGetArtifactInstance(choices[1].instanceId, out _), Is.True);
            Assert.That(inventory.TryGetArtifactInstance(choices[0].instanceId, out _), Is.False);
        }
        finally
        {
            PlayerPrefs.SetInt("project.artifact.skipDiscardConfirmation", skipBefore);
            if (PauseMenuController.IsOpen) PauseMenuController.Instance.OnResumeClicked();
            if (picker != null) Object.Destroy(picker.gameObject);
            if (hudHost != null) Object.Destroy(hudHost);
            Object.Destroy(root); Object.Destroy(inventory);
            releasePause?.Invoke();
        }
        yield return new ExitPlayMode();
    }
}
#endif
