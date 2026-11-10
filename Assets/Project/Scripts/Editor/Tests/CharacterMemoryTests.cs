#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.TeamBuilder.UI.Panels;
using Project.Training;
using Project.UI;
using System.Collections;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CharacterMemoryTests
{
    internal static void SetMemoryRanks(CharacterProgressionState progression, CharacterObject character,
        int skill = 0, int ultimate = 0, int passive = 0)
    {
        var memory = new CharacterMemoryProgress(character.characterId);
        int step = 1;
        int[] ranks = { skill, ultimate, passive };
        for (int family = 0; family < ranks.Length; family++)
            for (int i = 0; i < ranks[family]; i++, step += 3)
                memory.TrySelectStep(step, (PotentialTrack)family);
        typeof(CharacterProgressionState).GetMethod("SetPracticeMemory",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(progression, new object[] { memory });
    }

    [Test]
    public void MainUsesMemoryRanksAndGenericBonusesAsBaseStats()
    {
        var state = ScriptableObject.CreateInstance<CharacterProgressionState>();
        var main = AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset");
        try
        {
            state.SetPotentialLevel(main, PotentialTrack.Skill, 4);
            Assert.That(state.GetPotentialLevel(main, PotentialTrack.Skill), Is.EqualTo(4));
            Assert.That(state.GetCombatPotentialLevel(main, PotentialTrack.Skill), Is.Zero);
            Assert.That(state.CanUpgradePotential(main, PotentialTrack.Passive, out _), Is.False);
            var original = state.GetUnlockedPotentialNodes(main);

            Assert.That(state.TryGrantMemory(main, 1, PotentialTrack.Skill), Is.True);
            Assert.That(state.TryGrantMemory(main, 2, PotentialTrack.Skill), Is.True);
            Assert.That(state.TryGrantMemory(main, 3, PotentialTrack.Skill), Is.True);
            var stats = new CharacterStats { maxHP = 1000, critRate = 5, attack = 100 };
            state.ApplyMemoryBaseStats(main, stats);
            Assert.That(stats.maxHP, Is.EqualTo(1030f));
            Assert.That(stats.critRate, Is.EqualTo(8f));
            Assert.That(stats.attack, Is.EqualTo(100f));
            Assert.That(state.GetUnlockedPotentialNodes(main).Count, Is.EqualTo(original.Count + 1));
            Assert.That(state.GetCombatPotentialLevel(main, PotentialTrack.Skill), Is.EqualTo(1));

            SetMemoryRanks(state, main, skill: 6, ultimate: 6, passive: 6);
            Assert.That(state.TryGrantMemory(main, 53, PotentialTrack.Skill), Is.True);
            Assert.That(state.TryGrantMemory(main, 54, PotentialTrack.Skill), Is.True);
            Assert.That(state.GetUnlockedPotentialNodes(main).Count, Is.EqualTo(original.Count + 18));
            Assert.That(state.GetCombatPotentialLevel(main, PotentialTrack.Passive), Is.EqualTo(6));
            var fullStats = new CharacterStats { maxHP = 1000, attack = 100 };
            state.ApplyMemoryBaseStats(main, fullStats);
            Assert.That(fullStats.maxHP, Is.EqualTo(1270f));
            Assert.That(fullStats.attack, Is.EqualTo(121f));
            Assert.That(fullStats.dotMastery, Is.EqualTo(12f));
        }
        finally { Object.DestroyImmediate(state); }
    }

    [Test]
    public void ReiMemoryUsesAuthoredDescriptionsAndSavedProgress()
    {
        var state = ScriptableObject.CreateInstance<CharacterProgressionState>();
        var item = ScriptableObject.CreateInstance<CharacterItem>();
        var rei = AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_1_CameraGirl/CharacterObject_cg.asset");
        item.characterObjectData = rei;
        try
        {
            const string save = "{\"schemaVersion\":2,\"characterMemories\":[{\"_characterId\":\"char-1-camera_girl\"," +
                "\"_completedSteps\":4,\"_choices\":[0,2]}]}";
            state.ImportJson(save);
            Assert.That(state.GetCombatPotentialLevel(rei, PotentialTrack.Skill), Is.EqualTo(1));
            Assert.That(state.GetCombatPotentialLevel(rei, PotentialTrack.Passive), Is.EqualTo(1));
            Assert.That(state.TryGrantMemory(rei, 5, PotentialTrack.Skill), Is.True);
            var stats = new CharacterStats { maxHP = 1000f, critRate = 5f };
            state.ApplyMemoryBaseStats(rei, stats);
            Assert.That(stats.maxHP, Is.EqualTo(1030f));
            Assert.That(stats.critRate, Is.EqualTo(8f));

            var describe = typeof(CharacterMemoryPanel).GetMethod("DescribeMemoryEffect",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            foreach (var node in rei.kitDefinition.potentialNodes)
            {
                Assert.That(node.description, Is.Not.Empty);
                Assert.That(describe.Invoke(null, new object[] { item, 1, node.track, node.level }), Is.EqualTo(node.description));
            }
            Assert.That(describe.Invoke(null, new object[] { item, 2, PotentialTrack.Skill, 0 }),
                Does.StartWith("Base ").And.Contain("+3%"));
        }
        finally
        {
            Object.DestroyImmediate(item);
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void PracticeFillsTheFiftyFourStepTrackAndSwapsAFullFamily()
    {
        var memory = new CharacterMemoryProgress("character");
        int choiceSteps = 0;
        for (int step = 1; step <= CharacterMemoryProgress.StepCount; step++)
            if (CharacterMemoryProgress.IsChoiceStep(step)) choiceSteps++;

        Assert.That(choiceSteps, Is.EqualTo(18));
        Assert.That(memory.TrySelectStep(54), Is.True);
        Assert.That(memory.GetChoice(16), Is.EqualTo(PotentialTrack.Skill));
        Assert.That(memory.GetChoice(19), Is.EqualTo(PotentialTrack.Ultimate));
        Assert.That(memory.GetChoice(37), Is.EqualTo(PotentialTrack.Passive));
        Assert.That(memory.TrySelectStep(1, PotentialTrack.Passive), Is.True);
        Assert.That(memory.CompletedSteps, Is.EqualTo(54));
        Assert.That(memory.GetChoice(1), Is.EqualTo(PotentialTrack.Passive));
        Assert.That(memory.GetChoice(52), Is.EqualTo(PotentialTrack.Skill));
        Assert.That(memory.TrySelectStep(55), Is.False);
        AssertSixPerFamily(memory);
    }

    [Test]
    public void DrawSkipsFullFamiliesAndGenericStepsDoNotIncreaseTheMajorCount()
    {
        var state = ScriptableObject.CreateInstance<CharacterProgressionState>();
        var character = ScriptableObject.CreateInstance<CharacterObject>();
        character.characterId = "character";
        try
        {
            for (int step = 1; step <= 18; step++)
                Assert.That(state.TryGrantMemory(character, step, PotentialTrack.Skill), Is.True);
            Assert.That(state.GetCompletedMajorMemoryUpgrades(character.characterId), Is.EqualTo(6));
            Assert.That(state.TryGrantMemory(character, 19, PotentialTrack.Skill), Is.False);
            for (int step = 19; step <= 54; step++)
            {
                Assert.That(state.TryDrawMemory(character, step, 42, out PotentialTrack choice), Is.True);
                if (CharacterMemoryProgress.IsChoiceStep(step)) Assert.That(choice, Is.Not.EqualTo(PotentialTrack.Skill));
            }
            Assert.That(state.TryDrawMemory(character, 55, 42, out _), Is.False);
            Assert.That(state.GetCompletedMajorMemoryUpgrades(character.characterId), Is.EqualTo(18));
            AssertSixPerFamily(state.CaptureMemory(character.characterId));
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void OldSixtyStepSaveKeepsValidChoicesAndNormalizesOverflow()
    {
        var state = ScriptableObject.CreateInstance<CharacterProgressionState>();
        try
        {
            state.ImportJson("{\"schemaVersion\":2,\"characterMemories\":[{\"_characterId\":\"character\"," +
                "\"_completedSteps\":60,\"_choices\":[0,0,0,0,0,0,0,1,1,1,1,1,1,2,2,2,2,2,2,2]}]}");
            var memory = state.CaptureMemory("character");
            Assert.That(memory.CompletedSteps, Is.EqualTo(54));
            Assert.That(memory.GetChoice(16), Is.EqualTo(PotentialTrack.Skill));
            Assert.That(memory.GetChoice(19), Is.EqualTo(PotentialTrack.Passive));
            Assert.That(memory.GetChoice(22), Is.EqualTo(PotentialTrack.Ultimate));
            AssertSixPerFamily(memory);
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void GrantsAreSequentialAndSurviveCheckpointRestoreAndSaveRoundTrip()
    {
        var state = ScriptableObject.CreateInstance<CharacterProgressionState>();
        var reloaded = ScriptableObject.CreateInstance<CharacterProgressionState>();
        var character = ScriptableObject.CreateInstance<CharacterObject>();
        character.characterId = "character";
        try
        {
            const string oldSave = "{\"schemaVersion\":2,\"characterPotentials\":[{\"characterId\":\"character\",\"skillLevel\":2}]}";
            state.ImportJson(oldSave);
            state.MarkCommitted();
            Assert.That(state.GetCompletedMemorySteps(character.characterId), Is.Zero);
            Assert.That(state.TryGrantMemory(character, 2, PotentialTrack.Skill), Is.False);
            Assert.That(state.TryGrantMemory(character, 1, PotentialTrack.Ultimate), Is.True);
            Assert.That(state.TryGrantMemory(character, 1, PotentialTrack.Passive), Is.False);
            Assert.That(state.TryGrantMemory(character, 2, PotentialTrack.Skill), Is.True);
            state.RevertToCommitted();

            reloaded.ImportJson(state.ExportJson());
            Assert.That(reloaded.GetCompletedMemorySteps(character.characterId), Is.EqualTo(2));
            Assert.That(reloaded.CaptureMemory(character.characterId).GetChoice(1), Is.EqualTo(PotentialTrack.Ultimate));
            Assert.That(reloaded.GetPotentialLevel(character, PotentialTrack.Skill), Is.EqualTo(2));
            reloaded.ImportJson(oldSave);
            Assert.That(reloaded.GetCompletedMemorySteps(character.characterId), Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(reloaded);
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void ApplyingPracticeMemoryCopiesTheDraftIntoThePracticeState()
    {
        var state = ScriptableObject.CreateInstance<TrainingProgressionState>();
        try
        {
            var memory = state.CaptureMemory("practice");
            memory.TrySelectStep(54);
            Assert.That(state.GetCompletedMemorySteps("practice"), Is.Zero);

            state.SetPotentials(new[] { new TrainingConfiguration.CharacterPotential { characterId = "practice", memory = memory } });
            memory.TrySelectStep(1, PotentialTrack.Passive);
            Assert.That(state.GetCompletedMemorySteps("practice"), Is.EqualTo(54));
            Assert.That(state.CaptureMemory("practice").GetChoice(1), Is.EqualTo(PotentialTrack.Skill));
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void PagesUnlockOnlyAfterTheLastStepOfThePreviousPage()
    {
        var memory = new CharacterMemoryProgress("practice");
        Assert.That(memory.LastUnlockedPage, Is.Zero);
        memory.TrySelectStep(17);
        Assert.That(memory.LastUnlockedPage, Is.Zero);
        memory.TrySelectStep(18);
        Assert.That(memory.LastUnlockedPage, Is.EqualTo(1));
        memory.TrySelectStep(35);
        Assert.That(memory.LastUnlockedPage, Is.EqualTo(1));
        memory.TrySelectStep(36);
        Assert.That(memory.LastUnlockedPage, Is.EqualTo(2));
        memory.TrySelectStep(54);
        Assert.That(memory.LastUnlockedPage, Is.EqualTo(2));
    }

    [Test]
    public void InspectorHasOneBoxPerMajorUpgradeInTwoRowsOfNine()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/UI/TeamCanvas.prefab");
        var info = prefab.GetComponentInChildren<CharacterInfoPanel>(true);
        var boxes = new SerializedObject(info).FindProperty("potentialSegments");
        Assert.That(boxes.arraySize, Is.EqualTo(18));
        for (int i = 0; i < boxes.arraySize; i++)
        {
            var box = (Image)boxes.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(box.rectTransform.anchorMin.x, Is.EqualTo(i % 9 / 9f).Within(.001f));
            Assert.That(box.rectTransform.anchorMin.y, Is.EqualTo(i < 9 ? .55f : 0f));
        }
    }

    [UnityTest]
    public IEnumerator InspectorOpensMemoryWhenThePrefabHasNoCanvasGroup()
    {
        yield return new EnterPlayMode();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/UI/TeamCanvas.prefab");
        var canvas = new GameObject("Memory Inspector Test", typeof(RectTransform), typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
        var panel = Object.Instantiate(prefab.GetComponentInChildren<CharacterPotentialPanel>(true), canvas.transform);
        var state = ScriptableObject.CreateInstance<CharacterProgressionState>();
        var character = ScriptableObject.CreateInstance<CharacterObject>();
        var item = ScriptableObject.CreateInstance<CharacterItem>();
        character.characterId = "character";
        item.characterObjectData = character;
        try
        {
            Assert.That(panel.TryGetComponent<CanvasGroup>(out _), Is.False);
            var serialized = new SerializedObject(panel);
            var scroll = (ScrollRect)serialized.FindProperty("contentScrollRect").objectReferenceValue;
            RectTransform potentialContent = scroll.content;
            Vector2 potentialViewportOffset = scroll.viewport.offsetMax;
            var upgradeWindow = (CharacterPotentialUpgradeWindow)serialized.FindProperty("upgradeWindow").objectReferenceValue;
            var nodeSlots = serialized.FindProperty("nodeSlots");
            var potentialNode = (CharacterPotentialNodeUI)nodeSlots.GetArrayElementAtIndex(0).objectReferenceValue;
            serialized.FindProperty("metaProgression").objectReferenceValue = state;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.Show();
            panel.PopulateData(item, null);
            var memoryPanel = panel.GetComponentInChildren<CharacterMemoryPanel>();
            Assert.That(memoryPanel.isActiveAndEnabled, Is.True);
            Assert.That(panel.transform.Find("SKILLTrackTitle").gameObject.activeSelf, Is.False);
            Assert.That(scroll.content, Is.SameAs(memoryPanel.transform));
            Assert.That(potentialContent.gameObject.activeSelf, Is.False);
            Assert.That(scroll.horizontal && scroll.vertical, Is.True);
            Assert.That(scroll.content.rect.width, Is.GreaterThan(scroll.viewport.rect.width));
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.001f));
            var rows = scroll.content.Find("Connections and Major Nodes");
            Assert.That(rows.Find("Step 19"), Is.Null);
            var first = (RectTransform)rows.Find("Step 1/Skill Memory");
            var pages = serialized.FindProperty("genericMemoryPages");
            var pageOne = (RectTransform)pages.GetArrayElementAtIndex(0).objectReferenceValue;
            var final = pageOne.Find("Generic Memory 18").GetComponent<Button>();
            Assert.That(first.sizeDelta, Is.EqualTo(((RectTransform)potentialNode.transform).sizeDelta));
            Assert.That(((RectTransform)first.parent).anchoredPosition.y,
                Is.LessThan(((RectTransform)final.transform).anchoredPosition.y));
            Assert.That(final.navigation.selectOnDown.name, Is.EqualTo("Generic Memory 17"));
            var goToTop = scroll.transform.Find("Go to Top").GetComponent<Button>();
            var goToBottom = scroll.transform.Find("Go to Bottom").GetComponent<Button>();
            Assert.That(final.navigation.selectOnUp, Is.SameAs(goToTop));
            Assert.That(scroll.viewport.offsetMax.y, Is.LessThan(potentialViewportOffset.y));
            scroll.velocity = new Vector2(10f, 50f);
            goToTop.onClick.Invoke();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.001f));
            Assert.That(scroll.velocity, Is.EqualTo(Vector2.zero));
            goToBottom.onClick.Invoke();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.001f));
            final.onClick.Invoke();
            Assert.That(upgradeWindow.gameObject.activeSelf, Is.True);
            var window = new SerializedObject(upgradeWindow);
            var upgrade = (Button)window.FindProperty("upgradeButton").objectReferenceValue;
            Assert.That(upgrade.gameObject.activeInHierarchy, Is.False);
            Assert.That(state.GetCompletedMemorySteps(character.characterId), Is.Zero);
            var pageTwoButton = panel.transform.Find("Memory Page II").GetComponent<Button>();
            var pageThreeButton = panel.transform.Find("Memory Page III").GetComponent<Button>();
            var notice = panel.transform.Find("Memory Unlock Notice").GetComponent<TMP_Text>();
            Assert.That(pageTwoButton.transform.Find("Lock Icon").gameObject.activeSelf, Is.True);
            Assert.That(pageThreeButton.transform.Find("Lock Icon").gameObject.activeSelf, Is.True);
            pageTwoButton.onClick.Invoke();
            Assert.That(notice.gameObject.activeSelf, Is.True);
            Assert.That(notice.text, Is.EqualTo("Complete Page I to Unlock"));
            Assert.That(pageOne.gameObject.activeSelf && memoryPanel.isActiveAndEnabled && scroll.enabled, Is.True);
            Assert.That(scroll.content.localScale.x, Is.LessThan(1f));
            pageThreeButton.onClick.Invoke();
            Assert.That(notice.text, Is.EqualTo("Complete Page II to Unlock"));
            for (int step = 1; step <= CharacterMemoryProgress.StepsPerPage; step++)
                Assert.That(state.TryGrantMemory(character, step, PotentialTrack.Skill), Is.True);
            Assert.That(pageTwoButton.transform.Find("Lock Icon").gameObject.activeSelf, Is.False);
            Assert.That(pageThreeButton.transform.Find("Lock Icon").gameObject.activeSelf, Is.True);
            pageTwoButton.onClick.Invoke();
            Assert.That(notice.gameObject.activeSelf, Is.False);
            Assert.That(pageOne.gameObject.activeSelf, Is.False);
            Assert.That(((RectTransform)pages.GetArrayElementAtIndex(1).objectReferenceValue).gameObject.activeSelf, Is.True);
            goToTop.onClick.Invoke();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.001f));
            Assert.That(panel.transform.Find("Switch Upgrade View"), Is.Null);
            panel.Hide();
            Assert.That(scroll.content, Is.SameAs(potentialContent));
            Assert.That(goToTop.gameObject.activeSelf || goToBottom.gameObject.activeSelf, Is.False);
            Assert.That(scroll.viewport.offsetMax, Is.EqualTo(potentialViewportOffset));
            Assert.That(upgradeWindow.gameObject.activeSelf, Is.False);
            panel.Show();
            Assert.That(panel.TryGetComponent<CanvasGroup>(out _), Is.False);
            Assert.That(scroll.content, Is.SameAs(memoryPanel.transform));
            Assert.That(goToTop.isActiveAndEnabled && goToBottom.isActiveAndEnabled, Is.True);
            Assert.That(panel.GetComponentInChildren<CharacterMemoryPanel>().isActiveAndEnabled, Is.True);
            Assert.That(upgradeWindow.gameObject.activeSelf, Is.False);

            var rei = AssetDatabase.LoadAssetAtPath<CharacterObject>(
                "Assets/Project/Character/Hero/test_1_CameraGirl/CharacterObject_cg.asset");
            var reiItem = Object.Instantiate(item);
            try
            {
                reiItem.characterObjectData = rei;
                panel.PopulateData(reiItem, null);
                Assert.That(panel.transform.Find("Switch Upgrade View"), Is.Null);
                Assert.That(scroll.content, Is.SameAs(memoryPanel.transform));
                var firstReiNode = scroll.content.Find("Connections and Major Nodes/Step 1/Skill Memory").GetComponent<Button>();
                firstReiNode.onClick.Invoke();
                var description = (TMP_Text)window.FindProperty("descriptionText").objectReferenceValue;
                Assert.That(description.text, Is.EqualTo(CharacterPotentialPanel.FindPotentialNode(rei, PotentialTrack.Skill, 1).description));
            }
            finally { Object.Destroy(reiItem); }
        }
        finally
        {
            Object.Destroy(canvas);
            Object.Destroy(item);
            Object.Destroy(character);
            Object.Destroy(state);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RewardConfirmsOneFamilyAndCentersTheNextGenericCard()
    {
        yield return new EnterPlayMode();
        var prefab = Resources.Load<ArtifactRewardPickerUI>(ArtifactRewardPickerUI.ResourcePath);
        var character = ScriptableObject.CreateInstance<CharacterObject>();
        var item = ScriptableObject.CreateInstance<CharacterItem>();
        character.characterId = "reward-test";
        item.characterObjectData = character;
        var progress = new CharacterMemoryProgress(character.characterId);
        progress.TrySelectStep(18);
        var present = typeof(ArtifactRewardPickerUI).GetMethod("PresentMemory",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        ArtifactRewardPickerUI picker = null;
        try
        {
            for (int reward = 0; reward < 2; reward++)
            {
                int step = progress.CompletedSteps + 1;
                int claims = 0;
                picker = Object.Instantiate(prefab);
                var candidates = new[] { (item, "Test Character", progress.Copy()) };
                System.Func<int, PotentialTrack, bool> claim = (_, family) =>
                {
                    claims++;
                    return progress.TrySelectStep(step, family);
                };
                picker.StartCoroutine((IEnumerator)present.Invoke(picker, new object[]
                    { candidates, claim, (System.Func<bool>)(() => picker != null) }));
                var group = picker.GetComponent<CanvasGroup>();
                yield return WaitForMemoryInput(group);
                var roster = picker.transform.Find("Living Characters");
                var portrait = roster.GetComponentInChildren<Button>();
                Assert.That(portrait.transform.Find("HeaderDivider").gameObject.activeSelf, Is.False);
                portrait.onClick.Invoke();
                yield return null;
                yield return WaitForMemoryInput(group);

                var row = picker.transform.Find("Memory Choices");
                bool major = CharacterMemoryProgress.IsChoiceStep(step);
                var choices = row.GetComponentsInChildren<Button>();
                Assert.That(choices.Length, Is.EqualTo(major ? 3 : 1));
                Button chosen;
                if (major)
                {
                    Assert.That(choices[0].interactable, Is.False);
                    chosen = choices[2];
                }
                else
                {
                    chosen = choices[0];
                    float center = row.InverseTransformPoint(chosen.transform.position).x;
                    Assert.That(center, Is.EqualTo(0f).Within(1f));
                }
                Assert.That(chosen.transform.Find("HeaderDivider").gameObject.activeSelf, Is.True);
                Assert.That(chosen.transform.Find("DetailsDivider").gameObject.activeSelf, Is.True);
                Assert.That(chosen.transform.Find("RarityPanel").gameObject.activeSelf, Is.False);
                var details = chosen.GetComponentInChildren<ScrollRect>();
                Assert.That(details.content.GetChild(0).name, Is.EqualTo("Passive"));
                var footer = chosen.transform.Find("Stats").GetComponent<TMP_Text>();
                Assert.That(footer.gameObject.activeSelf, Is.EqualTo(major));
                if (major)
                {
                    Assert.That(footer.text, Does.Match(@"^Rank \d/6$"));
                    Assert.That(footer.alignment, Is.EqualTo(TextAlignmentOptions.Center));
                    Assert.That(footer.rectTransform.anchorMax.y, Is.LessThan(details.viewport.anchorMin.y));
                }
                chosen.onClick.Invoke();
                Assert.That(claims, Is.Zero);
                chosen.onClick.Invoke();
                chosen.onClick.Invoke();
                Assert.That(claims, Is.EqualTo(1));
                Assert.That(progress.CompletedSteps, Is.EqualTo(step));
                Assert.That(progress.GetChoice(19), Is.EqualTo(PotentialTrack.Passive));
                Object.Destroy(picker.gameObject);
                yield return null;
            }
        }
        finally
        {
            if (picker != null) Object.Destroy(picker.gameObject);
            Object.Destroy(item);
            Object.Destroy(character);
        }
        yield return new ExitPlayMode();
    }

    private static IEnumerator WaitForMemoryInput(CanvasGroup group)
    {
        float deadline = Time.realtimeSinceStartup + 4f;
        while (!group.interactable && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(group.interactable, Is.True);
    }

    private static void AssertSixPerFamily(CharacterMemoryProgress memory)
    {
        int[] count = new int[CharacterMemoryProgress.ChoiceCount];
        for (int step = 1; step <= memory.CompletedSteps; step++)
            if (CharacterMemoryProgress.IsChoiceStep(step)) count[(int)memory.GetChoice(step)]++;
        Assert.That(count, Is.All.EqualTo(6));
    }

    [Test]
    public void AuthoredHardPatternsAdvertiseMemoryWithTheirExistingPayout()
    {
        var tables = Resources.Load<RouteOfferTables>("MapSO/RouteOfferTables");
        Assert.That(tables, Is.Not.Null);
        Assert.That(tables.HardRewardPatterns.Count, Is.Positive);
        foreach (RouteOfferTables.HardRewardPattern pattern in tables.HardRewardPatterns)
        {
            Assert.That(pattern.grantsMemory, Is.True);
            var reward = RouteReward.Patterned(219, pattern.artifactPulls, 0, 0, pattern.grantsMemory);
            Assert.That(reward.BuildLabel(), Does.Contain("Memory"));
            Assert.That(reward.ArtifactPulls, Is.EqualTo(pattern.artifactPulls));
        }
    }
}
#endif
