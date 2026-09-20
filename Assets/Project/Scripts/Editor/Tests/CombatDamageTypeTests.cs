#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CombatDamageTypeTests
{
    [Test]
    public void StatCatalogExposesTenBuildableStatsAndHidesSpeed()
    {
        Assert.That(CombatStatCatalog.BuildableIds.Count, Is.EqualTo(10));
        CollectionAssert.DoesNotContain(
            (System.Collections.ICollection)CombatStatCatalog.BuildableIds,
            CombatStatId.Speed);
        Assert.That(CombatStatCatalog.Get(CombatStatId.Speed).IsBuildable, Is.False);
        Assert.That(CombatStatCatalog.Get(CombatStatId.AdditionalDamageIncrease).Label,
            Is.EqualTo("Additional Damage Increase (ADI)"));
    }

    [TestCase(DamageDelivery.Primary, ElementType.None, 120f)]
    [TestCase(DamageDelivery.Primary, ElementType.Lightning, 180f)]
    [TestCase(DamageDelivery.Dot, ElementType.Lightning, 150f)]
    [TestCase(DamageDelivery.Additional, ElementType.None, 130f)]
    [TestCase(DamageDelivery.Additional, ElementType.Lightning, 195f)]
    public void BuildStatMatrixUsesOnlyEligibleMultipliers(
        DamageDelivery delivery,
        ElementType element,
        float expected)
    {
        CharacterStats stats = new CharacterStats
        {
            damageIncrease = 20f,
            dotMastery = 50f,
            additionalDamageIncrease = 30f
        };
        DamageRequest request = CreateRequest(100f, delivery, element, stats);
        request.descriptor.criticalPolicy = DamageCriticalPolicy.Prevent;

        DamageResult result = CombatDamageResolver.Resolve(request);

        Assert.That(result.Damage, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void DeliveryMitigationFollowsPrimaryDotAdditionalContract()
    {
        DamageRequest primary = CreateRequest(100f, DamageDelivery.Primary, ElementType.None);
        primary.defenderDefense = 500f;
        primary.defenderDamageResistance = 20f;
        DamageRequest dot = primary;
        dot.descriptor = new DamageDescriptor(DamageDelivery.Dot, ElementType.Lightning);
        DamageRequest additional = primary;
        additional.descriptor = new DamageDescriptor(DamageDelivery.Additional, ElementType.None);

        Assert.That(CombatDamageResolver.Resolve(primary).Damage, Is.EqualTo(13));
        Assert.That(CombatDamageResolver.Resolve(dot).Damage, Is.EqualTo(80f).Within(0.001f));
        Assert.That(CombatDamageResolver.Resolve(additional).Damage, Is.EqualTo(100f).Within(0.001f));
    }

    [Test]
    public void TypeReductionIsAppliedExactlyOnceByThePureResolver()
    {
        DamageRequest request = CreateRequest(100f, DamageDelivery.Additional, ElementType.Lightning);
        request.typeReductionPercent = 25f;

        Assert.That(CombatDamageResolver.Resolve(request).Damage, Is.EqualTo(75f).Within(0.001f));
    }

    [Test]
    public void ProcessorUsesEnemyTypeReductionExactlyOnce()
    {
        GameObject enemyObject = new GameObject("Processor Reduction Test Enemy");
        EnemyController enemy = EnemyLifecycleTestUtility.AddActive(enemyObject);
        EnemyDamageTypeState state = enemyObject.AddComponent<EnemyDamageTypeState>();
        EnemyElementalProfile profile = ScriptableObject.CreateInstance<EnemyElementalProfile>();
        profile.lightningDamageReduction = 25f;
        state.Initialize(profile);
        DamageRequest request = CreateRequest(
            100f,
            DamageDelivery.Additional,
            ElementType.Lightning);
        request.target = enemy;
        request.typeReductionPercent = 25f;
        HitReactionData reaction = HitReactionData.Default(0f, Vector2.zero, 0f);

        float applied = CombatDamageProcessor.Apply(request, reaction, false);

        Assert.That(applied, Is.EqualTo(75f).Within(0.001f));
        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(enemyObject);
    }

    [Test]
    public void OnlyPositiveNonPhysicalPrimaryOrAdditionalCanRequestDot()
    {
        DamageResult primary = CombatDamageResolver.Resolve(
            CreateRequest(10f, DamageDelivery.Primary, ElementType.Lightning));
        DamageResult additional = CombatDamageResolver.Resolve(
            CreateRequest(10f, DamageDelivery.Additional, ElementType.Lightning));
        DamageResult dot = CombatDamageResolver.Resolve(
            CreateRequest(10f, DamageDelivery.Dot, ElementType.Lightning));
        DamageResult physical = CombatDamageResolver.Resolve(
            CreateRequest(10f, DamageDelivery.Primary, ElementType.None));

        Assert.That(primary.CanApplyDot, Is.True);
        Assert.That(additional.CanApplyDot, Is.True);
        Assert.That(dot.CanApplyDot, Is.False);
        Assert.That(physical.CanApplyDot, Is.False);
    }

    [Test]
    public void SecondaryDamageDoesNotCritUnlessExplicitlyAllowed()
    {
        CharacterStats stats = new CharacterStats { critRate = 100f, critDamage = 50f };
        DamageRequest request = CreateRequest(100f, DamageDelivery.Additional, ElementType.None, stats);
        request.criticalRoll01 = 0f;

        DamageResult defaultResult = CombatDamageResolver.Resolve(request);
        request.descriptor.criticalPolicy = DamageCriticalPolicy.Allow;
        DamageResult optInResult = CombatDamageResolver.Resolve(request);

        Assert.That(defaultResult.IsCriticalHit, Is.False);
        Assert.That(defaultResult.Damage, Is.EqualTo(100f));
        Assert.That(optInResult.IsCriticalHit, Is.True);
        Assert.That(optInResult.Damage, Is.EqualTo(150f));
    }

    [Test]
    public void NegativeBuildBonusesCannotProduceNegativeDamage()
    {
        CharacterStats stats = new CharacterStats { damageIncrease = -200f };
        DamageRequest request = CreateRequest(100f, DamageDelivery.Primary, ElementType.None, stats);

        Assert.That(CombatDamageResolver.Resolve(request).Damage, Is.Zero);
    }

    [Test]
    public void FinalDamageIsRoundedOnceAfterDecimalStatCalculation()
    {
        CharacterStats stats = new CharacterStats { damageIncrease = 12.34f };
        DamageRequest request = CreateRequest(10.5f, DamageDelivery.Primary, ElementType.None, stats);

        Assert.That(CombatDamageResolver.Resolve(request).Damage, Is.EqualTo(12));
    }

    [Test]
    public void SameElementDotUsesStrongestSnapshotAndPreservesEqualCadence()
    {
        GameObject enemyObject = new GameObject("DOT Snapshot Test Enemy");
        EnemyController enemy = EnemyLifecycleTestUtility.AddActive(enemyObject);
        EnemyDamageTypeState state = enemyObject.AddComponent<EnemyDamageTypeState>();
        CharacterStats first = new CharacterStats { attack = 100f, dotMastery = 0f };
        CharacterStats equal = first.Clone();
        CharacterStats stronger = new CharacterStats { attack = 200f, dotMastery = 0f };
        CharacterStats weaker = new CharacterStats { attack = 50f, dotMastery = 0f };

        state.ApplyDot(ElementType.Lightning, first, null);
        object activeDot = GetOnlyActiveDot(state);
        SetField(activeDot, "tickTimer", 0.2f);
        state.ApplyDot(ElementType.Lightning, equal, null);
        Assert.That(GetField<float>(activeDot, "tickTimer"), Is.EqualTo(0.2f));

        state.ApplyDot(ElementType.Lightning, stronger, null);
        Assert.That(GetField<float>(activeDot, "tickTimer"), Is.Zero);
        float strongSnapshot = GetField<float>(activeDot, "snapshottedStrength");
        state.ApplyDot(ElementType.Lightning, weaker, null);
        Assert.That(GetField<float>(activeDot, "snapshottedStrength"), Is.EqualTo(strongSnapshot));

        Object.DestroyImmediate(enemyObject);
    }

    [Test]
    public void EnemyProfileMapsEveryDamageTypeToItsReduction()
    {
        EnemyElementalProfile profile = ScriptableObject.CreateInstance<EnemyElementalProfile>();
        profile.physicalDamageReduction = 10f;
        profile.lightningDamageReduction = 15f;
        profile.bleedDamageReduction = 20f;
        profile.darknessDamageReduction = 25f;
        profile.particleDamageReduction = 30f;

        Assert.That(profile.GetDamageReductionPercent(ElementType.None), Is.EqualTo(10f));
        Assert.That(profile.GetDamageReductionPercent(ElementType.Lightning), Is.EqualTo(15f));
        Assert.That(profile.GetDamageReductionPercent(ElementType.Bleed), Is.EqualTo(20f));
        Assert.That(profile.GetDamageReductionPercent(ElementType.Darkness), Is.EqualTo(25f));
        Assert.That(profile.GetDamageReductionPercent(ElementType.Particle), Is.EqualTo(30f));

        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ParticleDamageAppliesReductionAndMapsElementDefinition()
    {
        ElementDefinition particleDef = ElementDatabase.Load().GetDefinition(ElementType.Particle);
        Assert.That(particleDef.elementType, Is.EqualTo(ElementType.Particle));

        DamageRequest request = CreateRequest(100f, DamageDelivery.Primary, ElementType.Particle);
        request.typeReductionPercent = 20f;
        DamageResult result = CombatDamageResolver.Resolve(request);
        Assert.That(result.Damage, Is.EqualTo(80));

        NormalHitbox hitbox = new NormalHitbox();
        hitbox.elementType = ElementType.Particle;
        hitbox.damageTags = CombatDamageTag.Explosion;
        Assert.That(hitbox.elementType, Is.EqualTo(ElementType.Particle));
        Assert.That(hitbox.damageTags.HasFlag(CombatDamageTag.Explosion), Is.True);
    }

    [Test]
    public void AdditionalDamageTextScale_FollowsDamageRatioAndCriticalRules()
    {
        GameObject go = new GameObject("WorldTextUtilityTest");
        try
        {
            WorldTextUtility utility = go.AddComponent<WorldTextUtility>();
            CharacterStats stats = new CharacterStats { attack = 100f };

            // Low ratio (<= 100% of source ATK): normal base scale (0.85)
            WorldTextRequest lowRatio = new WorldTextRequest
            {
                type = WorldTextType.Additional,
                amount = 50f,
                sourceStats = stats,
                damageRatio = 0.5f,
                elementType = ElementType.None,
                isCritical = false
            };
            float lowScale = utility.ResolveHoldScaleForTest(lowRatio);
            Assert.That(lowScale, Is.EqualTo(0.85f).Within(0.001f));

            // High ratio (> 100% of source ATK, e.g. 250%): base scale * 1.35 = 1.1475
            WorldTextRequest highRatio = new WorldTextRequest
            {
                type = WorldTextType.Additional,
                amount = 250f,
                sourceStats = stats,
                damageRatio = 2.5f,
                elementType = ElementType.None,
                isCritical = false
            };
            float highScale = utility.ResolveHoldScaleForTest(highRatio);
            Assert.That(highScale, Is.EqualTo(0.85f * 1.35f).Within(0.001f));

            // High ratio + Crit allowed: base scale * 1.35 * 1.5 = 1.72125
            WorldTextRequest highRatioCrit = new WorldTextRequest
            {
                type = WorldTextType.Additional,
                amount = 250f,
                sourceStats = stats,
                damageRatio = 2.5f,
                elementType = ElementType.None,
                isCritical = true
            };
            float highCritScale = utility.ResolveHoldScaleForTest(highRatioCrit);
            Assert.That(highCritScale, Is.EqualTo(0.85f * 1.35f * 1.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void DotDamageTextScale_FollowsDamageRatioAndCriticalRules()
    {
        GameObject go = new GameObject("WorldTextUtilityTest_DOT");
        try
        {
            WorldTextUtility utility = go.AddComponent<WorldTextUtility>();
            CharacterStats stats = new CharacterStats { attack = 100f };

            // Low ratio DOT (<= 100% of source ATK, e.g. 30%): base scale (0.85)
            WorldTextRequest lowRatio = new WorldTextRequest
            {
                type = WorldTextType.Dot,
                amount = 30f,
                sourceStats = stats,
                damageRatio = 0.3f,
                elementType = ElementType.None,
                isCritical = false
            };
            float lowScale = utility.ResolveHoldScaleForTest(lowRatio);
            Assert.That(lowScale, Is.EqualTo(0.85f).Within(0.001f));

            // High ratio DOT (> 100% of source ATK, e.g. 150%): base scale * 1.35 = 1.1475
            WorldTextRequest highRatio = new WorldTextRequest
            {
                type = WorldTextType.Dot,
                amount = 150f,
                sourceStats = stats,
                damageRatio = 1.5f,
                elementType = ElementType.None,
                isCritical = false
            };
            float highScale = utility.ResolveHoldScaleForTest(highRatio);
            Assert.That(highScale, Is.EqualTo(0.85f * 1.35f).Within(0.001f));

            // High ratio DOT + Crit: base scale * 1.35 * 1.5 = 1.72125
            WorldTextRequest highRatioCrit = new WorldTextRequest
            {
                type = WorldTextType.Dot,
                amount = 150f,
                sourceStats = stats,
                damageRatio = 1.5f,
                elementType = ElementType.None,
                isCritical = true
            };
            float highCritScale = utility.ResolveHoldScaleForTest(highRatioCrit);
            Assert.That(highCritScale, Is.EqualTo(0.85f * 1.35f * 1.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static DamageRequest CreateRequest(
        float baseDamage,
        DamageDelivery delivery,
        ElementType element,
        CharacterStats stats = null)
    {
        return new DamageRequest
        {
            baseDamage = baseDamage,
            sourceStats = stats,
            descriptor = new DamageDescriptor(delivery, element),
            hasCriticalRoll = true,
            criticalRoll01 = 1f
        };
    }

    private static object GetOnlyActiveDot(EnemyDamageTypeState state)
    {
        FieldInfo field = typeof(EnemyDamageTypeState).GetField("_activeDots", BindingFlags.Instance | BindingFlags.NonPublic);
        IList dots = (IList)field.GetValue(state);
        Assert.That(dots.Count, Is.EqualTo(1));
        return dots[0];
    }

    private static T GetField<T>(object target, string fieldName)
    {
        return (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public).GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public).SetValue(target, value);
    }
}
#endif
