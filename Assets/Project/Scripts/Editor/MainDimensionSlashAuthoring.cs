#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Project.Combat;

public static class MainDimensionSlashAuthoring
{
    public const string ActionId = "main.skill.dimension_slash";
    public const string CutId = "main.dash.dimension_cut";
    public const string CountId = "main.dimension_slash.cut_count";
    public const string ReadyTag = "main.dimension_slash.ready";
    public const string MainPath = "Assets/Project/Character/Hero/test_0_Main/";
    public const string Root = MainPath + "DimensionSlash/";

    [MenuItem("Tools/Project/Combat/Author Main Dimension Slash")]
    public static void Author()
    {
        CharacterObject character = AssetDatabase.LoadAssetAtPath<CharacterObject>(MainPath + "CharacterObject_main.asset");
        BuildProceduralMaterial();
        ExecutionBreakdownAuthoring.BuildMaterial();
        BuildAnticipationClip();
        Undo.RecordObjects(new UnityEngine.Object[] { character, character.actionResolverTable }, "Author Main Dimension Slash");
        ClearUnconfiguredScreenSpanDefaults(character);
        Configure(character);
        BindActorState(character);
        ExecutionBreakdownAuthoring.BindActor(character);
        EditorUtility.SetDirty(character);
        EditorUtility.SetDirty(character.actionResolverTable);
        AssetDatabase.SaveAssetIfDirty(character);
        AssetDatabase.SaveAssetIfDirty(character.actionResolverTable);
        Debug.Log("Dimension Slash: Passive4 / five paid cuts / 5s Skill window authored.");
    }

    [MenuItem("Tools/Project/Combat/Set Dimension Slash Hit Feedback")]
    public static void SetHitFeedback()
    {
        var table = AssetDatabase.LoadAssetAtPath<ActionResolverTable>(MainPath + "ActionResolverTable_main.asset");
        var action = table?.skillActions?.Find(a => a != null && a.actionId == ActionId);
        if (action?.comboTree?.rootNode == null) throw new InvalidOperationException("Main Dimension Slash action is missing.");
        var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(MainPath + "main_SFX/SFX_Slash_1.mp3");
        if (sound == null) throw new InvalidOperationException("Existing slash hit sound is missing.");
        int count = ConfigureSlashHitFeedback(action.comboTree.rootNode, sound, new HashSet<ComboNode>());
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssetIfDirty(table);
        Debug.Log($"Dimension Slash hit feedback configured on {count} normal hitbox(es).");
    }

    // screenSpanningCount was introduced for this action. Strip the field's
    // former initializer value from Main's existing steps before authoring its
    // explicit five full-screen accents, so unrelated slash fields stay local.
    private static void ClearUnconfiguredScreenSpanDefaults(CharacterObject character)
    {
        var visited = new HashSet<ComboNode>();
        foreach (var action in character.actionResolverTable.comboActions) ClearScreenSpanDefaults(action?.comboTree?.rootNode, visited);
        foreach (var action in character.actionResolverTable.skillActions) ClearScreenSpanDefaults(action?.comboTree?.rootNode, visited);
        foreach (var action in character.actionResolverTable.ultimateActions) ClearScreenSpanDefaults(action?.comboTree?.rootNode, visited);
        foreach (var action in character.actionResolverTable.standaloneActions) ClearScreenSpanDefaults(action?.comboTree?.rootNode, visited);
        ClearScreenSpanDefaults(character.qteSwap?.GetIntroComboTree(character.actionResolverTable)?.rootNode, visited);
    }

