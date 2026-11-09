using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit, repeatable asset migration for the approved ADR-020 kit.</summary>
public static class GoenitzKitAuthoring
{
    public const string Folder = "Assets/Project/Character/Hero/test_15_goenitz/";
    private const string Shared = "Assets/Project/Character/Hero/RuntimeEffect/";

    [MenuItem("Tools/Project/Combat/Apply Goenitz Blood Frenzy Kit")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Author Goenitz outside Play Mode.");
        CharacterObject character = Load<CharacterObject>(Folder + "CharacterObject_Goenitz.asset");
        if (character.kitDefinition.useMemoryRanks)
            throw new InvalidOperationException("The legacy Blood Frenzy migration would overwrite Goenitz's authored Memory kit.");
        ActionResolverTable table = character.actionResolverTable;
        Undo.RecordObjects(new Object[] { character, table }, "Apply Goenitz Blood Frenzy Kit");
        var profile = LoadOrCreate<GoenitzKitRuntimeDefinition>(Folder + "GoenitzKitRuntime.asset");
        var stat = Load<StatRuntimeEffectDefinition>(Shared + "RuntimeEffect_Stat.asset");
        var additional = Load<AdditionalDamageOnHitRuntimeEffectDefinition>(Shared + "RuntimeEffect_AdditionalDamage.asset");
        var damage = Load<DamageModifierRuntimeEffectDefinition>(Shared + "RuntimeEffect_DamageModifier.asset");
        var defense = LoadOrCreate<DefensiveRuntimeEffectDefinition>(Folder + "RuntimeEffect_FrenzyEvasion.asset");
        Sprite frenzyIcon = Load<RuntimeEffectContainerDefinition>(Folder + "RuntimeEffect_Wind_Frenzy.asset").icon;
        Sprite blizzardIcon = Load<RuntimeEffectContainerDefinition>(Folder + "RuntimeEffect_Blizzard.asset").icon;

        profile.frenzy = Recipe(stat, "goenitz.blood_frenzy", "Blood Frenzy", -1f, frenzyIcon);
        foreach (var target in new[] { RuntimeEffectStatTarget.DamageIncrease, RuntimeEffectStatTarget.DotMastery, RuntimeEffectStatTarget.AdditionalDamageIncrease })
            AddStat(profile.frenzy, target, RuntimeEffectStatOperation.Add, 25f);
        profile.evasion = Recipe(defense, "goenitz.frenzy_evasion", "Frenzy Evasion", -1f, frenzyIcon);
        profile.evasion.parameters.overrideDefense = true;
        profile.evasion.parameters.evasionChanceAdd = .35f;
        profile.blizzard = Recipe(stat, "goenitz.blizzard", "Blizzard", 15f, blizzardIcon);
        foreach (var target in new[] { RuntimeEffectStatTarget.MaxHP, RuntimeEffectStatTarget.Attack, RuntimeEffectStatTarget.Defense, RuntimeEffectStatTarget.Speed, RuntimeEffectStatTarget.AuraEnergyRecharge })
            AddStat(profile.blizzard, target, RuntimeEffectStatOperation.Multiply, 1.1f);
        foreach (var target in new[] { RuntimeEffectStatTarget.CritRate, RuntimeEffectStatTarget.CritDamage, RuntimeEffectStatTarget.CooldownMastery, RuntimeEffectStatTarget.DotMastery, RuntimeEffectStatTarget.DamageIncrease, RuntimeEffectStatTarget.AdditionalDamageIncrease })
            AddStat(profile.blizzard, target, RuntimeEffectStatOperation.Add, 10f);
        profile.killAttack = Recipe(stat, "goenitz.frenzy_kill_attack", "Frenzy ATK", -1f, frenzyIcon);
        profile.killAttack.policy.stackMode = RuntimeEffectStackMode.Stack;
        profile.killAttack.policy.stackable = true;
        profile.killAttack.policy.stackLimit = 10;
        profile.killAttack.policy.removeOnOwnerDamaged = true;
        profile.killAttack.parameters.overrideStatModifiers = true;
        profile.killAttack.parameters.statModifiers.Add(new RuntimeEffectStatModifier
        {
            target = RuntimeEffectStatTarget.Attack, operation = RuntimeEffectStatOperation.AddTargetStatRatio,
            sourceStat = RuntimeEffectStatTarget.Attack, statRatioValueSource = RuntimeEffectStatValueSource.BaseValue, value = .03f
        });
        profile.vulnerability = Recipe(damage, "goenitz.frenzy_vulnerability", "Damage Taken +15%", 20f, frenzyIcon);
        profile.vulnerability.policy.appliesOn = RuntimeEffectApplyTarget.Enemy;
        profile.vulnerability.policy.kind = RuntimeEffectKind.Debuff;
        profile.vulnerability.parameters.overrideDamageModifier = true;
        profile.vulnerability.parameters.damageModifier.incomingDamageTakenPercentAdd = 15f;
        profile.frenzyBleed = Bleed(additional, "goenitz.frenzy_bleed", .5f);
        profile.exBleed = Bleed(additional, "goenitz.ex_bleed", .7f);
        profile.finisherBleed = Bleed(additional, "goenitz.shin_bleed", 5f);
        character.kitDefinition.runtimeDefinition = profile;

        SkillAction yonokaze = table.skillActions.Single(a => a.actionId == "goenitz.skill.yonokaze");
        yonokaze.actionDamageMultiplier = 3.5f;
        yonokaze.cooldown = 3f;
        yonokaze.description = "Summon a tornado dealing {multiplierPercent} ATK over its hits. Critical hits deal 150% of their normally resolved critical damage.";
        ProjectileData normal = Load<ProjectileData>(Folder + "ProjectileData_goe_skill.asset");
        ProjectileData upgraded = Load<ProjectileData>(Folder + "ProjectileData_goe_skill_lv4.asset");
        ConfigureTornado(normal, 8, 1f, 8f / 60f);
        ConfigureTornado(upgraded, 15, 1.45f, 6f / 60f);
        ProjectileData red = CloneProjectile(normal, Folder + "ProjectileData_goe_ex_skill.asset");
        ProjectileData redUpgraded = CloneProjectile(upgraded, Folder + "ProjectileData_goe_ex_skill_lv4.asset");
        AnimationClip redClip = Load<AnimationClip>(Folder + "Animation/goe_vfx_ex-skill-projectile.anim");
        ConfigureRed(red, redClip, 20, 10f, .4f);
        ConfigureRed(redUpgraded, redClip, 32, 14.5f, .3f);

        SkillAction ex = table.skillActions.FirstOrDefault(a => a.actionId == profile.exSkillActionId || a.actionName == "EX Yonokaze");
        if (ex == null) { ex = new SkillAction(); table.skillActions.Add(ex); }
        ex.actionId = profile.exSkillActionId; ex.actionName = "EX Yonokaze";
        ex.requiresSkillHold = true; ex.skillHoldDuration = .35f;
        ex.context = CombatActionContext.Ground; ex.actionDamageMultiplier = 2.5f; ex.cooldown = 7f;
        ex.conditions = new List<ActionConditionBase> { Potential(PotentialTrack.Skill, 1) };
        ex.description = "Homing red tornado, 10-second lifespan, {multiplierPercent} ATK over its hits. Each critical hit deals 70% ATK additional Bleed damage and grants 5 AE outside Blood Frenzy.";
        AnimationClip exClip = Load<AnimationClip>(Folder + "Animation/goe_ex-skill.anim");
        int exFrames = Mathf.CeilToInt(exClip.length * CombatCore.FrameDataConstants.LOGIC_FPS);
        AudioFrameData normalAudio = yonokaze.comboTree.rootNode.stepData.audioData;
        AudioFrameData exAudio = ex.comboTree?.rootNode?.stepData?.audioData;
        if (exAudio == null || ReferenceEquals(exAudio, normalAudio))
            exAudio = normalAudio == null ? null : JsonUtility.FromJson<AudioFrameData>(JsonUtility.ToJson(normalAudio));
        ex.comboTree = new ComboTree { rootNode = new ComboNode { stepData = new AttackStepData
        {
            stepName = "EX Yonokaze", animationClip = exClip, startupFrames = exFrames / 3,
            activeFrames = 1, recoveryFrames = exFrames - exFrames / 3 - 1, cancelWindowStartFrame = exFrames - 5,
            audioData = exAudio,
            hitData = new HitFrameData { hitboxes = new List<HitboxBase> { new ProjectileSpawnHitbox { projectileData = red, activeFrames = 1 } } }
        } } };

        UltimateAction yami = table.ultimateActions.Single(a => a.actionId == profile.ultimateActionId);
        UltimateAction shin = table.ultimateActions.Single(a => a.actionId == profile.finisherActionId);
        yami.actionDamageMultiplier = 11f; yami.cooldown = 18f; yami.energyCost = 70f;
        yami.description = "Yamidokoku deals 800% + 300% ATK Physical damage. Critical hits deal 150% of their normally resolved critical damage.";
        shin.actionDamageMultiplier = 8.5f; shin.energyCost = 0f;
        shin.conditions = new List<ActionConditionBase> { Potential(PotentialTrack.Ultimate, 3), Tag(profile.modeTag, true) };
        shin.description = "Shin: Yaotome deals {multiplierPercent} ATK Physical damage and 500% ATK additional Bleed damage. Critical hits deal 150% of their normally resolved critical damage.";
        shin.activationDescription = "Press Ultimate during Blood Frenzy; consumes Blood Frenzy.";
        NormalizeUltimates(yami, shin);

        AnimationClip limit = CreateLimitBreak();
        var controller = character.metaData.animatorController as AnimatorController;
        if (controller == null) throw new InvalidOperationException("Goenitz requires an editable animator controller.");
        AnimatorState state = controller.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == limit.name)
            ?? controller.layers[0].stateMachine.AddState(limit.name);
        state.motion = limit;
        UltimateAction transform = table.ultimateActions.FirstOrDefault(a => a.actionId == profile.transformationActionId);
        if (transform == null) { transform = new UltimateAction(); table.ultimateActions.Add(transform); }
        transform.actionId = profile.transformationActionId; transform.actionName = "Blood Frenzy";
        transform.inputTrigger = UltimateInputTrigger.Hold; transform.holdDuration = .35f; transform.energyCost = 0f;
        transform.context = CombatActionContext.Ground; transform.priority = 20; transform.cooldown = 0f;
        transform.activationVoice = yami.activationVoice; transform.activationSFX = yami.activationSFX;
        transform.conditions = new List<ActionConditionBase> { Potential(PotentialTrack.Ultimate, 1), Tag(profile.modeTag, false), new AuraEnergyActionCondition() };
        transform.description = "Consume 3 AE per second, including off-field. Blood Frenzy grants DI/DM/ADI +25% and attacks deal 50% ATK additional Bleed damage. Ordinary AE gains are blocked; qualifying kills restore 6 AE at Ultimate Lv.4.";
        transform.activationDescription = "Hold Ultimate with positive AE; no entry cost.";
        int frames = Mathf.CeilToInt(limit.length * CombatCore.FrameDataConstants.LOGIC_FPS);
        transform.comboTree = new ComboTree { rootNode = new ComboNode { stepData = new AttackStepData
        {
            stepName = "Blood Frenzy", animationClip = limit, startupFrames = Mathf.Max(1, frames / 3),
            activeFrames = 1, recoveryFrames = Mathf.Max(1, frames - frames / 3 - 1), cancelWindowStartFrame = frames
        } } };

