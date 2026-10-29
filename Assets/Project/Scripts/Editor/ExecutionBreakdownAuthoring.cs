#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ExecutionBreakdownAuthoring
{
    public const string StepName = "ExecutionBreakdown";
    public const string ActionId = "main.ultimate.execution_breakdown";
    private const string LegacyActionId = "main.qte.execution_breakdown";
    private const string LegacyReadyTag = "main.qte.execution_breakdown.ready";
    private const string Root = MainDimensionSlashAuthoring.Root;

    public static void Configure(CharacterObject character)
    {
        var node = character.kitDefinition.basePassiveNode;
        node.conditionalStateEffects ??= new List<PassiveConditionalStateEffectDefinition>();
        node.conditionalStateEffects.RemoveAll(e => e?.filter?.actionId == LegacyActionId
            || e?.runtimeTagEffects?.Exists(effect => effect != null && effect.tag == LegacyReadyTag) == true);
        node.actionModifiers ??= new List<PassiveActionModifierDefinition>();
        node.actionModifiers.RemoveAll(e => e != null && e.actionId == LegacyActionId);
        var table = character.actionResolverTable;
        table.standaloneActions.RemoveAll(a => a != null && a.actionId == LegacyActionId);
        table.ultimateActions ??= new List<UltimateAction>();
        var qte = table.qteActions?.Find(a => a != null && a.actionId == MainLightningPlungeAuthoring.ActionId);
        var intro = qte?.comboTree?.rootNode;
        var execution = table.ultimateActions.Find(a => a != null && a.actionId == ActionId);
        if (execution == null)
        {
            // Transfer the existing node, including every authored reference.
            // Only create defaults when no Execution Breakdown exists at all.
            var existing = intro?.defaultNext;
            execution = new UltimateAction
            {
                actionId = ActionId, actionName = "Execution Breakdown",
                inputTrigger = UltimateInputTrigger.Hold, holdDuration = 0.35f,
                context = CombatActionContext.Both, energyCost = 0f,
                outputContext = qte?.outputContext ?? CombatActionOutputContext.Ground,
                targetRadius = qte?.targetRadius ?? 15f,
                auraEnergyReward = qte?.auraEnergyReward ?? 0f,
                damageSourceStat = qte?.damageSourceStat ?? ActionDamageSourceStat.Attack,
                actionDamageMultiplier = qte?.actionDamageMultiplier ?? 5f,
                comboTree = new ComboTree { treeName = "Execution Breakdown", rootNode =
                    existing?.stepData?.stepName == StepName ? existing : new ComboNode { stepData = CreateStep() } }
            };
            table.ultimateActions.Add(execution);
        }
        if (intro?.defaultNext?.stepData?.stepName == StepName) intro.defaultNext = null;
        if (qte?.comboTree != null) qte.comboTree.treeName = "QTE Lightning Plunge";
    }

    public static AttackStepData CreateStep()
    {
        // Preserve the original approved sprite sequence, contacts, budget and feedback.
        var step = new AttackStepData { stepName = StepName, startupFrames = 11, activeFrames = 48,
            recoveryFrames = 25, cancelWindowStartFrame = 66, syncAnimationToActionFrames = true,
            animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/main_dimension_slash_actor.anim"),
            hitData = new HitFrameData(), vfxData = new VFXFrameData(), eventData = new ActionEventFrameData(), audioData = new AudioFrameData() };
        int[] frames = { 12,20,27,33,38,42,54 };
        for (int i = 0; i < frames.Length; i++)
        {
            bool final = i == 6;
            step.hitData.hitboxes.Add(new NormalHitbox { startFrameOffset = frames[i] - 12, activeFrames = 2,
                elementType = ElementType.Darkness, offset = new Vector2(4f,1.6f), size = new Vector2(final ? 16f : 9f, final ? 7f : 4.5f),
                damageRatio = final ? 0.52f : 0.08f, impactFreezeFrames = final ? 6 : i == 0 || i == 5 ? 2 : 0,
                impactTier = final ? DamageImpactTier.Heavy : DamageImpactTier.Low,
                horizontalSpeed = final ? 5f : 0f, launchSpeed = 0f, knockbackFrames = final ? 12 : 4,
                recoveryFrames = final ? 12 : 2, flashColor = new Color(0.85f,0.7f,1f), flashFrames = final ? 6 : 2 });
            if (!final)
            {
                AddEffect(step, i % 3 == 0 ? "burst_a" : i % 3 == 1 ? "burst_b" : "burst_c", frames[i], 4);
                AddEffect(step, "residual", frames[i] + 2, 12);
            }
        }
        AddEffect(step, "seam",44,10); AddEffect(step,"shards",66,18);
        step.eventData.entries.Add(new ActionEventEntry { label = "Fullscreen purple execution slash", triggerFrame = 54,
            eventType = ActionEventType.ProceduralSlashField, freezeWorld = false,
            backgroundOverlayFlashSettings = new BackgroundOverlayFlashSettings { fadeOutFrames = 8, flashColor = Color.white },
            slashFieldSettings = new ProceduralSlashFieldSettings { fullScreenSlash = true, hideActor = false,
                material = AssetDatabase.LoadAssetAtPath<Material>(Root + "DimensionSlashRibbon.mat"),
                slashCount = 1, seamCount = 0, firstFrame = 0, endFrame = 12, foregroundFraction = 1f,
                fullScreenAngle = 24.2f,
                widthRange = new Vector2(1.2f, 1.2f), revealFrames = Vector2.one, decayFrames = new Vector2(8f, 8f),
                impactDistortion = new CutImpactDistortionSettings { enabled = true, durationFrames = 10,
                    waveWidth = 0.18f, refractionPixels = 48f, blurPixels = 2f } } });
        var camera = new ActionCameraMotionSettings();
        int[] cameraFrames = { 0,12,42,44,50,54,56,60,66 };
        float[] sizes = { 1,1,0.99f,1,1,0.96f,0.98f,0.99f,1 };
        for (int i = 0; i < cameraFrames.Length; i++) camera.keys.Add(new ActionCameraMotionSettings.Key
        { frame = cameraFrames[i], sizeMultiplier = sizes[i], horizontalViewportOffset = cameraFrames[i] == 54 ? -0.0035f : cameraFrames[i] == 56 ? 0.001f : 0f });
        step.eventData.entries.Add(new ActionEventEntry { label = "Execution framing", eventType = ActionEventType.ActionCameraMotion,
            freezeWorld = false, cameraMotionSettings = camera });
        var table = AssetDatabase.LoadAssetAtPath<ActionResolverTable>(MainDimensionSlashAuthoring.MainPath + "ActionResolverTable_main.asset");
        var clips = new List<AudioClip>();
        foreach (var action in table.comboActions) CollectAudio(action.comboTree?.rootNode, clips, new HashSet<ComboNode>());
        foreach (var action in table.ultimateActions) CollectAudio(action.comboTree?.rootNode, clips, new HashSet<ComboNode>());
        int[] audioFrames = {0,12,20,27,33,38,42,50,54};
        for (int i = 0; clips.Count > 0 && i < audioFrames.Length; i++) step.audioData.entries.Add(new AudioEntry
        { label = "Execution accent", clip = clips[i % clips.Count], triggerFrame = audioFrames[i], duration = audioFrames[i] == 54 ? 20 : 4,
            channel = AudioChannel.ActionSFX, volume = audioFrames[i] == 54 ? 1f : audioFrames[i] == 50 ? 0.3f : 0.55f,
            pitchVariation = audioFrames[i] == 54 ? 0.8f : 1f + i * 0.025f });
        return step;
    }

    public static void BuildMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(Root + "DimensionSlashRibbon.mat") == null)
            throw new System.InvalidOperationException("Dimension Slash ribbon material is missing.");
    }

    public static void BindActor(CharacterObject character)
    {
        var controller = character.metaData.animatorController as AnimatorController;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/main_dimension_slash_actor.anim");
        var machine = controller.layers[0].stateMachine; AnimatorState state = null;
        foreach (var child in machine.states) if (child.state.name == clip.name) state = child.state;
        if (state == null) state = machine.AddState(clip.name);
        state.motion = clip; state.speed = 0f; EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
    }

    private static void AddEffect(AttackStepData step, string name, int frame, int duration)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/main_dimension_slash_" + name + ".anim");
        step.vfxData.entries.Add(new VFXEntry { label = "Execution " + name, triggerFrame = frame, duration = duration, vfxClip = clip,
            impactDistortion = name.StartsWith("burst") ? new CutImpactDistortionSettings { enabled = true, durationFrames = duration } : null,
            vfxType = (Project.Combat.VFXType)(name == "burst_a" ? 160 : name == "burst_b" ? 161 : name == "burst_c" ? 162 : name == "residual" ? 163 : name == "seam" ? 164 : 166),
            transformKeys = new List<VFXTransformKey> { new VFXTransformKey { position = Vector3.zero, scale = Vector3.one } } });
    }

    private static void CollectAudio(ComboNode node, List<AudioClip> clips, HashSet<ComboNode> visited)
    {
        if (node == null || !visited.Add(node)) return;
        if (node.stepData?.audioData?.entries != null) foreach (var entry in node.stepData.audioData.entries)
            if (entry?.clip != null && entry.channel != AudioChannel.Voice && !clips.Contains(entry.clip)) clips.Add(entry.clip);
        CollectAudio(node.defaultNext, clips, visited);
        if (node.routes != null) foreach (var route in node.routes) CollectAudio(route?.targetNode, clips, visited);
    }
}
#endif
