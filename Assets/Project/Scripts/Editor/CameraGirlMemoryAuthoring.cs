using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit editor authoring only. Gameplay reads the saved kit, action and projectile assets.
public static class CameraGirlMemoryAuthoring
{
    private const string Folder = "Assets/Project/Character/Hero/test_1_CameraGirl/";
    private const string Formulas = "Assets/Project/Character/Hero/RuntimeEffect/";
    private const string Capture = "cg.skill", Shadow = "cg.skill.ex", Ultimate = "cg.ult";
    private const string Mastery = "Skill.super.ex", Mark = "cg.captured", Window = "cg.skill.followup_window";
    private static string passiveEntryId;

    [MenuItem("Tools/Project/Combat/Apply Camera Girl Memory Revision")]
    public static void Apply()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(Folder + "CharacterObject_cg.asset");
        var kit = character.kitDefinition;
        var table = character.actionResolverTable;
        passiveEntryId = kit.presentation.entries.FirstOrDefault(entry => entry.passive)?.entryId ?? "cg.passive";
        var lensVisual = CreateLensVisuals();
        Undo.RecordObjects(new Object[] { character, table }, "Camera Girl Memory revision");
        kit.useMemoryRanks = true;
        PotentialNodeDefinition Node(PotentialTrack track, int level)
        {
            var node = kit.potentialNodes.FirstOrDefault(n => n.track == track && n.level == level);
            if (node == null)
            {
                node = new PotentialNodeDefinition { track = track, level = level, fragmentCost = 1,
                    nodeId = "cg." + (track == PotentialTrack.Skill ? "skill" : track == PotentialTrack.Ultimate ? "ultimate" : "passive") + ".lv" + level };
                kit.potentialNodes.Add(node);
            }
            return node;
        }
        for (int level = 1; level <= 6; level++)
            foreach (PotentialTrack track in new[] { PotentialTrack.Skill, PotentialTrack.Ultimate, PotentialTrack.Passive }) Node(track, level);

        RuntimeEffectRecipe Crit(string id, RuntimeEffectStatTarget stat, float value, RuntimeEffectApplyTarget target) =>
            Stat(id, 10f, target, new RuntimeEffectStatModifier { target = stat, value = value });
        var baseCR = Crit("cg.capture.cr", RuntimeEffectStatTarget.CritRate, 17f, RuntimeEffectApplyTarget.Teammates);
        var baseCD = Crit("cg.capture.cd", RuntimeEffectStatTarget.CritDamage, 33f, RuntimeEffectApplyTarget.Teammates);
        kit.baseSkillNode.runtimeEffects = new List<PassiveRuntimeEffectDefinition>
        {
            new() { trigger = PassiveTriggerType.OnActionStarted, filter = Action(Capture),
                enableRandomBuffSelection = true, minRandomBuffCount = 1, maxRandomBuffCount = 1,
                randomBuffPool = new List<RuntimeEffectRecipeData> { baseCR, baseCD } }
        };
        Describe(kit.baseSkillNode, "Capture Da Soul randomly grants teammates except Camera Girl +17% Critical Rate or +33% Critical Damage for 10 seconds.");
        kit.skill.description = kit.baseSkillNode.description;
        for (int level = 1; level <= 3; level++)
        {
            var target = level == 3 ? RuntimeEffectApplyTarget.Allies : RuntimeEffectApplyTarget.Teammates;
            var node = Node(PotentialTrack.Skill, level);
            node.runtimeEffects = new List<PassiveRuntimeEffectDefinition>
            {
                Trigger(Crit("cg.capture.cr", RuntimeEffectStatTarget.CritRate, level == 1 ? 17f : 31f, target), PassiveTriggerType.OnActionStarted, Action(Capture)),
                Trigger(Crit("cg.capture.cd", RuntimeEffectStatTarget.CritDamage, level == 1 ? 33f : 64f, target), PassiveTriggerType.OnActionStarted, Action(Capture))
            };
            if (level == 3)
                node.runtimeEffects.Add(Trigger(Stat("cg.capture.defense", 15f, RuntimeEffectApplyTarget.Self,
                    new RuntimeEffectStatModifier { target = RuntimeEffectStatTarget.Defense, operation = RuntimeEffectStatOperation.Multiply, value = 1.4f }),
                    PassiveTriggerType.OnActionStarted, Action(Capture)));
            Describe(node, level == 1 ? "Capture Da Soul grants teammates both +17% Critical Rate and +33% Critical Damage for 10s."
                : level == 2 ? "Capture Da Soul grants teammates +31% Critical Rate and +64% Critical Damage for 10s."
                : "Capture Da Soul grants its Critical buffs to all allies, including Camera Girl, and grants Camera Girl +40% DEF for 15s.");
        }
        var stun = Node(PotentialTrack.Skill, 4);
        stun.onHitStatusEffects = new List<PassiveOnHitStatusEffectDefinition>
        { new() { filter = Action(Capture, false), chance = .4f, status = StatusEffectType.Stun, durationSeconds = 2.5f } };
        Describe(stun, "Capture Da Soul hits have a 40% chance to Stun for 2.5 seconds.");