    private static void ClearScreenSpanDefaults(ComboNode node, HashSet<ComboNode> visited)
    {
        if (node == null || !visited.Add(node)) return;
        var entries = node.stepData?.eventData?.entries;
        if (entries != null)
            foreach (var entry in entries)
                if (entry?.slashFieldSettings != null) entry.slashFieldSettings.screenSpanningCount = 0;
        ClearScreenSpanDefaults(node.defaultNext, visited);
        ClearScreenSpanDefaults(node.onGrabSuccessNode, visited);
        if (node.routes != null)
            foreach (var route in node.routes) ClearScreenSpanDefaults(route?.targetNode, visited);
        if (node.stepData?.triggerWindows != null)
            foreach (var trigger in node.stepData.triggerWindows) ClearScreenSpanDefaults(trigger?.targetNode, visited);
    }

    private static void BindActorState(CharacterObject character)
    {
        var controller = character.metaData.animatorController as AnimatorController;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/main_dimension_slash_prepare.anim");
        if (controller == null || clip == null) throw new InvalidOperationException("Main actor controller/clip is missing.");
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = null;
        foreach (var child in machine.states) if (child.state.name == clip.name) state = child.state;
        if (state == null) state = machine.AddState(clip.name, new Vector3(700f, 450f, 0f));
        state.motion = clip;
        state.speed = 0f;
        // The FSM owns exit/cancel; existing locomotion methods select the return state.
        EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
    }

