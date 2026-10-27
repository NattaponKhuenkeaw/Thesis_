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
        Undo.RecordObjects(new UnityEngine.Object[] { character, character.actionResolverTable }, "Author Main Dimension Slash");
        Configure(character);
        BindActorState(character);
        EditorUtility.SetDirty(character);
        EditorUtility.SetDirty(character.actionResolverTable);
        AssetDatabase.SaveAssetIfDirty(character);
        AssetDatabase.SaveAssetIfDirty(character.actionResolverTable);
        Debug.Log("Dimension Slash: Passive4 / five paid cuts / 1.5s Skill window authored.");
    }

    private static void BindActorState(CharacterObject character)
    {
        var controller = character.metaData.animatorController as AnimatorController;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/main_dimension_slash_actor.anim");
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
                new PassiveRuntimeTagEffectDefinition { tag = ReadyTag, addTag = true, duration = 1.5f }
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
        const string description = " Perform 5 paid Dimension Cuts to open a 1.5s Skill follow-up for Dimension Slash.";
        if (!node.description.Contains("1.5s Skill follow-up")) node.description += description;
        if (!node.skillPanelDescriptionOverride.Contains("1.5s Skill follow-up")) node.skillPanelDescriptionOverride += description;

        var table = character.actionResolverTable;
        table.skillActions.RemoveAll(a => a != null && a.actionId == ActionId);
        var step = CreateStep();
        table.skillActions.Add(new SkillAction
        {
            actionId = ActionId, actionName = "Dimension Slash", priority = 1,
            context = CombatActionContext.Both, outputContext = CombatActionOutputContext.MatchInput,
            cooldown = 0f, actionDamageMultiplier = 5f, auraEnergyReward = 0f,
            conditions = new List<ActionConditionBase>
            {
                new PotentialLevelActionCondition { track = PotentialTrack.Passive, level = 4 },
                new RuntimeTagActionCondition { tag = ReadyTag }
            },
            comboTree = new ComboTree { treeName = "Dimension Slash", rootNode = new ComboNode { stepData = step } }
        });
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
            stepName = "DimensionSlash", startupFrames = 11, activeFrames = 48, recoveryFrames = 25,
            cancelWindowStartFrame = 66, loopActivePhase = false, autoChainToNext = false,
            syncAnimationToActionFrames = true,
            animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/main_dimension_slash_actor.anim")
                ?? AssetDatabase.LoadAssetAtPath<AnimationClip>(MainPath + "animation/main_Idle.anim"),
            hitData = new HitFrameData(), eventData = new ActionEventFrameData(),
            vfxData = new VFXFrameData(), audioData = new AudioFrameData()
        };
        int[] frames = { 12, 20, 27, 33, 38, 42, 54 };
        for (int i = 0; i < frames.Length; i++)
        {
            bool final = i == frames.Length - 1;
            step.hitData.hitboxes.Add(new NormalHitbox
            {
                startFrameOffset = frames[i] - 12, activeFrames = 2,
                elementType = ElementType.Darkness,
                offset = new Vector2(4f, 1.6f), size = new Vector2(final ? 16f : 9f, final ? 7f : 4.5f),
                damageRatio = final ? 0.52f : 0.08f, impactFreezeFrames = final ? 6 : i == 0 || i == 5 ? 2 : 0,
                impactTier = final ? DamageImpactTier.Heavy : DamageImpactTier.Low,
                horizontalSpeed = final ? 5f : 0f, launchSpeed = 0f,
                knockbackFrames = final ? 12 : 4, recoveryFrames = final ? 12 : 2,
                flashColor = new Color(0.85f, 0.7f, 1f), flashFrames = final ? 6 : 2
            });
            string variant = final ? "final" : i % 3 == 0 ? "burst_a" : i % 3 == 1 ? "burst_b" : "burst_c";
            AddEffect(step, variant, frames[i], final ? 30 : 4);
            if (!final) AddEffect(step, "residual", frames[i] + 2, 12);
        }
        AddEffect(step, "seam", 44, 10);
        AddEffect(step, "shards", 66, 18);
        AddAudio(step);
        var motion = new ActionCameraMotionSettings();
        int[] cameraFrames = { 0, 12, 42, 44, 50, 54, 56, 60, 66 };
        float[] sizes = { 1f, 1f, 0.99f, 1f, 1f, 0.96f, 0.98f, 0.99f, 1f };
        for (int i = 0; i < cameraFrames.Length; i++)
            motion.keys.Add(new ActionCameraMotionSettings.Key
            {
                frame = cameraFrames[i], sizeMultiplier = sizes[i],
                horizontalViewportOffset = cameraFrames[i] == 54 ? -0.0035f : cameraFrames[i] == 56 ? 0.001f : 0f
            });
        step.eventData.entries.Add(new ActionEventEntry
        {
            label = "Dimension Slash framing", eventType = ActionEventType.ActionCameraMotion,
            triggerFrame = 0, freezeWorld = false, cameraMotionSettings = motion
        });
        return step;
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
        int[] frames = { 0, 12, 20, 27, 33, 38, 42, 50, 54 };
        for (int i = 0; i < frames.Length; i++)
            step.audioData.entries.Add(new AudioEntry
            {
                label = frames[i] == 54 ? "Dimension tear release" : "Slash accent",
                clip = sounds[i % sounds.Count], triggerFrame = frames[i], duration = frames[i] == 54 ? 20 : 4,
                channel = AudioChannel.ActionSFX, volume = frames[i] == 54 ? 1f : frames[i] == 50 ? 0.3f : 0.55f,
                pitchVariation = frames[i] == 54 ? 0.8f : 1f + i * 0.025f
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
