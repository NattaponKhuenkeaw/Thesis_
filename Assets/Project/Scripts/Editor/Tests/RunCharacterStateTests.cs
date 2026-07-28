using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using TMPro;
using UnityEngine;

public class RunCharacterStateTests
{
    [Test]
    public void CharacterRunUpgradeStartsFromDefinitionGrowthButLevelOne()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        character.upgrade.level = 12;
        character.upgrade.currentExp = 34;
        character.upgrade.maxLevel = 40;
        character.upgrade.attackPerLevel = 2f;

        CharacterItem characterItem = ScriptableObject.CreateInstance<CharacterItem>();
        characterItem.characterObjectData = character;

        inventory.TryAddItem(characterItem);
        CharacterUpgrade runUpgrade = inventory.GetCharacterUpgrade(character);

        Assert.That(runUpgrade, Is.Not.Null);
        Assert.That(runUpgrade.CurrentLevel, Is.EqualTo(1));
        Assert.That(runUpgrade.currentExp, Is.EqualTo(0));
        Assert.That(runUpgrade.MaxLevel, Is.EqualTo(40));
        Assert.That(runUpgrade.attackPerLevel, Is.EqualTo(2f));
        Assert.That(character.upgrade.CurrentLevel, Is.EqualTo(12));
        Assert.That(character.upgrade.currentExp, Is.EqualTo(34));

        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(character);
        Object.DestroyImmediate(characterItem);
    }

    [Test]
    public void RunUpgradeMutationDoesNotMutateCharacterDefinition()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        character.upgrade.level = 1;
        character.upgrade.currentExp = 0;
        character.upgrade.attackPerLevel = 1f;

        CharacterItem characterItem = ScriptableObject.CreateInstance<CharacterItem>();
        characterItem.characterObjectData = character;

        inventory.TryAddItem(characterItem);
        CharacterUpgrade runUpgrade = inventory.GetCharacterUpgrade(character);
        runUpgrade.AddExp(CharacterUpgrade.GetRequiredEXP(1));

        Assert.That(runUpgrade.CurrentLevel, Is.EqualTo(2));
        Assert.That(character.upgrade.CurrentLevel, Is.EqualTo(1));
        Assert.That(character.upgrade.currentExp, Is.EqualTo(0));

        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(character);
        Object.DestroyImmediate(characterItem);
    }

    [Test]
    public void CharacterStatsResolveFromRunUpgradeWithoutMutatingDefinition()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        character.stats.attack = 10f;
        character.stats.defense = 5f;
        character.stats.maxHP = 100f;
        character.upgrade.level = 1;
        character.upgrade.currentExp = 0;
        character.upgrade.attackPerLevel = 3f;
        character.upgrade.defensePerLevel = 2f;
        character.upgrade.maxHpPerLevel = 20f;

        CharacterItem characterItem = ScriptableObject.CreateInstance<CharacterItem>();
        characterItem.characterObjectData = character;

        inventory.TryAddItem(characterItem);
        CharacterUpgrade runUpgrade = inventory.GetCharacterUpgrade(character);
        runUpgrade.AddExp(CharacterUpgrade.GetRequiredEXP(1));

        CharacterStats runStats = inventory.GetCharacterStats(character);
        CharacterStats definitionStats = character.GetLeveledStats();

        Assert.That(runStats.attack, Is.EqualTo(13f));
        Assert.That(runStats.defense, Is.EqualTo(7f));
        Assert.That(runStats.maxHP, Is.EqualTo(120f));
        Assert.That(definitionStats.attack, Is.EqualTo(10f));
        Assert.That(definitionStats.defense, Is.EqualTo(5f));
        Assert.That(definitionStats.maxHP, Is.EqualTo(100f));

        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(character);
        Object.DestroyImmediate(characterItem);
    }

    [Test]
    public void DeployedTeamSlotResolvesRunCharacterState()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        inventory.DeployTeam(new[] { character }, 0);

        RunCharacterState state = inventory.GetTeamRunCharacterAt(0);

        Assert.That(state, Is.Not.Null);
        Assert.That(state.character, Is.SameAs(character));
        Assert.That(inventory.GetTeamSnapshot()[0], Is.SameAs(character));

        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(character);
    }

    [Test]
    public void RuntimeEffectStatBonusAppliesAndExpiresFromRunState()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        StatRuntimeEffectDefinition effect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();

        try
        {
            character.stats.attack = 100f;
            effect.durationType = RuntimeEffectDurationType.Seconds;
            effect.durationSeconds = 1f;
            effect.statModifiers.Add(new RuntimeEffectStatModifier
            {
                target = RuntimeEffectStatTarget.Attack,
                operation = RuntimeEffectStatOperation.AddSourceStatRatio,
                sourceStat = RuntimeEffectStatTarget.Attack,
                value = 0.2f
            });

            inventory.DeployTeam(new[] { character }, 0);
            inventory.ApplyRuntimeEffect(character, effect, character, character.stats);

            Assert.That(inventory.GetCharacterStats(character).attack, Is.EqualTo(120f).Within(0.001f));
            Assert.That(character.GetLeveledStats().attack, Is.EqualTo(100f).Within(0.001f));
            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(1));

            inventory.TickRuntimeEffects(1.1f);

            Assert.That(inventory.GetCharacterStats(character).attack, Is.EqualTo(100f).Within(0.001f));
            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void RuntimeEffectContainerAppliesChildEffectsAsSingleRunState()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        RuntimeEffectContainerDefinition container = ScriptableObject.CreateInstance<RuntimeEffectContainerDefinition>();
        StatRuntimeEffectDefinition attackEffect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();
        StatRuntimeEffectDefinition defenseEffect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();

        try
        {
            character.stats.attack = 100f;
            character.stats.defense = 50f;
            container.durationType = RuntimeEffectDurationType.Seconds;
            container.durationSeconds = 1f;
            container.effects.Add(attackEffect);
            container.effects.Add(defenseEffect);

            attackEffect.kind = RuntimeEffectKind.Effect;
            attackEffect.statModifiers.Add(new RuntimeEffectStatModifier
            {
                target = RuntimeEffectStatTarget.Attack,
                operation = RuntimeEffectStatOperation.AddSourceStatRatio,
                sourceStat = RuntimeEffectStatTarget.Attack,
                value = 0.2f
            });

            defenseEffect.kind = RuntimeEffectKind.Effect;
            defenseEffect.statModifiers.Add(new RuntimeEffectStatModifier
            {
                target = RuntimeEffectStatTarget.Defense,
                operation = RuntimeEffectStatOperation.Multiply,
                value = 1.1f
            });

            inventory.DeployTeam(new[] { character }, 0);
            inventory.ApplyRuntimeEffect(character, container, character, character.stats);

            CharacterStats boostedStats = inventory.GetCharacterStats(character);
            Assert.That(boostedStats.attack, Is.EqualTo(120f).Within(0.001f));
            Assert.That(boostedStats.defense, Is.EqualTo(55f).Within(0.001f));
            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(1));

            inventory.TickRuntimeEffects(1.1f);

            CharacterStats expiredStats = inventory.GetCharacterStats(character);
            Assert.That(expiredStats.attack, Is.EqualTo(100f).Within(0.001f));
            Assert.That(expiredStats.defense, Is.EqualTo(50f).Within(0.001f));
            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(defenseEffect);
            Object.DestroyImmediate(attackEffect);
            Object.DestroyImmediate(container);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void RuntimeEffectIconUI_BindsStackCountToQuantityText()
    {
        GameObject iconObject = new GameObject("RuntimeEffectIcon");
        GameObject quantityObject = new GameObject("QuantityText");
        RuntimeEffectIconUI icon = iconObject.AddComponent<RuntimeEffectIconUI>();
        RuntimeEffectDefinition effect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();
        RuntimeEffectInstance instance = new RuntimeEffectInstance();

        try
        {
            quantityObject.transform.SetParent(iconObject.transform, false);
            TMP_Text quantityText = quantityObject.AddComponent<TextMeshProUGUI>();
            instance.Initialize(effect, null, null);

            icon.Bind(instance, 3);

            Assert.That(quantityObject.activeSelf, Is.True);
            Assert.That(quantityText.text, Is.EqualTo("x3"));
        }
        finally
        {
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(iconObject);
        }
    }

    [Test]
    public void RuntimeEffectTimerTicksForOffFieldTeamMembers()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject activeCharacter = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterObject offFieldCharacter = ScriptableObject.CreateInstance<CharacterObject>();
        StatRuntimeEffectDefinition effect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();

        try
        {
            effect.durationType = RuntimeEffectDurationType.Seconds;
            effect.durationSeconds = 1f;

            inventory.DeployTeam(new[] { activeCharacter, offFieldCharacter }, 0);
            inventory.ApplyRuntimeEffect(activeCharacter, effect, activeCharacter, activeCharacter.stats);
            inventory.ApplyRuntimeEffect(offFieldCharacter, effect, activeCharacter, activeCharacter.stats);

            Assert.That(inventory.GetActiveRuntimeEffects(activeCharacter).Count, Is.EqualTo(1));
            Assert.That(inventory.GetActiveRuntimeEffects(offFieldCharacter).Count, Is.EqualTo(1));

            inventory.TickRuntimeEffects(1.1f);

            Assert.That(inventory.GetActiveRuntimeEffects(activeCharacter).Count, Is.EqualTo(0));
            Assert.That(inventory.GetActiveRuntimeEffects(offFieldCharacter).Count, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(offFieldCharacter);
            Object.DestroyImmediate(activeCharacter);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void RuntimeEffectStacksIndependentlyForEachTeamTarget()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject activeCharacter = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterObject offFieldCharacter = ScriptableObject.CreateInstance<CharacterObject>();
        StatRuntimeEffectDefinition effect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();

        try
        {
            effect.durationType = RuntimeEffectDurationType.Seconds;
            effect.durationSeconds = 10f;
            effect.stackable = true;
            effect.statModifiers.Add(new RuntimeEffectStatModifier
            {
                target = RuntimeEffectStatTarget.Attack,
                operation = RuntimeEffectStatOperation.Multiply,
                value = 1.07f
            });

            inventory.DeployTeam(new[] { activeCharacter, offFieldCharacter }, 0);
            inventory.ApplyRuntimeEffect(activeCharacter, effect, activeCharacter, activeCharacter.stats, -1f, RuntimeEffectStackMode.Stack, 99);
            inventory.ApplyRuntimeEffect(offFieldCharacter, effect, activeCharacter, activeCharacter.stats, -1f, RuntimeEffectStackMode.Stack, 99);
            inventory.ApplyRuntimeEffect(activeCharacter, effect, activeCharacter, activeCharacter.stats, -1f, RuntimeEffectStackMode.Stack, 99);
            inventory.ApplyRuntimeEffect(offFieldCharacter, effect, activeCharacter, activeCharacter.stats, -1f, RuntimeEffectStackMode.Stack, 99);

            Assert.That(inventory.GetActiveRuntimeEffects(activeCharacter)[0].stackCount, Is.EqualTo(2));
            Assert.That(inventory.GetActiveRuntimeEffects(offFieldCharacter)[0].stackCount, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(offFieldCharacter);
            Object.DestroyImmediate(activeCharacter);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void RuntimeEffectInstance_TracksAppliedDurationOverrideForUiFill()
    {
        StatRuntimeEffectDefinition effect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        RuntimeEffectInstance instance = new RuntimeEffectInstance();

        try
        {
            effect.durationType = RuntimeEffectDurationType.Seconds;
            effect.durationSeconds = 10f;

            instance.Initialize(effect, character, null, 5f);

            Assert.That(instance.remainingSeconds, Is.EqualTo(5f).Within(0.001f));
            Assert.That(instance.appliedDurationSeconds, Is.EqualTo(5f).Within(0.001f));

            instance.Refresh(2.5f);

            Assert.That(instance.remainingSeconds, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(instance.appliedDurationSeconds, Is.EqualTo(2.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(effect);
        }
    }

    [Test]
    public void RuntimeEffectShieldAbsorbsDamageAndExpiresWhenDepleted()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        ShieldRuntimeEffectDefinition shield = ScriptableObject.CreateInstance<ShieldRuntimeEffectDefinition>();

        try
        {
            character.stats.maxHP = 100f;
            shield.durationType = RuntimeEffectDurationType.Seconds;
            shield.durationSeconds = 30f;
            shield.valueSource = RuntimeEffectStatTarget.MaxHP;
            shield.valueRatio = 0.4f;
            shield.useSourceStats = true;

            inventory.DeployTeam(new[] { character }, 0);
            inventory.ApplyRuntimeEffect(character, shield, character, character.stats);

            RuntimeEffectInstance instance = inventory.GetActiveRuntimeEffects(character)[0];
            Assert.That(instance.remainingShieldAmount, Is.EqualTo(40f).Within(0.001f));

            int runtimeEffectNotifications = 0;
            inventory.OnRuntimeEffectsChanged += _ => runtimeEffectNotifications++;
            RuntimeEffectDamageContext context = new RuntimeEffectDamageContext();
            float firstDamage = inventory.ModifyIncomingDamage(character, context, 25f);
            Assert.That(firstDamage, Is.EqualTo(0f).Within(0.001f));
            Assert.That(context.shieldAbsorbedAmount, Is.EqualTo(25f).Within(0.001f));
            Assert.That(instance.remainingShieldAmount, Is.EqualTo(15f).Within(0.001f));
            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(1));
            Assert.That(runtimeEffectNotifications, Is.EqualTo(1));

            runtimeEffectNotifications = 0;
            float secondDamage = inventory.ModifyIncomingDamage(character, context, 20f);
            Assert.That(secondDamage, Is.EqualTo(5f).Within(0.001f));
            Assert.That(context.shieldAbsorbedAmount, Is.EqualTo(40f).Within(0.001f));
            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(0));
            Assert.That(runtimeEffectNotifications, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(shield);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void RuntimeEffectShieldReapplyAddsAbsorbAmount()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        ShieldRuntimeEffectDefinition shield = ScriptableObject.CreateInstance<ShieldRuntimeEffectDefinition>();

        try
        {
            character.stats.maxHP = 100f;
            RuntimeEffectRecipe recipe = new RuntimeEffectRecipe
            {
                definition = shield,
                recipeId = "test.additive-shield",
                policy = new RuntimeEffectApplicationPolicy
                {
                    kind = RuntimeEffectKind.Buff,
                    appliesOn = RuntimeEffectApplyTarget.Self,
                    durationType = RuntimeEffectDurationType.Seconds,
                    durationSeconds = 30f,
                    stackMode = RuntimeEffectStackMode.Override,
                    refreshDurationOnReapply = true
                },
                parameters = new RuntimeEffectParameterSet
                {
                    overrideShield = true,
                    shield = new RuntimeEffectShieldParameters
                    {
                        valueSource = RuntimeEffectStatTarget.MaxHP,
                        valueRatio = 0.4f,
                        useSourceStats = true
                    }
                }
            };

            inventory.DeployTeam(new[] { character }, 0);
            inventory.ApplyRuntimeEffect(character, recipe, character, character.stats);
            inventory.ApplyRuntimeEffect(character, recipe, character, character.stats);

            RuntimeEffectInstance instance = inventory.GetActiveRuntimeEffects(character)[0];
            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(1));
            Assert.That(instance.remainingShieldAmount, Is.EqualTo(80f).Within(0.001f));
            Assert.That(instance.appliedShieldAmount, Is.EqualTo(80f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(shield);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void RuntimeEffectShieldStateChannelAddsAcrossDifferentRecipesAndRefreshesIncomingDuration()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterObject secondSource = ScriptableObject.CreateInstance<CharacterObject>();
        ShieldRuntimeEffectDefinition shield = ScriptableObject.CreateInstance<ShieldRuntimeEffectDefinition>();

        try
        {
            character.stats.maxHP = 100f;
            secondSource.stats.maxHP = 100f;
            shield.durationType = RuntimeEffectDurationType.Seconds;
            shield.durationSeconds = 30f;

            RuntimeEffectRecipe largeShield = CreateShieldRecipe(shield, "test.skill-shield", 0.4f, 30f);
            RuntimeEffectRecipe smallShield = CreateShieldRecipe(shield, "test.grab-shield", 0.12f, 12f);

            inventory.DeployTeam(new[] { character, secondSource }, 0);
            inventory.ApplyRuntimeEffect(character, largeShield, character, character.stats);
            RuntimeEffectInstance first = inventory.GetRuntimeEffectByChannel(
                character,
                RuntimeEffectStateChannel.Shield);
            Assert.That(first, Is.Not.Null);

            inventory.ModifyIncomingDamage(character, new RuntimeEffectDamageContext(), 10f);
            inventory.TickRuntimeEffects(2f);
            inventory.ApplyRuntimeEffect(character, smallShield, secondSource, secondSource.stats);

            RuntimeEffectInstance instance = inventory.GetActiveRuntimeEffects(character)[0];
            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(1));
            Assert.That(instance, Is.SameAs(first));
            Assert.That(instance.StateChannel, Is.EqualTo(RuntimeEffectStateChannel.Shield));
            Assert.That(instance.stackCount, Is.EqualTo(1));
            Assert.That(instance.remainingShieldAmount, Is.EqualTo(42f).Within(0.001f));
            Assert.That(instance.appliedShieldAmount, Is.EqualTo(52f).Within(0.001f));
            Assert.That(instance.remainingSeconds, Is.EqualTo(12f).Within(0.001f));
            Assert.That(instance.appliedDurationSeconds, Is.EqualTo(12f).Within(0.001f));
            Assert.That(instance.recipe, Is.SameAs(smallShield));
            Assert.That(instance.sourceCharacter, Is.SameAs(secondSource));
            Assert.That(
                inventory.GetRuntimeEffectByChannel(character, RuntimeEffectStateChannel.Shield),
                Is.SameAs(instance));

            inventory.ApplyRuntimeEffect(
                secondSource,
                smallShield,
                secondSource,
                secondSource.stats);
            Assert.That(
                inventory.GetRuntimeEffectByChannel(secondSource, RuntimeEffectStateChannel.Shield)
                    .remainingShieldAmount,
                Is.EqualTo(12f).Within(0.001f));
            Assert.That(instance.remainingShieldAmount, Is.EqualTo(42f).Within(0.001f));

            inventory.TickRuntimeEffects(12.1f);
            Assert.That(inventory.GetActiveRuntimeEffects(character), Is.Empty);
            Assert.That(inventory.GetActiveRuntimeEffects(secondSource), Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(shield);
            Object.DestroyImmediate(secondSource);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void RuntimeEffectReceiverUsesOneShieldStateAcrossRecipes()
    {
        GameObject receiverObject = new GameObject("Shield Receiver");
        RuntimeEffectReceiver receiver = receiverObject.AddComponent<RuntimeEffectReceiver>();
        CharacterObject firstSource = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterObject secondSource = ScriptableObject.CreateInstance<CharacterObject>();
        ShieldRuntimeEffectDefinition shield = ScriptableObject.CreateInstance<ShieldRuntimeEffectDefinition>();

        try
        {
            firstSource.stats.maxHP = 100f;
            secondSource.stats.maxHP = 100f;
            RuntimeEffectRecipe largeShield = CreateShieldRecipe(shield, "receiver.large", 0.4f, 30f);
            RuntimeEffectRecipe smallShield = CreateShieldRecipe(shield, "receiver.small", 0.12f, 8f);

            RuntimeEffectInstance first =
                receiver.ApplyEffect(largeShield, firstSource, firstSource.stats);
            RuntimeEffectInstance reapplied =
                receiver.ApplyEffect(smallShield, secondSource, secondSource.stats);

            Assert.That(receiver.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(reapplied, Is.SameAs(first));
            Assert.That(receiver.GetEffectByChannel(RuntimeEffectStateChannel.Shield), Is.SameAs(first));
            Assert.That(first.remainingShieldAmount, Is.EqualTo(52f).Within(0.001f));
            Assert.That(first.stackCount, Is.EqualTo(1));
            Assert.That(first.remainingSeconds, Is.EqualTo(8f).Within(0.001f));
            Assert.That(first.sourceCharacter, Is.SameAs(secondSource));

            int changeNotifications = 0;
            receiver.OnEffectsChanged += () => changeNotifications++;
            float remainingDamage = receiver.ModifyIncomingDamage(
                new RuntimeEffectDamageContext(),
                100f);
            Assert.That(remainingDamage, Is.EqualTo(48f).Within(0.001f));
            Assert.That(receiver.ActiveEffects, Is.Empty);
            Assert.That(changeNotifications, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(shield);
            Object.DestroyImmediate(secondSource);
            Object.DestroyImmediate(firstSource);
            Object.DestroyImmediate(receiverObject);
        }
    }

    [Test]
    public void RuntimeEffectStateChannelDoesNotMergeDifferentNonShieldRecipes()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        DamageModifierRuntimeEffectDefinition effect =
            ScriptableObject.CreateInstance<DamageModifierRuntimeEffectDefinition>();

        try
        {
            RuntimeEffectRecipe first = new RuntimeEffectRecipe
            {
                definition = effect,
                recipeId = "non-channel.first",
                policy = new RuntimeEffectApplicationPolicy
                {
                    durationType = RuntimeEffectDurationType.Seconds,
                    durationSeconds = 10f,
                    stackMode = RuntimeEffectStackMode.Override
                }
            };
            RuntimeEffectRecipe second = new RuntimeEffectRecipe
            {
                definition = effect,
                recipeId = "non-channel.second",
                policy = new RuntimeEffectApplicationPolicy
                {
                    durationType = RuntimeEffectDurationType.Seconds,
                    durationSeconds = 10f,
                    stackMode = RuntimeEffectStackMode.Override
                }
            };

            inventory.DeployTeam(new[] { character }, 0);
            inventory.ApplyRuntimeEffect(character, first, character, character.stats);
            inventory.ApplyRuntimeEffect(character, second, character, character.stats);

            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void PeriodicRuntimeEffectOnlyAdvancesFormulaScheduleWhenDueAndCatchesUp()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        PeriodicHealRuntimeEffectDefinition effect =
            ScriptableObject.CreateInstance<PeriodicHealRuntimeEffectDefinition>();

        try
        {
            character.stats.maxHP = 100f;
            effect.durationType = RuntimeEffectDurationType.Seconds;
            effect.durationSeconds = 10f;
            effect.tickInterval = 1f;
            effect.flatValue = 1f;

            inventory.DeployTeam(new[] { character }, 0);
            inventory.ApplyRuntimeEffect(character, effect, character, character.stats);
            RuntimeEffectInstance instance = inventory.GetActiveRuntimeEffects(character)[0];

            inventory.TickRuntimeEffects(0.1f);
            Assert.That(instance.nextTickAt, Is.EqualTo(1f).Within(0.001f));

            inventory.TickRuntimeEffects(0.4f);
            Assert.That(instance.nextTickAt, Is.EqualTo(1f).Within(0.001f));

            inventory.TickRuntimeEffects(2.6f);
            Assert.That(instance.elapsedSeconds, Is.EqualTo(3.1f).Within(0.001f));
            Assert.That(instance.nextTickAt, Is.EqualTo(4f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void PlayerHudDoesNotBuildDisplayEntryForIconlessEffect()
    {
        GameObject hudObject = new GameObject("Runtime Effect HUD Test");
        PlayerHUD hud = hudObject.AddComponent<PlayerHUD>();
        StatRuntimeEffectDefinition effect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();
        RuntimeEffectInstance instance = new RuntimeEffectInstance();

        try
        {
            effect.kind = RuntimeEffectKind.Buff;
            effect.icon = null;
            instance.Initialize(effect, null, null);

            MethodInfo buildEntries = typeof(PlayerHUD).GetMethod(
                "BuildDisplayEntries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo entriesField = typeof(PlayerHUD).GetField(
                "runtimeEffectDisplayEntries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildEntries, Is.Not.Null);
            Assert.That(entriesField, Is.Not.Null);

            buildEntries.Invoke(hud, new object[] { new[] { instance } });
            ICollection entries = entriesField.GetValue(hud) as ICollection;
            Assert.That(entries, Is.Not.Null);
            Assert.That(entries.Count, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(hudObject);
        }
    }

    [Test]
    public void RuntimeEffectInstantHealAppliesValueAndExpires()
    {
        GameObject playerObject = new GameObject("Instant Heal Test Player");
        GameObject loaderObject = new GameObject("Instant Heal Test TeamLoader");
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        InstantHealRuntimeEffectDefinition heal = ScriptableObject.CreateInstance<InstantHealRuntimeEffectDefinition>();

        try
        {
            character.characterId = "instant-heal-test-character";
            character.stats.maxHP = 100f;
            character.stats.attack = 50f;

            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            EnsureEditModeAwake(player, PlayerCharacter.Instance);
            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = player;
            EnsureEditModeAwake(teamLoader, TeamLoader.Instance);
            teamLoader.SetRunInventory(inventory);
            teamLoader.DeployLoadout(new[] { character }, 0);
            player.SetCharacter(character, 25f, character.stats);

            heal.durationType = RuntimeEffectDurationType.Instant;
            heal.valueSource = RuntimeEffectStatTarget.Attack;
            heal.valueRatio = 0.5f;
            heal.flatValue = 20f;
            heal.useSourceStats = true;

            inventory.ApplyRuntimeEffect(character, heal, character, character.stats);

            Assert.That(player.CurrentHP, Is.EqualTo(70f).Within(0.001f));
            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(heal);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void RuntimeEffectRecipeExplicitStackKeyIgnoresParameterSignature()
    {
        DamageModifierRuntimeEffectDefinition effect = ScriptableObject.CreateInstance<DamageModifierRuntimeEffectDefinition>();

        try
        {
            RuntimeEffectRecipeData lowValueRecipe = new RuntimeEffectRecipeData
            {
                definition = effect,
                policy = new RuntimeEffectApplicationPolicy
                {
                    stackKey = "shared.echo.debuff"
                },
                parameters = new RuntimeEffectParameterSet
                {
                    overrideDamageModifier = true,
                    damageModifier = new RuntimeEffectDamageModifierParameters
                    {
                        incomingDamageTakenPercentAdd = 30f
                    }
                }
            };
            RuntimeEffectRecipeData highValueRecipe = new RuntimeEffectRecipeData
            {
                definition = effect,
                policy = new RuntimeEffectApplicationPolicy
                {
                    stackKey = "shared.echo.debuff"
                },
                parameters = new RuntimeEffectParameterSet
                {
                    overrideDamageModifier = true,
                    damageModifier = new RuntimeEffectDamageModifierParameters
                    {
                        incomingDamageTakenPercentAdd = 50f
                    }
                }
            };

            Assert.That(
                RuntimeEffectRuntimeUtility.GetStackKey(effect, lowValueRecipe),
                Is.EqualTo(RuntimeEffectRuntimeUtility.GetStackKey(effect, highValueRecipe)));
        }
        finally
        {
            Object.DestroyImmediate(effect);
        }
    }

    private static RuntimeEffectRecipe CreateShieldRecipe(
        ShieldRuntimeEffectDefinition shield,
        string recipeId,
        float valueRatio,
        float durationSeconds = 30f)
    {
        return new RuntimeEffectRecipe
        {
            definition = shield,
            recipeId = recipeId,
            policy = new RuntimeEffectApplicationPolicy
            {
                kind = RuntimeEffectKind.Buff,
                appliesOn = RuntimeEffectApplyTarget.Allies,
                durationType = RuntimeEffectDurationType.Seconds,
                durationSeconds = durationSeconds,
                stackMode = RuntimeEffectStackMode.Override,
                refreshDurationOnReapply = true
            },
            parameters = new RuntimeEffectParameterSet
            {
                overrideShield = true,
                shield = new RuntimeEffectShieldParameters
                {
                    valueSource = RuntimeEffectStatTarget.MaxHP,
                    valueRatio = valueRatio,
                    useSourceStats = true
                }
            }
        };
    }

    [Test]
    public void PlayerTakeDamageReturnsFalseWhenRunInventoryShieldFullyAbsorbs()
    {
        GameObject playerObject = new GameObject("Shield Test Player");
        GameObject loaderObject = new GameObject("Shield Test TeamLoader");
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        ShieldRuntimeEffectDefinition shield = ScriptableObject.CreateInstance<ShieldRuntimeEffectDefinition>();

        try
        {
            character.characterId = "shield-test-character";
            character.stats.maxHP = 100f;
            character.stats.defense = 0f;
            character.stats.damageReduction = 0f;

            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            EnsureEditModeAwake(player, PlayerCharacter.Instance);
            player.RuntimeEffects = playerObject.AddComponent<RuntimeEffectReceiver>();
            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = player;
            EnsureEditModeAwake(teamLoader, TeamLoader.Instance);
            teamLoader.SetRunInventory(inventory);
            teamLoader.DeployLoadout(new[] { character }, 0);
            player.SetCharacter(character, 100f, character.stats);

            shield.durationType = RuntimeEffectDurationType.Seconds;
            shield.durationSeconds = 30f;
            shield.flatValue = 60f;
            shield.valueRatio = 0f;
            shield.useSourceStats = true;

            inventory.ApplyRuntimeEffect(character, shield, character, character.stats);

            float shieldAbsorbed = 0f;
            bool missed = false;
            player.OnShieldAbsorbed += amount => shieldAbsorbed += amount;
            player.OnDamageMissed += () => missed = true;

            bool tookDamage = player.TakeDamage(50f);

            Assert.That(tookDamage, Is.False);
            Assert.That(player.CurrentHP, Is.EqualTo(100f).Within(0.001f));
            Assert.That(shieldAbsorbed, Is.EqualTo(50f).Within(0.001f));
            Assert.That(missed, Is.False);
            Assert.That(inventory.GetActiveRuntimeEffects(character)[0].remainingShieldAmount, Is.EqualTo(10f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(shield);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    private static void EnsureEditModeAwake(
        MonoBehaviour component,
        MonoBehaviour currentInstance)
    {
        if (component == null || ReferenceEquals(component, currentInstance))
        {
            return;
        }

        MethodInfo awake = component.GetType().GetMethod(
            "Awake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(awake, Is.Not.Null);
        awake.Invoke(component, null);
    }
}