        var focusNode = Node(PotentialTrack.Skill, 5);
        var strike = CopyProjectile("ProjectileData_cg_ult", "ProjectileData_cg_lens_focus");
        var hit = (strike.ResolveSpawningHitbox() ?? strike.ResolveRoutineHitbox()) as NormalHitbox;
        if (hit == null) throw new System.InvalidOperationException("Len Focus requires an authored Normal hitbox.");
        strike.spawnPositionMode = SpawnPositionMode.Target;
        strike.spawnReferencePointMode = ProjectileReferencePointMode.Center;
        strike.applyOffsetToTarget = false;
        strike.speed = 0f; strike.traversalMode = ProjectileTraversalMode.Linear;
        strike.playSpawnAnimation = true;
        strike.spawnDuration = strike.spawnClip.length;
        strike.lifetime = .1f;
        strike.spawningHitbox = new ProjectilePhaseHitboxData { enabled = true, decrementsMaxHits = true, hitbox = hit };
        strike.routineHitbox = new ProjectilePhaseHitboxData { enabled = false };
        strike.destroyWhenHitsExhausted = false;
        strike.destroyOnLifetimeExpire = true;
        strike.magnetismSpeed = 0f;
        strike.travelColliderSize = new Vector2(1.4f, 1.4f); strike.travelColliderOffset = Vector2.zero;
        strike.maxHits = 1; strike.hitCountMode = ProjectileHitCountMode.Shared;
        strike.limitHitsToTarget = true; strike.fixedHitCadence = false;
        strike.auraEnergyPerDamageBatch = 0f; strike.onHitEffects.Clear();
        hit.offset = Vector2.zero; hit.size = new Vector2(1.4f, 1.4f);
        hit.elementType = ElementType.None;
        hit.damageScaling = new List<HitDamageStatScaling> { new() { stat = CombatStatId.Defense, ratio = 1f } };
        Save(strike);
        var focus = Recipe<PeriodicProjectileRuntimeEffectDefinition>("RuntimeEffect_PeriodicProjectile", "cg.lens_focus", 10f);
        focus.parameters.overrideProjectile = true;
        focus.parameters.projectile = new RuntimeEffectProjectileParameters { projectile = strike, tickInterval = 1f,
            roundRobinTargets = true, category = AttackCategory.Skill, actionId = "cg.lens_focus", actionName = "Len Focus" };
        focus.presentation.fieldVisualPrefab = lensVisual;
        focus.presentation.followActiveCharacter = true;
        focus.presentation.displayName = "Len Focus";
        focus.presentation.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Resources/FieldEffects/LensFocusIcon.png");
        var aura = Stat("cg.lens_focus.aura", 10f, RuntimeEffectApplyTarget.Allies,
            new RuntimeEffectStatModifier { target = RuntimeEffectStatTarget.Speed, value = 1f });
        aura.parameters.overrideMovement = true; aura.parameters.dashCooldownMultiplier = .5f;
        aura.policy.requiredSourceEffectKey = focus.recipeId;
        focusNode.runtimeEffects = new List<PassiveRuntimeEffectDefinition>
        { Trigger(focus, PassiveTriggerType.OnActionStarted, Action(Capture)), Trigger(aura, PassiveTriggerType.OnActionStarted, Action(Capture)) };
        Describe(focusNode, "Capture Da Soul summons Len Focus for 10s, refreshing without stacking. Allies gain +1 SPD and half Dash cooldown. Each second it attacks the next enemy in the scene for 100% of Camera Girl's DEF.");