    public static void Configure(CharacterObject character)
    {
        var node = character.kitDefinition.potentialNodes.Find(n => n.track == PotentialTrack.Passive && n.level == 4);
        if (node == null) throw new InvalidOperationException("Main Passive Lv4 is missing.");
        node.conditionalStateEffects ??= new List<PassiveConditionalStateEffectDefinition>();
        node.conditionalStateEffects.RemoveAll(e => e != null && e.filter != null
            && ((e.filter.actionId == CutId && e.filter.requireManualOnFieldActionStart) || e.filter.actionId == ActionId));
        node.conditionalStateEffects.Add(new PassiveConditionalStateEffectDefinition
        {
            trigger = PassiveTriggerType.OnActionStarted, filter = ManualCutFilter(),
            requiredTags = new List<RuntimeTagRequirement> { new RuntimeTagRequirement { tag = ReadyTag, expectedValue = false } },
            counterEffects = new List<PassiveCounterEffectDefinition>
            {
                new PassiveCounterEffectDefinition { counterId = CountId, operation = PassiveCounterOperation.Add, value = 1 }
            }
        });
        node.conditionalStateEffects.Add(new PassiveConditionalStateEffectDefinition
        {
            trigger = PassiveTriggerType.OnActionStarted, filter = ManualCutFilter(),
            requiredTags = new List<RuntimeTagRequirement> { new RuntimeTagRequirement { tag = ReadyTag, expectedValue = false } },
            requiredCounters = new List<RuntimeCounterRequirement>
            {
                new RuntimeCounterRequirement { counterId = CountId, comparison = CounterComparison.GreaterThanOrEqual, value = 5 }
            },
            runtimeTagEffects = new List<PassiveRuntimeTagEffectDefinition>
            {
                new PassiveRuntimeTagEffectDefinition { tag = ReadyTag, addTag = true, duration = 5f }
            },
            counterEffects = new List<PassiveCounterEffectDefinition>
            {
                new PassiveCounterEffectDefinition { counterId = CountId, operation = PassiveCounterOperation.Reset }
            }
        });
        node.conditionalStateEffects.Add(new PassiveConditionalStateEffectDefinition
        {
            trigger = PassiveTriggerType.OnActionStarted,
            filter = new PassiveTriggerFilter { filterByCategory = true, category = AttackCategory.Skill, filterByActionId = true, actionId = ActionId },
            runtimeTagEffects = new List<PassiveRuntimeTagEffectDefinition>
            {
                new PassiveRuntimeTagEffectDefinition { tag = ReadyTag, addTag = false }
            }
        });
        node.actionModifiers ??= new List<PassiveActionModifierDefinition>();
        node.actionModifiers.RemoveAll(m => m != null && m.actionId == ActionId);
        node.actionModifiers.Add(new PassiveActionModifierDefinition
        {
            category = AttackCategory.Skill, actionId = ActionId,
            requiredTags = new List<RuntimeTagRequirement> { new RuntimeTagRequirement { tag = ReadyTag } }
        });
        const string description = " Perform 5 paid Dimension Cuts to open a 5s Skill follow-up for Judgment Cut.";
        node.description = node.description.Replace("1.5s Skill follow-up", "5s Skill follow-up");
        node.skillPanelDescriptionOverride = node.skillPanelDescriptionOverride.Replace("1.5s Skill follow-up", "5s Skill follow-up");
        node.description = node.description.Replace("5s Ultimate follow-up", "5s Skill follow-up");
        node.skillPanelDescriptionOverride = node.skillPanelDescriptionOverride.Replace("5s Ultimate follow-up", "5s Skill follow-up");
        node.description = node.description.Replace("follow-up for Dimension Slash.", "follow-up for Judgment Cut.");
        node.skillPanelDescriptionOverride = node.skillPanelDescriptionOverride.Replace("follow-up for Dimension Slash.", "follow-up for Judgment Cut.");
        if (!node.description.Contains("5s Skill follow-up")) node.description += description;
        if (!node.skillPanelDescriptionOverride.Contains("5s Skill follow-up")) node.skillPanelDescriptionOverride += description;

        var table = character.actionResolverTable;
        table.ultimateActions ??= new List<UltimateAction>();
        table.skillActions ??= new List<SkillAction>();
        var legacy = table.ultimateActions.Find(a => a != null && a.actionId == ActionId);
        var skill = table.skillActions.Find(a => a != null && a.actionId == ActionId);
        if (skill == null)
        {
            skill = new SkillAction
            {
                actionId = ActionId, actionName = legacy?.actionName ?? "Judgment Cut", priority = legacy?.priority ?? 1,
                resourceCounterId = legacy?.resourceCounterId, resourceCost = legacy?.resourceCost ?? 0,
                commitsDash = legacy?.commitsDash ?? false, requiresEnemyTarget = legacy?.requiresEnemyTarget ?? false,
                reselectOnAttackInput = legacy?.reselectOnAttackInput ?? false, targetRadius = legacy?.targetRadius ?? 8f,
                context = legacy?.context ?? CombatActionContext.Both,
                outputContext = legacy?.outputContext ?? CombatActionOutputContext.MatchInput,
                cooldown = legacy?.cooldown ?? 0f,
                damageSourceStat = legacy?.damageSourceStat ?? ActionDamageSourceStat.Attack,
                actionDamageMultiplier = legacy?.actionDamageMultiplier ?? 5f,
                auraEnergyReward = legacy?.auraEnergyReward ?? 0f,
                conditions = legacy?.conditions ?? new List<ActionConditionBase>
                {
                    new PotentialLevelActionCondition { track = PotentialTrack.Passive, level = 4 },
                    new RuntimeTagActionCondition { tag = ReadyTag }
                },
                comboTree = legacy?.comboTree ?? new ComboTree { treeName = "Dimension Slash", rootNode = new ComboNode
                { stepData = CreateStep(), defaultNext = new ComboNode { stepData = CreateFinalCut(table) } } }
            };
            table.skillActions.Add(skill);
        }
        skill.actionType = CombatActionType.Ultimate;
        ConfigureSlashHitFeedback(skill.comboTree?.rootNode,
            AssetDatabase.LoadAssetAtPath<AudioClip>(MainPath + "main_SFX/SFX_Slash_1.mp3"), new HashSet<ComboNode>());
        table.ultimateActions.RemoveAll(a => a != null && a.actionId == ActionId);
        ExecutionBreakdownAuthoring.Configure(character);
    }

