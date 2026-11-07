#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using Project.TeamBuilder.UI.Panels;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ArtifactUiPolishTests
{
    [Test]
    public void RewardDetailsScrollLongTextWithoutClaimingAndResetOnNextDraw()
    {
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        var picker = Object.Instantiate(Resources.Load<ArtifactRewardPickerUI>(ArtifactRewardPickerUI.ResourcePath));
        try
        {
            var definition = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/RarityR/R_RustedOathRing");
            var generator = new ArtifactRollGenerator(inventory.ArtifactRollTable);
            var choices = Enumerable.Range(0, 3).Select(i => generator.Generate(definition, 0, 100 + i, 2)).ToArray();
            int claims = 0;
            picker.Present(choices, inventory, 1, 2, _ => { claims++; return true; }).MoveNext();
            var card = picker.transform.Find("Card1");
            card.gameObject.SetActive(true);
            var scroll = card.GetComponentInChildren<ScrollRect>(true);
            var passive = card.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Passive");
            passive.text = string.Join(" ", Enumerable.Repeat("A long passive effect that must wrap and remain readable.", 40));
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(passive.enableAutoSizing, Is.False);
            Assert.That(card.Find("Icon").IsChildOf(scroll.content), Is.False);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            scroll.OnScroll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { scrollDelta = new Vector2(0f, -5f) });
            Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(0f));
            Assert.That(claims, Is.Zero);
            picker.Present(choices, inventory, 2, 2, _ => false).MoveNext();
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(0f).Within(.1f));
            Assert.That(card.GetComponentsInChildren<ScrollRect>(true).Length, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(picker.gameObject); Object.DestroyImmediate(inventory); }
    }
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);

    [Test]
    public void RewardCardsRollRarityIndependentlyWithoutDuplicateDefinitionsAndKeepSeededRolls()
    {
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        var draft = typeof(RoomWaveManager).Assembly.GetType("Project.Core.ZoneState.ArtifactRewardDraft");
        var prepare = draft.GetMethod("TryPrepare", BindingFlags.Public | BindingFlags.Static);
        try
        {
            bool foundMixed = false;
            for (int seed = 0; seed < 64; seed++)
            {
                var terms = RouteReward.Patterned(0, 2, seed, 2);
                object[] args = { terms, inventory.ArtifactRollTable, null };
                Assert.That(prepare.Invoke(null, args), Is.True);
                var pulls = (ArtifactInstance[][])args[2];
                object[] replay = { terms, inventory.ArtifactRollTable, null };
                Assert.That(prepare.Invoke(null, replay), Is.True);
                var repeated = (ArtifactInstance[][])replay[2];
                for (int p = 0; p < pulls.Length; p++)
                {
                    Assert.That(pulls[p].Select(a => a.definitionId).Distinct().Count(), Is.EqualTo(3));
                    var rarities = pulls[p].Select(a => inventory.ResolveArtifactDefinition(a.definitionId).rarity).ToArray();
                    CollectionAssert.DoesNotContain(rarities, ArtifactRarity.SSR);
                    foundMixed |= rarities.Distinct().Count() > 1;
                    for (int c = 0; c < pulls[p].Length; c++)
                    {
                        Assert.That(repeated[p][c].definitionId, Is.EqualTo(pulls[p][c].definitionId));
                        Assert.That(repeated[p][c].rolls, Is.EqualTo(pulls[p][c].rolls));
                    }
                }
            }
            Assert.That(foundMixed, Is.True, "At least one pull must mix R and SR with the authored late-biome weights.");
            object[] early = { RouteReward.Patterned(0, 2, 91, 0), inventory.ArtifactRollTable, null };
            Assert.That(prepare.Invoke(null, early), Is.True);
            Assert.That(((ArtifactInstance[][])early[2]).SelectMany(p => p)
                .All(a => inventory.ResolveArtifactDefinition(a.definitionId).rarity == ArtifactRarity.R), Is.True);
        }
        finally { Object.DestroyImmediate(inventory); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RaritySortingUsesDefinitionRarityInBothDirections(bool descending)
    {
        var definitions = new Dictionary<string, ArtifactDefinition>();
        try
        {
            var input = new List<ArtifactInstance>();
            foreach (var rarity in new[] { ArtifactRarity.SR, ArtifactRarity.R, ArtifactRarity.SSR })
            {
                var definition = ScriptableObject.CreateInstance<ArtifactDefinition>();
                definition.rarity = rarity;
                definitions.Add(rarity.ToString(), definition);
                input.Add(new ArtifactInstance { definitionId = rarity.ToString(), instanceId = rarity.ToString() });
            }
            var query = ArtifactInventoryQuery.Default;
            query.sortMode = ArtifactSortMode.Rarity;
            query.descending = descending;
            var results = new List<ArtifactInstance>();
            ArtifactInventoryQueryEngine.Execute(input, query, null, id => definitions[id], null, results);
            Assert.That(results.Select(a => definitions[a.definitionId].rarity).ToArray(), Is.EqualTo(descending
                ? new[] { ArtifactRarity.SSR, ArtifactRarity.SR, ArtifactRarity.R }
                : new[] { ArtifactRarity.R, ArtifactRarity.SR, ArtifactRarity.SSR }));
        }
        finally { foreach (var definition in definitions.Values) Object.DestroyImmediate(definition); }
    }

    [Test]
    public void SelectingAnotherRewardDoesNotClaimAndFailedClaimCanBeRetriedOnce()
    {
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        var picker = Object.Instantiate(Resources.Load<ArtifactRewardPickerUI>(ArtifactRewardPickerUI.ResourcePath));
        try
        {
            var definition = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/RarityR/R_RustedOathRing");
            var generator = new ArtifactRollGenerator(inventory.ArtifactRollTable);
            var choices = Enumerable.Range(0, 3).Select(i => generator.Generate(definition, 0, 100 + i, 2)).ToArray();
            int calls = 0;
            int chosen = -1;
            var routine = picker.Present(choices, inventory, 1, 1, index => { chosen = index; return ++calls > 1; });
            Assert.That(routine.MoveNext(), Is.True);
            picker.GetComponent<CanvasGroup>().interactable = true;
            var first = picker.transform.Find("Card1").GetComponent<Button>();
            var second = picker.transform.Find("Card2").GetComponent<Button>();
            first.onClick.Invoke(); second.onClick.Invoke();
            Assert.That(calls, Is.Zero);
            Assert.That(picker.transform.Find("Card2").GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Passive").richText, Is.True);
            second.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(picker.GetComponent<CanvasGroup>().interactable, Is.True);
            second.onClick.Invoke(); second.onClick.Invoke(); first.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(chosen, Is.EqualTo(1));
            Assert.That(picker.GetComponent<CanvasGroup>().interactable, Is.False);
        }
        finally { Object.DestroyImmediate(picker.gameObject); Object.DestroyImmediate(inventory); }
    }

    [Test]
    public void DiscardDismissalImmediatelyReleasesRaycastsAndActiveReferenceOnEveryCycle()
    {
        for (int i = 0; i < 3; i++)
        {
            var dialog = Object.Instantiate(Resources.Load<ArtifactCopyMenuUI>(ArtifactCopyMenuUI.DialogPath));
            try
            {
                int closed = 0;
                dialog.Closed += _ => closed++;
                dialog.OpenDiscard("test", () => true);
                Assert.That(dialog.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                Assert.That(ArtifactCopyMenuUI.TryDismissActive(), Is.True);
                Assert.That(dialog.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(dialog.GetComponent<CanvasGroup>().interactable, Is.False);
                Assert.That(ArtifactCopyMenuUI.TryDismissActive(), Is.False);
                Assert.That(closed, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(dialog.gameObject); }
        }
    }

    [Test]
    public void PassivePotentialMirrorsTheWindowLeftAndOtherTracksRestoreTheOriginalPose()
    {
        var root = PrefabUtility.LoadPrefabContents(TeamBuilderInventoryAuthoring.PrefabPath);
        try
        {
            var window = root.GetComponentInChildren<CharacterPotentialUpgradeWindow>(true);
            var rect = (RectTransform)window.transform;
            Vector2 anchor = rect.anchorMin, position = rect.anchoredPosition, pivot = rect.pivot;
            window.Show(null, null, new PotentialNodeDefinition { track = PotentialTrack.Passive }, null);
            Assert.That(rect.anchorMin.x, Is.EqualTo(1f - anchor.x));
            Assert.That(rect.pivot.x, Is.EqualTo(1f - pivot.x));
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(-position.x));
            foreach (var track in new[] { PotentialTrack.Skill, PotentialTrack.Ultimate })
            {
                window.Show(null, null, new PotentialNodeDefinition { track = track }, null);
                Assert.That(rect.anchorMin, Is.EqualTo(anchor));
                Assert.That(rect.pivot, Is.EqualTo(pivot));
                Assert.That(rect.anchoredPosition, Is.EqualTo(position));
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void ArtifactPreviewRendersRichTextAndKeepsCompactRowsInsideItsStatsPanel()
    {
        var root = PrefabUtility.LoadPrefabContents(TeamBuilderInventoryAuthoring.PrefabPath);
        var definition = ScriptableObject.CreateInstance<ArtifactDefinition>();
        try
        {
            definition.itemName = "Rich text test";
            definition.passiveDescription = "<b>Attack</b> <color=#FFCC00>bonus</color>";
            definition.description = "<i>Artifact lore</i>";
            var panel = root.GetComponentInChildren<ArtifactGridPanel>(true);
            var preview = Get<ArtifactPreviewPanel>(panel, "previewPanel");
            for (Transform ancestor = panel.transform; ancestor != null; ancestor = ancestor.parent)
                ancestor.gameObject.SetActive(true);
            preview.Open(new ArtifactInstance { rolls = new List<ArtifactRoll>
            {
                new ArtifactRoll(ArtifactStatKind.AdditionalDamageIncrease, 1234, ArtifactRollSource.Generated),
                new ArtifactRoll(ArtifactStatKind.FlatMaxHP, 99999, ArtifactRollSource.Generated)
            } }, definition, null);
            var description = Get<TMP_Text>(preview, "descriptionText");
            var textInfo = description.GetTextInfo(description.text);
            Assert.That(description.richText, Is.True);
            string parsed = new string(textInfo.characterInfo.Take(textInfo.characterCount).Select(c => c.character).ToArray());
            Assert.That(parsed, Does.Contain("Attack bonus").And.Not.Contain("<b>"));
            var stats = (RectTransform)Get<Transform>(preview, "statItemContainer");
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)preview.transform);
            var rows = stats.GetComponentsInChildren<StatItemUI>();
            Assert.That(rows.Length, Is.EqualTo(2));
            typeof(TeamBuilderInventoryValidation).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { root, "artifact-rich-text-stats", 1280, 720 });
            foreach (var row in rows)
            {
                var rect = (RectTransform)row.transform;
                Assert.That(rect.rect.width, Is.LessThanOrEqualTo(stats.rect.width + .01f));
                foreach (var text in row.GetComponentsInChildren<TMP_Text>())
                {
                    var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                    string bounds = $"{row.name}/{text.name}: row={rect.rect}, text={text.rectTransform.rect}, anchors={text.rectTransform.anchorMin}/{text.rectTransform.anchorMax}, position={text.rectTransform.anchoredPosition}, scale={text.rectTransform.localScale}";
                    Assert.That(rect.InverseTransformPoint(corners[0]).x, Is.GreaterThanOrEqualTo(rect.rect.xMin - .01f), bounds);
                    Assert.That(rect.InverseTransformPoint(corners[2]).x, Is.LessThanOrEqualTo(rect.rect.xMax + .01f), bounds);
                }
            }
            var favourite = Get<Button>(panel, "favouriteFilterButton");
            Assert.That(favourite == null || !favourite.gameObject.activeSelf, Is.True);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); Object.DestroyImmediate(definition); }
    }
}
#endif