        var splash = AssetDatabase.LoadAssetAtPath<ExplosionSplashDefinition>(Folder + "ExplosionSplash_cg_captured.asset");
        if (splash == null) { splash = ScriptableObject.CreateInstance<ExplosionSplashDefinition>(); AssetDatabase.CreateAsset(splash, Folder + "ExplosionSplash_cg_captured.asset"); }
        splash.stableId = "cg.captured.explosion"; splash.radius = 3f; splash.excludeOriginalTarget = false; Save(splash);
        splash.visualPrefab = EnsureCapturedExplosionVisual();
        Save(splash);
        var skillDamage = Damage("cg.skill.def_additional", 3.5f);
        var explosion = Damage("cg.skill.captured_explosion", 10f);
        explosion.parameters.additionalDamage.filter.requiredTargetEffectKey = Mark;
        explosion.parameters.additionalDamage.splash = splash;
        var skillReaction = Reaction("cg.skill.damage_reaction", new RuntimeEffectDamageFilter
        { filterByDelivery = true, delivery = DamageDelivery.Primary, actionIds = new List<string> { Capture, Shadow } },
            new RuntimeAreaEffectApplication { recipe = explosion, triggerOnHit = true },
            new RuntimeAreaEffectApplication { recipe = skillDamage, triggerOnHit = true });
        Node(PotentialTrack.Skill, 6).runtimeEffects = new List<PassiveRuntimeEffectDefinition> { Always(skillReaction) };
        Describe(Node(PotentialTrack.Skill, 6), "Skill and Skill EX hits deal Additional DMG equal to 350% of Camera Girl's DEF. Captured targets also explode for 1000% DEF Additional splash damage.");

        var initialCoord = Clone(Node(PotentialTrack.Ultimate, 1).coordinatedActionEffects.First());
        for (int level = 1; level <= 6; level++)
        {
            var node = Node(PotentialTrack.Ultimate, level);
            node.runtimeTagEffects.Clear(); node.coordinatedActionEffects.Clear(); node.actionModifiers.Clear(); node.runtimeEffects.Clear();
            if (level == 1 || level == 3)
                node.runtimeTagEffects.Add(new PassiveRuntimeTagEffectDefinition { trigger = PassiveTriggerType.OnActionStarted,
                    filter = Action(Ultimate), tag = "cg.ult.shadow_assist", duration = level == 1 ? 15f : 30f });
            if (level == 1 || level == 2 || level == 3 || level == 5)
            {
                var coord = Clone(initialCoord); coord.triggerChance = level == 5 ? 1f : .5f;
                coord.damageMultiplierOverride = level == 5 ? 6.5f : 0f;
                coord.overrideDamageSource = level == 5; coord.damageSourceStat = ActionDamageSourceStat.Defense;
                coord.hitboxScaleMultiplier = 1f; coord.auraEnergyRewardOnHit = level >= 2 ? 3f : 0f;
                coord.canTriggerOffField = coord.canTriggerOnField = true; coord.maxSimultaneousActive = 1;
                node.coordinatedActionEffects.Add(coord);
            }
            if (level == 4)
                node.runtimeEffects.Add(Trigger(Stat("cg.ultimate.ally_attack", 20f, RuntimeEffectApplyTarget.Allies,
                    new RuntimeEffectStatModifier { target = RuntimeEffectStatTarget.Attack,
                        sourceStat = RuntimeEffectStatTarget.Defense, operation = RuntimeEffectStatOperation.AddSourceStatRatio,
                        value = 5f, maxIncrease = 900f }), PassiveTriggerType.OnActionStarted, Action(Ultimate)));
            if (level == 6)
                node.runtimeEffects.Add(Trigger(Stat("cg.ultimate.adi", 20f, RuntimeEffectApplyTarget.Self,
                    new RuntimeEffectStatModifier { target = RuntimeEffectStatTarget.AdditionalDamageIncrease, value = 100f }),
                    PassiveTriggerType.OnActionStarted, Action(Ultimate)));
            string[] descriptions = { "", "Send Da Btch grants Shadow Assist for 15s: ally attacks have a 50% chance to summon one Shadow Attack, including while Camera Girl is off field.",
                "Each Shadow Attack grants Camera Girl 3 AE on its first successful hit, maximum 3 AE per attack.",
                "Shadow Assist now lasts 30 seconds.", "Send Da Btch grants all allies ATK equal to 500% of Camera Girl's DEF, capped at +900 ATK, for 20s.",
                "Shadow Assist triggers at 100% chance and Shadow Attack deals 650% of Camera Girl's DEF.", "Send Da Btch grants Camera Girl +100% ADI for 20s." };
            Describe(node, descriptions[level]);
        }