    private static int ConfigureSlashHitFeedback(ComboNode node, AudioClip sound, HashSet<ComboNode> visited)
    {
        if (node == null || !visited.Add(node)) return 0;
        int count = 0;
        var step = node.stepData;
        if ((step?.stepName == "DimensionSlash" || step?.stepName == "DimensionSlashFinalCut")
            && step.hitData?.hitboxes != null)
        {
            foreach (var hitbox in step.hitData.hitboxes)
            {
                if (hitbox is not NormalHitbox normal) continue;
                normal.elementType = ElementType.None;
                normal.suppressContactVfx = false;
                normal.hitVFXType = VFXType.HitSpark_og_Slash;
                normal.hitVFXPrefab = null;
                normal.hitSFX = sound;
                count++;
            }
        }
        count += ConfigureSlashHitFeedback(node.defaultNext, sound, visited);
        count += ConfigureSlashHitFeedback(node.onGrabSuccessNode, sound, visited);
        if (node.routes != null)
            foreach (var route in node.routes) count += ConfigureSlashHitFeedback(route?.targetNode, sound, visited);
        if (step?.triggerWindows != null)
            foreach (var trigger in step.triggerWindows) count += ConfigureSlashHitFeedback(trigger?.targetNode, sound, visited);
        return count;
    }

    private static PassiveTriggerFilter ManualCutFilter() => new PassiveTriggerFilter
    {
        filterByCategory = true, category = AttackCategory.Normal, filterByActionId = true, actionId = CutId,
        filterByStepName = true, stepName = "DimensionCut", requireManualOnFieldActionStart = true
    };

    public static AttackStepData CreateStep()
    {
        var step = new AttackStepData
        {
            stepName = "DimensionSlash", startupFrames = 18, activeFrames = 85, recoveryFrames = 1,
            cancelWindowStartFrame = 104, loopActivePhase = false, autoChainToNext = true,
            syncAnimationToActionFrames = true,
            animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/main_dimension_slash_prepare.anim")
                ?? AssetDatabase.LoadAssetAtPath<AnimationClip>(MainPath + "animation/main_Idle.anim"),
            hitData = new HitFrameData(), eventData = new ActionEventFrameData(),
            vfxData = new VFXFrameData(), audioData = new AudioFrameData(), motionData = new MotionFrameData()
        };
        for (int i = 0; i < 19; i++)
        {
            step.hitData.hitboxes.Add(new NormalHitbox
            {
                startFrameOffset = 24 + i * 3 - step.ActivePhaseStart, activeFrames = 2,
                elementType = ElementType.None,
                offset = new Vector2(4f, 1.6f), size = new Vector2(12f, 6f),
                damageRatio = 0.58f / 19f, impactFreezeFrames = 0, impactTier = DamageImpactTier.Low,
                suppressImpactFeedback = true, suppressContactVfx = false,
                hitVFXType = VFXType.HitSpark_og_Slash,
                hitSFX = AssetDatabase.LoadAssetAtPath<AudioClip>(MainPath + "main_SFX/SFX_Slash_1.mp3"),
                horizontalSpeed = 0f, launchSpeed = 0f, knockbackFrames = 2, recoveryFrames = 1,
                flashColor = new Color(0.7f, 0.35f, 1f), flashFrames = 1
            });
        }
        step.motionData.keyframes.Add(new MotionKeyframe { startFrame = 9, endFrame = 18,
            position = new Vector3(1.2f, 0f), easing = LeanTweenType.easeOutQuad });
        step.motionData.keyframes.Add(new MotionKeyframe { startFrame = 88, endFrame = 90,
            position = new Vector3(0f, 2.6f), easing = LeanTweenType.easeOutQuad });
        step.motionData.keyframes.Add(new MotionKeyframe { startFrame = 94, endFrame = 103,
            position = new Vector3(0f, -2.6f), easing = LeanTweenType.easeInQuad });
        step.eventData.entries.Add(new ActionEventEntry { label = "Purple dimension slash field",
            eventType = ActionEventType.ProceduralSlashField, triggerFrame = 18, freezeWorld = false,
            slashFieldSettings = new ProceduralSlashFieldSettings
            {
                material = AssetDatabase.LoadAssetAtPath<Material>(Root + "DimensionSlashRibbon.mat"),
                firstFrame = 0, endFrame = 72, hideFromFrame = 0, showAtFrame = 72,
                slashCount = 19, intervalFrames = 2, seamCount = 12, screenSpanningCount = 5,
                lengthRange = new Vector2(3f, 46f), viewportCoverage = 1.8f,
                widthRange = new Vector2(0.08f, 1.8f), overallScale = 1.95f,
                foregroundFraction = 0.5f,
                revealFrames = new Vector2(1f, 3f), decayFrames = new Vector2(16f, 27f),
                impactDistortion = new CutImpactDistortionSettings { enabled = true }
            } });
        AddAudio(step);
        return step;
    }

