using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using Project.Core.ZoneState;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class TeamRuntimeStatFeedbackTests
{
    [Test]
    public void TeamLoader_StoresArtifactsAndRefreshesRuntimeStats()
    {
        GameObject playerObject = new GameObject("Test Player");
        GameObject loaderObject = new GameObject("Test TeamLoader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        Artifact artifact = ScriptableObject.CreateInstance<Artifact>();

        try
        {
            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            playerCharacter.PassiveController = passiveController;
            passiveController.playerCharacter = playerCharacter;

            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);

            artifact.bonusStats = CreateZeroBonusStats();
            artifact.bonusStats.maxHP = 50f;
            artifact.bonusStats.cooldownRate = 50f;

            teamLoader.DeployLoadout(
                new[] { character },
                0,
                new[] { new[] { artifact } });

            Assert.That(teamLoader.GetEquippedArtifactsAt(0)[0], Is.SameAs(artifact));
            Assert.That(playerCharacter.MaxHP, Is.EqualTo(150f).Within(0.001f));
            Assert.That(playerCharacter.Stats.cooldownRate, Is.EqualTo(50f).Within(0.001f));
            Assert.That(runInventory.GetTeamRunCharacterAt(0).character, Is.SameAs(character));
            playerCharacter.SetCurrentHP(140f);
            artifact.bonusStats.maxHP = 0f;

            teamLoader.RefreshRuntimeState();

            Assert.That(playerCharacter.MaxHP, Is.EqualTo(100f).Within(0.001f));
            Assert.That(playerCharacter.CurrentHP, Is.EqualTo(100f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(artifact);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void TeamSlotTrend_ClassifiesCdrOnlyEquipAndRemove()
    {
        MethodInfo calculateTrend = typeof(TeamSlotUI).GetMethod(
            "CalculateStatTrend",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(calculateTrend, Is.Not.Null);

        CharacterStats baseline = new CharacterStats();
        CharacterStats upgraded = baseline.Clone();
        upgraded.cooldownRate += 50f;

        float equipTrend = (float)calculateTrend.Invoke(null, new object[] { baseline, upgraded });
        float removeTrend = (float)calculateTrend.Invoke(null, new object[] { upgraded, baseline });

        Assert.That(equipTrend, Is.GreaterThan(0f));
        Assert.That(removeTrend, Is.LessThan(0f));
    }

    [Test]
    public void TeamLoader_AppliesTeamScopePotentialStatModifiers()
    {
        GameObject playerObject = new GameObject("Potential Stat Test Player");
        GameObject loaderObject = new GameObject("Potential Stat Test Loader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject leader = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterObject ally = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            leader.characterId = "leader";
            leader.stats.attack = 100f;
            ally.characterId = "ally";
            ally.stats.attack = 100f;
            leader.kitDefinition.basePassiveNode.statModifiers.Add(new PassiveStatModifierDefinition
            {
                target = PassiveModifierTarget.AttackStat,
                operation = PassiveNumericOperation.Add,
                value = 10f,
                scope = PassiveEffectScope.Team
            });

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);

            teamLoader.DeployLoadout(new[] { leader, ally }, 0);

            Assert.That(teamLoader.GetCharacterRuntimeStats(leader).attack, Is.EqualTo(110f).Within(0.001f));
            Assert.That(teamLoader.GetCharacterRuntimeStats(ally).attack, Is.EqualTo(110f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(ally);
            Object.DestroyImmediate(leader);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [UnityTest]
    public IEnumerator TeamSlot_CdrOnlyArtifactFlashesGreenThenRed()
    {
        GameObject builderObject = new GameObject("Test TeamBuilder");
        GameObject slotObject = new GameObject("Test TeamSlot", typeof(RectTransform));
        GameObject portraitObject = new GameObject(
            "Test Portrait",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        GameObject artifactSlotObject = new GameObject(
            "Test ArtifactSlot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ArtifactSlotUI));
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterItem characterItem = ScriptableObject.CreateInstance<CharacterItem>();
        Artifact artifact = ScriptableObject.CreateInstance<Artifact>();

        try
        {
            portraitObject.transform.SetParent(slotObject.transform, false);
            artifactSlotObject.transform.SetParent(slotObject.transform, false);

            TeamBuilderUI builder = builderObject.AddComponent<TeamBuilderUI>();
            SetPrivateField(builder, "currentMode", ZoneMode.SafeZone);

            TeamSlotUI teamSlot = slotObject.AddComponent<TeamSlotUI>();
            ArtifactSlotUI artifactSlot = artifactSlotObject.GetComponent<ArtifactSlotUI>();
            Image portrait = portraitObject.GetComponent<Image>();
            SetPrivateField(teamSlot, "profileImage", portrait);
            SetPrivateField(teamSlot, "artifactSlots", new[] { artifactSlot });

            characterItem.characterObjectData = character;
            artifact.bonusStats = CreateZeroBonusStats();
            artifact.bonusStats.cooldownRate = 50f;

            teamSlot.Init(builder, 0);
            teamSlot.SetCharacter(characterItem);

            artifactSlot.SetArtifact(artifact);
            teamSlot.RecalculateStats();
            yield return null;

            Assert.That(portrait.color.g, Is.GreaterThan(portrait.color.r));

            yield return new WaitForSecondsRealtime(0.35f);

            artifactSlot.ClearArtifact();
            teamSlot.RecalculateStats();
            yield return null;

            Assert.That(portrait.color.r, Is.GreaterThan(portrait.color.g));
        }
        finally
        {
            Object.DestroyImmediate(artifact);
            Object.DestroyImmediate(characterItem);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(slotObject);
            Object.DestroyImmediate(builderObject);
        }
    }

    private static CharacterStats CreateZeroBonusStats()
    {
        return new CharacterStats
        {
            maxHP = 0f,
            attack = 0f,
            defense = 0f,
            speed = 0f,
            elementalMastery = 0f,
            critRate = 0f,
            critDamage = 0f,
            cooldownRate = 0f,
            impact = 0f,
            aeRecharge = 0f
        };
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