        for (int level = 1; level <= 2; level++)
        {
            var node = Node(PotentialTrack.Passive, level);
            var captured = Captured(level);
            node.runtimeEffects = new List<PassiveRuntimeEffectDefinition>
            { Trigger(captured, PassiveTriggerType.OnHitDealt, Action(Capture, false)),
                Trigger(captured, PassiveTriggerType.OnHitDealt, new PassiveTriggerFilter { filterByCategory = true,
                    category = AttackCategory.Normal, filterByStepName = true, stepName = "Attack3" }) };
            Describe(node, level == 1 ? "Attack3 or Capture Da Soul marks hit enemies as Captured for 10s, reducing their DEF by 40%."
                : "Captured reduces DEF and RES by 40% and increases incoming DOT and Additional damage by 40% for 10s.");
        }
        var mastery = table.skillActions.FirstOrDefault(a => a.actionId == Mastery);
        if (mastery == null) { mastery = Clone(table.skillActions.Single(a => a.actionId == Capture)); table.skillActions.Add(mastery); }
        mastery.actionId = Mastery; mastery.actionName = "Mastery EndField"; mastery.priority = 100;
        mastery.cooldown = 0f; mastery.auraEnergyReward = 0f;
        mastery.description = "Execute the authored Attack6. Available for 10s after Capture Da Soul or Attack3 hits a Captured enemy.";
        mastery.conditions = new List<ActionConditionBase> { new PotentialLevelActionCondition { track = PotentialTrack.Passive, level = 3 },
            new RuntimeTagActionCondition { tag = Window } };
        var root = mastery.comboTree.rootNode;
        var attack6 = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "animation/cg_attack6.anim");
        foreach (var action in table.comboActions)
        {
            var step = FindStep(action.comboTree?.rootNode, attack6, new HashSet<ComboNode>());
            if (step == null) continue;
            root.stepData = Clone(step);
            mastery.damageSourceStat = action.damageSourceStat;
            mastery.actionDamageMultiplier = action.actionDamageMultiplier;
            break;
        }
        root.defaultNext = null; root.routes.Clear(); root.onGrabSuccessNode = null;
        root.stepData.animationClip = attack6;
        root.stepData.stepName = "Attack6";
        if (!kit.presentation.entries.Any(entry => entry.entryId == Mastery))
            kit.presentation.entries.Add(new CharacterKitPresentationDefinition.Entry { entryId = Mastery, actionId = Mastery,
                sectionId = "skill", minimumPotentialLevel = 3, unlockTrack = PotentialTrack.Passive, order = 3,
                activationDescription = "Hit a Captured enemy with Capture Da Soul or Attack3, then press Skill within 10 seconds." });
        var passive3 = Node(PotentialTrack.Passive, 3);
        passive3.actionModifiers = new List<PassiveActionModifierDefinition>
        { new() { category = AttackCategory.Skill, actionId = Mastery, requiredTags = new List<RuntimeTagRequirement> { new() { tag = Window } } } };
        passive3.runtimeTagEffects = new List<PassiveRuntimeTagEffectDefinition>
        {
            new() { trigger = PassiveTriggerType.OnHitDealt, filter = new PassiveTriggerFilter { filterByActionId = true, actionId = Capture,
                requiredEnemyEffectKey = Mark }, tag = Window, duration = 10f },
            new() { trigger = PassiveTriggerType.OnHitDealt, filter = new PassiveTriggerFilter { filterByCategory = true,
                category = AttackCategory.Normal, filterByStepName = true, stepName = "Attack3", requiredEnemyEffectKey = Mark }, tag = Window, duration = 10f }
        };
        Describe(passive3, "Capture Da Soul or Attack3 hitting an already Captured enemy opens a 10s Skill follow-up for Mastery EndField (Attack6).");
        var passive4 = Node(PotentialTrack.Passive, 4);
        passive4.actionModifiers.Clear(); passive4.runtimeTagEffects.Clear();
        var delivery = Recipe<DamageInputRuntimeEffectDefinition>("RuntimeEffect_DamageInput", "cg.mastery.additional_delivery", -1f);
        delivery.parameters.overrideDamageInput = true;
        delivery.parameters.damageInput.filter.actionId = Mastery;
        delivery.parameters.damageInput.overrideDelivery = true; delivery.parameters.damageInput.delivery = DamageDelivery.Additional;
        passive4.runtimeEffects = new List<PassiveRuntimeEffectDefinition> { Always(delivery) };
        passive4.resourceEffects = new List<PassiveResourceEffectDefinition> { new() { trigger = PassiveTriggerType.OnActionEnded,
            filter = Action(Mastery, false), effectType = PassiveResourceEffectType.RefillUltimateAuraEnergy, flatValue = 5f } };
        Describe(passive4, "Mastery EndField generates 5 AE and its damage uses Additional delivery.");
        var proc70 = Damage("cg.captured.def_proc", .7f);
        var procReaction = Reaction("cg.captured.hit_proc", MarkedFilter(), new RuntimeAreaEffectApplication { recipe = proc70, triggerOnHit = true });
        procReaction.parameters.reaction.internalCooldown = 1.5f; procReaction.parameters.reaction.skillCooldownReduction = .5f;
        Node(PotentialTrack.Passive, 5).runtimeEffects = new List<PassiveRuntimeEffectDefinition> { Always(procReaction) };
        Describe(Node(PotentialTrack.Passive, 5), "Camera Girl hitting a Captured enemy reduces her Skill cooldowns by 0.5s and deals Additional DMG equal to 70% of her DEF; 1.5s internal cooldown.");
        var stacks = Stat("cg.captured.stacks", 3f, RuntimeEffectApplyTarget.Self,
            new RuntimeEffectStatModifier { target = RuntimeEffectStatTarget.Attack, operation = RuntimeEffectStatOperation.Multiply, value = 1.03f },
            new RuntimeEffectStatModifier { target = RuntimeEffectStatTarget.Defense, operation = RuntimeEffectStatOperation.Multiply, value = 1.03f });
        stacks.policy.stackMode = RuntimeEffectStackMode.Stack; stacks.policy.stackable = true; stacks.policy.stackLimit = 10;
        Node(PotentialTrack.Passive, 6).runtimeEffects = new List<PassiveRuntimeEffectDefinition>
        { Always(Reaction("cg.captured.stack_reaction", MarkedFilter(), new RuntimeAreaEffectApplication { recipe = stacks })) };
        Describe(Node(PotentialTrack.Passive, 6), "Camera Girl hitting a Captured enemy gains +3% ATK and DEF for 3s, stacking up to +30%; hits refresh the stack duration.");
        Save(character); Save(table);
        Debug.Log("Camera Girl Memory revision authored: saved recipes, six ranks per track, Len Focus and Mastery EndField.");
    }

    private static RuntimeEffectDamageFilter MarkedFilter() => new() { requiredTargetEffectKey = Mark };
    private static PassiveTriggerFilter Action(string id, bool initial = true) => new() { filterByActionId = true, actionId = id, requireInitialActionStep = initial };
    private static PassiveRuntimeEffectDefinition Trigger(RuntimeEffectRecipe recipe, PassiveTriggerType trigger, PassiveTriggerFilter filter) => new() { recipe = recipe, trigger = trigger, filter = filter };
    private static PassiveRuntimeEffectDefinition Always(RuntimeEffectRecipe recipe) => Trigger(recipe, PassiveTriggerType.Always, new PassiveTriggerFilter());
    private static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    private static RuntimeEffectRecipe Recipe<T>(string file, string id, float duration) where T : RuntimeEffectDefinition
    {
        var formula = AssetDatabase.LoadAssetAtPath<T>(Formulas + file + ".asset");
        if (formula == null) { formula = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(formula, Formulas + file + ".asset"); }
        return new RuntimeEffectRecipe { recipeId = id, definition = formula, policy = new RuntimeEffectApplicationPolicy
            { durationType = duration < 0f ? RuntimeEffectDurationType.Forever : RuntimeEffectDurationType.Seconds,
                durationSeconds = Mathf.Max(0f, duration), stackMode = RuntimeEffectStackMode.Override, stackKey = id } };
    }
    private static RuntimeEffectRecipe Stat(string id, float duration, RuntimeEffectApplyTarget target, params RuntimeEffectStatModifier[] modifiers)
    {
        var recipe = Recipe<StatRuntimeEffectDefinition>("RuntimeEffect_Stat", id, duration);
        recipe.policy.appliesOn = target; recipe.parameters.overrideStatModifiers = true;
        recipe.parameters.statModifiers = modifiers.ToList();
        recipe.presentation.fieldVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Resources/FieldEffects/FieldPowerUp.prefab");
        return recipe;
    }
    private static RuntimeEffectRecipe Damage(string id, float defenseRatio)
    {
        var recipe = Recipe<AdditionalDamageOnHitRuntimeEffectDefinition>("RuntimeEffect_AdditionalDamage", id, 0f);
        recipe.parameters.overrideAdditionalDamage = true;
        recipe.parameters.additionalDamage.attackRatio = 0f;
        recipe.parameters.additionalDamage.sourceStatDamageSource = RuntimeEffectStatTarget.Defense;
        recipe.parameters.additionalDamage.sourceStatRatio = defenseRatio;
        return recipe;
    }
    private static RuntimeEffectRecipe Reaction(string id, RuntimeEffectDamageFilter filter, params RuntimeAreaEffectApplication[] effects)
    {
        var recipe = Recipe<OnHitRecipeRuntimeEffectDefinition>("RuntimeEffect_OnHitRecipe", id, -1f);
        recipe.parameters.overrideReaction = true; recipe.parameters.reaction = new RuntimeEffectReactionParameters { filter = filter };
        recipe.parameters.reaction.effects = effects.ToList(); return recipe;
    }
    private static RuntimeEffectRecipe Captured(int level)
    {
        var recipe = Recipe<RuntimeEffectContainerDefinition>("RuntimeEffect_Container", Mark, 10f);
        recipe.policy.kind = RuntimeEffectKind.Debuff; recipe.policy.appliesOn = RuntimeEffectApplyTarget.Enemy;
        recipe.presentation.displayName = "Captured";
        recipe.presentation.fieldVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Resources/FieldEffects/FieldCaptured.prefab");
        foreach (DamageDelivery delivery in new[] { DamageDelivery.Primary, DamageDelivery.Dot, DamageDelivery.Additional })
        {
            var defense = Recipe<DamageInputRuntimeEffectDefinition>("RuntimeEffect_DamageInput", Mark + ".defense." + delivery, 10f);
            defense.parameters.overrideDamageInput = true; defense.parameters.damageInput.defenderDefensePercentAdd = -40f;
            defense.parameters.damageInput.filter = new RuntimeEffectDamageFilter { filterByDelivery = true, delivery = delivery };
            if (level == 2) defense.parameters.damageInput.defenderDamageResistancePercentAdd = -40f;
            recipe.childRecipes.Add(defense);
        }
        if (level == 2)
            foreach (DamageDelivery delivery in new[] { DamageDelivery.Dot, DamageDelivery.Additional })
            {
                var taken = Recipe<DamageModifierRuntimeEffectDefinition>("RuntimeEffect_DamageModifier", Mark + "." + delivery, 10f);
                taken.parameters.overrideDamageModifier = true;
                taken.parameters.damageModifier.filter = new RuntimeEffectDamageFilter { filterByDelivery = true, delivery = delivery };
                taken.parameters.damageModifier.incomingDamageTakenPercentAdd = 40f;
                recipe.childRecipes.Add(taken);
            }
        return recipe;
    }
    private static ProjectileData CopyProjectile(string source, string name)
    {
        var data = AssetDatabase.LoadAssetAtPath<ProjectileData>(Folder + name + ".asset");
        if (data != null) return data;
        data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ProjectileData>(Folder + source + ".asset"));
        data.name = name; AssetDatabase.CreateAsset(data, Folder + name + ".asset"); return data;
    }
    private static AttackStepData FindStep(ComboNode node, AnimationClip clip, HashSet<ComboNode> visited)
    {
        if (node == null || !visited.Add(node)) return null;
        if (node.stepData?.animationClip == clip) return node.stepData;
        var found = FindStep(node.defaultNext, clip, visited) ?? FindStep(node.onGrabSuccessNode, clip, visited);
        if (found != null) return found;
        foreach (var route in node.routes)
        {
            found = FindStep(route.targetNode, clip, visited);
            if (found != null) return found;
        }
        return null;
    }
    private static void Describe(PotentialNodeDefinition node, string description)
    {
        node.description = description;
        if (string.IsNullOrWhiteSpace(node.nodeName)) node.nodeName = node.track + " Lv." + node.level;
        string entry = node.track == PotentialTrack.Ultimate ? Ultimate : node.track == PotentialTrack.Skill ? Capture : passiveEntryId;
        node.kitInfoPatches.Clear(); node.kitInfoPatches.Add(new CharacterKitPresentationDefinition.Patch
        { entryId = entry, noteId = node.nodeId, description = description, appendNote = true, priority = node.level });
    }
    private static void Save(Object asset) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }

    [MenuItem("Tools/Project/Combat/Assign Rei Captured Explosion Visual")]
    public static void AssignCapturedExplosionVisual()
    {
        var splash = AssetDatabase.LoadAssetAtPath<ExplosionSplashDefinition>(Folder + "ExplosionSplash_cg_captured.asset");
        var visual = EnsureCapturedExplosionVisual();
        Undo.RecordObject(splash, "Assign Rei Captured explosion visual");
        splash.visualPrefab = visual;
        Save(splash);
    }

    private static GameObject EnsureCapturedExplosionVisual()
    {
        const string path = Folder + "CapturedExplosionRei.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Resources/FieldEffects/ChronoBloodExplosion.prefab");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            var view = instance.GetComponent<ArtifactAreaVisual>();
            view.tint = new Color(1f, 0.83f, 0.12f, 0.95f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            return PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static GameObject CreateLensVisuals()
    {
        const string rootPath = "Assets/Project/Resources/FieldEffects/";
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + .5f - size / 2f, dy = y + .5f - size / 2f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                if (radius > 13f) continue;
                Color color = new Color32(24, 18, 43, 255);
                if (radius < 11.5f && radius > 9f) color = dy > 0f ? new Color32(194, 201, 242, 255) : new Color32(97, 91, 152, 255);
                if (radius < 7.8f) color = Color.Lerp(new Color32(57, 31, 100, 255), new Color32(105, 181, 224, 255), Mathf.Clamp01((dx + dy + 10f) / 20f));
                if (radius < 3.5f) color = new Color32(20, 15, 38, 255);
                if ((dx + 4f) * (dx + 4f) + (dy - 4f) * (dy - 4f) < 3f) color = new Color32(224, 244, 255, 255);
                pixels[y * size + x] = color;
            }
        texture.SetPixels(pixels); texture.Apply();
        string path = rootPath + "LensFocusIcon.png";
        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = size;
        importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        GameObject result = null;
        foreach (bool captured in new[] { false, true })
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var host = new GameObject(captured ? "FieldCaptured" : "FieldLensFocus");
            SceneManager.MoveGameObjectToScene(host, scene);
            try
            {
                var visual = host.AddComponent<FieldEffectVisual>(); visual.tint = Color.white;
                visual.style = captured ? FieldEffectVisual.VisualStyle.Mark : FieldEffectVisual.VisualStyle.FloatingIcon;
                var motif = new GameObject("Lens"); motif.transform.SetParent(host.transform, false);
                visual.motif = motif.AddComponent<SpriteRenderer>(); visual.motif.sprite = sprite;
                visual.motif.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(rootPath + "Chevron.mat");
                var prefab = PrefabUtility.SaveAsPrefabAsset(host, rootPath + host.name + ".prefab");
                if (!captured) result = prefab;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        return result;
    }
}