    public static AttackStepData CreateFinalCut(ActionResolverTable table)
    {
        var cut = table.standaloneActions.Find(a => a != null && a.actionId == CutId)?.comboTree?.rootNode?.stepData;
        if (cut == null) throw new InvalidOperationException("Existing Dimension Cut root is missing.");
        var step = new AttackStepData
        {
            stepName = "DimensionSlashFinalCut", animationClip = cut.animationClip,
            startupFrames = cut.startupFrames, activeFrames = cut.activeFrames, recoveryFrames = cut.recoveryFrames,
            cancelWindowStartFrame = cut.cancelWindowStartFrame, lockAirPosition = cut.lockAirPosition,
            motionData = cut.motionData != null ? JsonUtility.FromJson<MotionFrameData>(JsonUtility.ToJson(cut.motionData)) : null,
            audioData = cut.audioData != null ? JsonUtility.FromJson<AudioFrameData>(JsonUtility.ToJson(cut.audioData)) : new AudioFrameData(),
            hitData = new HitFrameData(), vfxData = new VFXFrameData(), eventData = new ActionEventFrameData()
        };
        NormalHitbox source = null;
        foreach (var hit in cut.hitData.hitboxes) if (hit is NormalHitbox normal) { source = normal; break; }
        if (source == null) throw new InvalidOperationException("Existing Dimension Cut hit is missing.");
        var final = JsonUtility.FromJson<NormalHitbox>(JsonUtility.ToJson(source));
        final.damageRatio = 0.42f; final.impactTier = DamageImpactTier.Low;
        final.impactFreezeFrames = 0; final.suppressImpactFeedback = true; final.suppressContactVfx = true;
        step.hitData.hitboxes.Add(final);
        step.eventData.entries.Add(new ActionEventEntry { label = "Final purple cut", triggerFrame = 0,
            eventType = ActionEventType.ProceduralSlashField, freezeWorld = false,
            slashFieldSettings = new ProceduralSlashFieldSettings
            {
                material = AssetDatabase.LoadAssetAtPath<Material>(Root + "DimensionSlashRibbon.mat"), hideActor = false,
                overallScale = 1.95f,
                slashCount = 1, seamCount = 3, firstFrame = Mathf.Max(0, step.ActivePhaseStart + final.startFrameOffset - 4),
                endFrame = step.TotalFrames, spread = new Vector2(0.6f, 0.5f),
                lengthRange = new Vector2(15f, 18f), widthRange = new Vector2(0.7f, 1.2f),
                revealFrames = new Vector2(4f, 4f), decayFrames = new Vector2(12f, 18f),
                impactDistortion = new CutImpactDistortionSettings { enabled = true }
            } });
        return step;
    }

    private static void BuildProceduralMaterial()
    {
        Shader shader = Shader.Find("Project/DimensionSlashRibbon");
        if (shader == null) throw new InvalidOperationException("DimensionSlashRibbon shader not imported.");
        string path = Root + "DimensionSlashRibbon.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        else { material.shader = shader; EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
    }