        ReviseNodes(character, profile, upgraded, redUpgraded);
        foreach (ComboNode node in Nodes(table.comboActions.Select(a => a.comboTree.rootNode)))
            foreach (ComboRoute route in node.routes)
                if (route.targetNode?.stepData?.stepName == "Attack2-2")
                {
                    route.followUpOnly = true;
                    route.conditions = new List<ActionConditionBase> { Potential(PotentialTrack.Passive, 2), Tag(profile.modeTag, true) };
                }
        AddEntry(character, ex, "skill", PotentialTrack.Skill, 1);
        AddEntry(character, transform, "ultimate", PotentialTrack.Ultimate, 1);
        CharacterKitPresentationDefinition.Entry shinEntry = character.kitDefinition.presentation.entries.First(e => e.actionId == shin.actionId);
        shinEntry.minimumPotentialLevel = 3; shinEntry.unlockTrack = PotentialTrack.Ultimate;
        foreach (Object asset in new Object[] { character, table, profile, defense, normal, upgraded, red, redUpgraded, limit, controller })
        {
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
        }
        Debug.Log("Goenitz Blood Frenzy kit authored. Existing QTE and unrelated animation edits preserved.");
    }

    private static void ReviseNodes(CharacterObject character, GoenitzKitRuntimeDefinition data, ProjectileData normalUpgrade, ProjectileData redUpgrade)
    {
        CharacterKitDefinition kit = character.kitDefinition;
        PotentialNodeDefinition oldDash = kit.GetNodeById("goenitz.passive.lv2");
        PotentialNodeDefinition oldAfter = kit.GetNodeById("goenitz.passive.lv3");
        var dash = oldDash.dashThroughDamageEffects.ToArray();
        var afterDamage = oldAfter.additionalDamageEffects.ToArray();
        var afterTags = oldAfter.runtimeTagEffects.ToArray();
        var afterActions = oldAfter.actionModifiers.ToArray();
        // On repeat migration the merged Lv.2 is the source, not the emptied Lv.3.
        if (afterDamage.Length == 0)
        {
            afterDamage = oldDash.additionalDamageEffects.ToArray(); afterTags = oldDash.runtimeTagEffects.ToArray();
            afterActions = oldDash.actionModifiers.ToArray();
        }
        var descriptions = new Dictionary<PotentialTrack, string[]>
        {
            [PotentialTrack.Skill] = new[] { "Unlock EX Yonokaze with Hold Skill.", "Increase CR by 17 percentage points.", "Increase CD by 43 percentage points.", "Both tornado variants gain larger coverage, longer lifespan and more hits." },
            [PotentialTrack.Ultimate] = new[] { "Hold Ultimate unlocks Blood Frenzy, fueled by the existing AE gauge.", "Hits by any Blood Frenzy actor apply +15% damage taken for 20 seconds.", "Blood Frenzy unlocks Shin: Yaotome. Goenitz Ultimate/limit-break use grants Blizzard, all-stat boost for 15 seconds; one refreshed stack, including +10 ADI.", "Goenitz kills during Blood Frenzy grant +3% ATK, maximum 10 stacks, until damaged; restore 6 AE even at the stack cap." },
            [PotentialTrack.Passive] = new[] { "Critical hits permanently reduce enemy Physical resistance by 15 percentage points; does not stack.", "During Blood Frenzy, unlock Attack2-2; dash through enemies for 100% ATK Physical damage. After dash, next damaging hit adds 4% enemy MaxHP (cap 500% ATK); next normal begins at stage 4.", "Share Blood Frenzy's DI/DM/ADI and Bleed attack effect with teammates for Goenitz's remaining duration.", "Teammates sharing Blood Frenzy gain +35 percentage points evasion for its remaining duration." }
        };
        foreach (var pair in descriptions)
            for (int i = 1; i <= 4; i++)
            {
                string id = "goenitz." + pair.Key.ToString().ToLowerInvariant() + ".lv" + i;
                PotentialNodeDefinition old = kit.GetNodeById(id);
                int index = kit.potentialNodes.IndexOf(old);
                var node = new PotentialNodeDefinition { nodeId = id, nodeName = pair.Key + " " + i, track = pair.Key, level = i,
                    fragmentCost = old.fragmentCost, description = pair.Value[i - 1] };
                string entry = pair.Key == PotentialTrack.Skill ? "goenitz.skill.yonokaze" : pair.Key == PotentialTrack.Ultimate ? data.ultimateActionId : "passive";
                node.kitInfoPatches.Add(new CharacterKitPresentationDefinition.Patch { entryId = entry, noteId = id, appendNote = true, description = node.description });
                node.skillPanelDescriptionOverride = string.Join("\n", pair.Value.Take(i));
                kit.potentialNodes[index] = node;
            }
        kit.GetNodeById("goenitz.skill.lv2").statModifiers.Add(new PassiveStatModifierDefinition { target = PassiveModifierTarget.CritRate, value = 17f });
        kit.GetNodeById("goenitz.skill.lv3").statModifiers.Add(new PassiveStatModifierDefinition { target = PassiveModifierTarget.CritDamage, value = 43f });
        foreach (var pair in new[] { ("goenitz.skill.yonokaze", normalUpgrade), (data.exSkillActionId, redUpgrade) })
            kit.GetNodeById("goenitz.skill.lv4").actionModifiers.Add(new PassiveActionModifierDefinition { category = AttackCategory.Skill, actionId = pair.Item1, replacementProjectileData = pair.Item2 });
        var merged = kit.GetNodeById("goenitz.passive.lv2");
        foreach (var effect in dash) { effect.attackRatio = 1f; effect.elementType = ElementType.None; effect.filter.requireRuntimeTag = true; effect.filter.runtimeTag = data.modeTag; merged.dashThroughDamageEffects.Add(effect); }
        merged.additionalDamageEffects.AddRange(afterDamage);
        merged.runtimeTagEffects.AddRange(afterTags);
        foreach (var effect in merged.runtimeTagEffects)
            if (effect.addTag) { effect.filter.requireRuntimeTag = true; effect.filter.runtimeTag = data.modeTag; }
        foreach (var modifier in afterActions)
        {
            modifier.consumeRequiredTagsOnUse = false;
            modifier.startingComboStepNameOverride = "Attack4";
            modifier.requiredTags.RemoveAll(t => t.tag == data.modeTag);
            modifier.requiredTags.Add(new RuntimeTagRequirement { tag = data.modeTag });
            merged.actionModifiers.Add(modifier);
        }
        kit.baseSkillNode.skillPanelDescriptionOverride = "Yonokaze: 350% ATK, 3-second cooldown. Critical hits deal 150% of resolved critical damage.";
        kit.baseSkillNode.kitInfoPatches.Clear();
        kit.baseUltimateNode.skillPanelDescriptionOverride = "Yamidokoku: 800% + 300% ATK, 18-second cooldown, 70 AE. Critical hits deal 150% of resolved critical damage.";
        kit.baseUltimateNode.kitInfoPatches.Clear();
        kit.basePassiveNode.skillPanelDescriptionOverride = "35% chance to evade incoming enemy attacks.";
        kit.basePassiveNode.kitInfoPatches.Clear();
        kit.basePassiveNode.description = kit.basePassiveNode.skillPanelDescriptionOverride;
    }

    private static void NormalizeUltimates(UltimateAction yami, UltimateAction shin)
    {
        foreach (ComboNode node in Nodes(new[] { yami.comboTree.rootNode }))
            if (node.stepData?.hitData != null)
                foreach (HitboxBase hit in node.stepData.hitData.hitboxes)
                {
                    if (hit is NormalHitbox normal) normal.damageRatio = 3f / 11f;
                    if (hit is ProjectileSpawnHitbox spawn && spawn.projectileData != null)
                    {
                        ProjectileData projectile = spawn.projectileData;
                        projectile.hitIntervalSeconds = projectile.hitIntervalFrames / 60f;
                        if (projectile.ResolveRoutineHitbox() is NormalHitbox box) box.damageRatio = 8f / 11f / projectile.maxHits;
                        EditorUtility.SetDirty(projectile); AssetDatabase.SaveAssetIfDirty(projectile);
                    }
                }
        var normals = new List<NormalHitbox>();
        var projectiles = new HashSet<ProjectileData>();
        foreach (ComboNode node in Nodes(new[] { shin.comboTree.rootNode }))
            if (node.stepData?.hitData != null)
                foreach (HitboxBase hit in node.stepData.hitData.hitboxes)
                {
                    if (hit is NormalHitbox normal) normals.Add(normal);
                    if (hit is ProjectileSpawnHitbox spawn && spawn.projectileData != null) projectiles.Add(spawn.projectileData);
                }
        float weight = normals.Sum(n => n.damageRatio);
        foreach (ProjectileData projectile in projectiles)
        {
            projectile.hitIntervalSeconds = projectile.hitIntervalFrames / 60f;
            projectile.maxHits = Mathf.Min(projectile.maxHits, Mathf.CeilToInt(projectile.lifetime / projectile.hitIntervalSeconds));
            if (projectile.ResolveRoutineHitbox() is NormalHitbox box) weight += box.damageRatio * projectile.maxHits;
        }
        if (weight <= 0f) throw new InvalidOperationException("Shin must retain a damage budget.");
        foreach (NormalHitbox hit in normals) hit.damageRatio /= weight;
        foreach (ProjectileData projectile in projectiles)
        {
            if (projectile.ResolveRoutineHitbox() is NormalHitbox box) box.damageRatio /= weight;
            EditorUtility.SetDirty(projectile); AssetDatabase.SaveAssetIfDirty(projectile);
        }
    }

    public static IEnumerable<ComboNode> Nodes(IEnumerable<ComboNode> roots)
    {
        var seen = new HashSet<ComboNode>(); var pending = new Stack<ComboNode>(roots);
        while (pending.Count > 0)
        {
            ComboNode node = pending.Pop(); if (node == null || !seen.Add(node)) continue;
            yield return node;
            pending.Push(node.defaultNext); pending.Push(node.onGrabSuccessNode);
            foreach (ComboRoute route in node.routes) pending.Push(route.targetNode);
            if (node.stepData?.triggerWindows != null) foreach (TriggerWindow window in node.stepData.triggerWindows) pending.Push(window.targetNode);
        }
    }

    private static void ConfigureTornado(ProjectileData data, int hits, float lifetime, float interval)
    {
        data.maxHits = hits; data.lifetime = lifetime; data.hitIntervalSeconds = interval;
        if (data.ResolveRoutineHitbox() is NormalHitbox box) box.damageRatio = 1f / hits;
    }

    private static void ConfigureRed(ProjectileData data, AnimationClip clip, int hits, float lifetime, float interval)
    {
        ConfigureTornado(data, hits, lifetime, interval);
        data.spawnClip = clip; data.loopClip = clip; data.traversalMode = ProjectileTraversalMode.GroundHoming;
        data.spawnPositionMode = SpawnPositionMode.Caster; data.spawnOffset = new Vector2(1.5f, 0f);
        data.speed = 4f; data.stopOnImpact = false; data.trackingRadius = 20f;
        data.destroyWhenHitsExhausted = false; data.destroyOnLifetimeExpire = true;
    }

    private static ProjectileData CloneProjectile(ProjectileData source, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<ProjectileData>(path);
        if (existing != null) return existing;
        var clone = Object.Instantiate(source); clone.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(clone, path); return clone;
    }

    private static AnimationClip CreateLimitBreak()
    {
        string path = Folder + "Animation/goenitz_limit-break.anim";
        var clip = LoadOrCreate<AnimationClip>(path);
        Sprite[] sprites = AssetDatabase.FindAssets("Goenitz_limit-break_ t:Texture2D", new[] { Folder + "Goenitz sprite" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => int.Parse(System.IO.Path.GetFileNameWithoutExtension(p).Split('_').Last()))
            .Select(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>().First()).ToArray();
        if (sprites.Length == 0) throw new InvalidOperationException("Limit-break sprites missing.");
        clip.frameRate = 24f;
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 24f, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / 24f, value = sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        return clip;
    }

    private static RuntimeEffectRecipe Recipe(RuntimeEffectDefinition definition, string id, string name, float duration, Sprite icon = null)
        => new RuntimeEffectRecipe
        {
            recipeId = id, definition = definition, presentation = new RuntimeEffectPresentation { effectId = id, displayName = name, icon = icon, showRemainingTime = true },
            policy = new RuntimeEffectApplicationPolicy { durationType = duration < 0 ? RuntimeEffectDurationType.Forever : RuntimeEffectDurationType.Seconds,
                durationSeconds = Mathf.Max(0f, duration), stackMode = RuntimeEffectStackMode.Override, stackLimit = 1, stackKey = id, refreshDurationOnReapply = true }
        };

    private static RuntimeEffectRecipe Bleed(RuntimeEffectDefinition definition, string id, float ratio)
    {
        var recipe = Recipe(definition, id, "Bleed", -1f);
        recipe.parameters.overrideAdditionalDamage = true;
        recipe.parameters.additionalDamage = new RuntimeEffectAdditionalDamageParameters
        { attackRatio = ratio, elementType = ElementType.Bleed, applyNativeDot = true };
        return recipe;
    }

    private static void AddStat(RuntimeEffectRecipe recipe, RuntimeEffectStatTarget target, RuntimeEffectStatOperation operation, float value)
    {
        recipe.parameters.overrideStatModifiers = true;
        recipe.parameters.statModifiers.Add(new RuntimeEffectStatModifier { target = target, operation = operation, value = value });
    }

    private static PotentialLevelActionCondition Potential(PotentialTrack track, int level) => new PotentialLevelActionCondition { track = track, level = level };
    private static RuntimeTagActionCondition Tag(string tag, bool exists) => new RuntimeTagActionCondition { tag = tag, shouldExist = exists };
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing " + path);
    private static T LoadOrCreate<T>(string path) where T : Object
    {
        T value = AssetDatabase.LoadAssetAtPath<T>(path);
        if (value != null) return value;
        value = typeof(T) == typeof(AnimationClip) ? new AnimationClip() as T : ScriptableObject.CreateInstance(typeof(T)) as T;
        value.name = System.IO.Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(value, path); return value;
    }

    private static void AddEntry(CharacterObject character, CombatAction action, string section, PotentialTrack track, int level)
    {
        var entries = character.kitDefinition.presentation.entries;
        if (entries.Any(e => e.actionId == action.actionId)) return;
        entries.Add(new CharacterKitPresentationDefinition.Entry { entryId = action.actionId, actionId = action.actionId,
            sectionId = section, unlockTrack = track, minimumPotentialLevel = level, order = 2, activationDescription = action.activationDescription });
    }
}