    private static void BuildAnticipationClip()
    {
        var original = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/main_dimension_slash_actor.anim");
        var jump = AssetDatabase.LoadAssetAtPath<AnimationClip>(MainPath + "animation/main_Jump.anim");
        if (original == null || jump == null) throw new InvalidOperationException("Actor anticipation/plunge sources missing.");
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var originalKeys = AnimationUtility.GetObjectReferenceCurve(original, binding);
        var jumpKeys = AnimationUtility.GetObjectReferenceCurve(jump, binding);
        if (originalKeys == null || originalKeys.Length < 3 || jumpKeys == null || jumpKeys.Length == 0)
            throw new InvalidOperationException("Actor source sprite binding missing.");
        string path = Root + "Animations/main_dimension_slash_prepare.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path); bool create = clip == null;
        if (create) clip = new AnimationClip(); clip.frameRate = 60f;
        int[] frames = { 0, 6, 12, 18, 90, 94, 103, 104 };
        UnityEngine.Object[] sprites = { originalKeys[0].value, originalKeys[1].value, originalKeys[2].value,
            originalKeys[2].value, jumpKeys[0].value, jumpKeys[jumpKeys.Length - 1].value,
            jumpKeys[jumpKeys.Length - 1].value, jumpKeys[jumpKeys.Length - 1].value };
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < keys.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = frames[i] / 60f, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        if (create) AssetDatabase.CreateAsset(clip, path); else { EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip); }
    }

    private static void AddEffect(AttackStepData step, string name, int frame, int duration)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/main_dimension_slash_" + name + ".anim");
        if (clip == null) return;
        step.vfxData.entries.Add(new VFXEntry
        {
            label = "Dimension Slash " + name, triggerFrame = frame, duration = duration, vfxClip = clip,
            vfxType = (VFXType)(name == "burst_a" ? 160 : name == "burst_b" ? 161 : name == "burst_c" ? 162
                : name == "residual" ? 163 : name == "seam" ? 164 : name == "final" ? 165 : 166),
            transformKeys = new List<VFXTransformKey>
            {
                // New sprites register their cut center at (4,1.6) from the action origin.
                // Zero trajectory avoids the existing spawn-offset + trajectory-offset sum.
                new VFXTransformKey { position = Vector3.zero, scale = Vector3.one }
            }
        });
    }

    private static void AddAudio(AttackStepData step)
    {
        var table = AssetDatabase.LoadAssetAtPath<ActionResolverTable>(MainPath + "ActionResolverTable_main.asset");
        var sounds = new List<AudioClip>();
        foreach (var action in table.comboActions) CollectAudio(action.comboTree?.rootNode, sounds, new HashSet<ComboNode>());
        foreach (var action in table.ultimateActions) CollectAudio(action.comboTree?.rootNode, sounds, new HashSet<ComboNode>());
        if (sounds.Count == 0) return;
        int[] frames = { 0, 18, 27, 36, 45, 54, 63, 72, 90 };
        for (int i = 0; i < frames.Length; i++)
            step.audioData.entries.Add(new AudioEntry
            {
                label = "Dimension field accent",
                clip = sounds[i % sounds.Count], triggerFrame = frames[i], duration = 4,
                channel = AudioChannel.ActionSFX, volume = frames[i] == 0 ? 0.4f : 0.3f,
                pitchVariation = 1f + i * 0.025f
            });
    }

    private static void CollectAudio(ComboNode node, List<AudioClip> clips, HashSet<ComboNode> visited)
    {
        if (node == null || !visited.Add(node)) return;
        if (node.stepData?.audioData?.entries != null)
            foreach (var entry in node.stepData.audioData.entries)
                if (entry?.clip != null && entry.channel != AudioChannel.Voice && !clips.Contains(entry.clip)) clips.Add(entry.clip);
        CollectAudio(node.defaultNext, clips, visited);
        if (node.routes != null) foreach (var route in node.routes) CollectAudio(route?.targetNode, clips, visited);
    }
}
#endif
