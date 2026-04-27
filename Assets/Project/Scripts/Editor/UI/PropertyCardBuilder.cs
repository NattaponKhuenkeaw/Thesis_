using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CombatCore;

namespace CombatEditor.UI
{
    /// <summary>
    /// Builds Inspector-style foldout component cards for the right panel.
    /// </summary>
    public class PropertyCardBuilder
    {
        private readonly VisualElement container;
        private readonly Action onDataChanged;
        private readonly Action onStructureChanged;
        private readonly Dictionary<string, bool> foldoutState = new Dictionary<string, bool>();

        private UnityEngine.Object targetObject;
        private ActionNode currentNode;
        private readonly ActionTreeController treeController;

        public PropertyCardBuilder(VisualElement panelContainer, ActionTreeController treeController, Action onDataChanged, Action onStructureChanged)
        {
            container = panelContainer;
            this.treeController = treeController;
            this.onDataChanged = onDataChanged;
            this.onStructureChanged = onStructureChanged;
        }

        public void SetTarget(UnityEngine.Object target)
        {
            this.targetObject = target;
        }

        public void SetCharacter(CharacterObject character)
        {
            this.targetObject = character;
        }

        public void Build(ProjectileData data)
        {
            container.Clear();
            targetObject = data;
            if (data == null) return;
            BuildProjectileDataCards(data);
        }

        public void Build(ActionNode node)
        {
            container.Clear();
            currentNode = node;

            if (node == null || targetObject == null)
            {
                return;
            }

            switch (node.DataType)
            {
                case "AttackStep":
                    BuildAttackStepCards(node.DataRef as AttackStepData);
                    break;
                case "ComboNode":
                    BuildComboNodeCards(node.DataRef as ComboNode);
                    break;
                case "Dash":
                    BuildDashCards(node.DataRef as DashData);
                    break;
                case "Skill":
                case "Ultimate":
                    if (node.DataRef is AttackStepData step) BuildAttackStepCards(step);
                    break;
                case "CombatAction":
                    BuildCombatActionCards(node.DataRef as CombatAction);
                    break;
                case "ResolverTable":
                    BuildResolveTableCards(node.DataRef as ActionResolverTable);
                    break;
            }
        }

        private void BuildProjectileDataCards(ProjectileData data)
        {
            // ── 1. Identity & Prefab ──
            BuildFoldoutCard("Identity & Prefab", "proj_identity", new Color(0.9f, 0.4f, 0.1f), body =>
            {
                AddEnumField(body, "Projectile Type", data.projectileType, value =>
                {
                    RecordUndo("Edit Projectile Type");
                    data.projectileType = (ProjectileType)value;
                    MarkDirty();
                });
                AddObjectField(body, "Projectile Prefab", data.projectilePrefab, value =>
                {
                    RecordUndo("Edit Prefab");
                    data.projectilePrefab = value as GameObject;
                    MarkDirty();
                });
                AddVector2Field(body, "Spawn Offset", data.spawnOffset, value =>
                {
                    RecordUndo("Edit Offset");
                    data.spawnOffset = value;
                    MarkDirty();
                });

                AddSeparator(body, "Animation Clips (Override Controller)");
                AddObjectField(body, "Spawn Clip", data.spawnClip, value =>
                {
                    RecordUndo("Edit Spawn Clip");
                    data.spawnClip = value as AnimationClip;
                    MarkDirty();
                });
                AddObjectField(body, "Loop Clip", data.loopClip, value =>
                {
                    RecordUndo("Edit Loop Clip");
                    data.loopClip = value as AnimationClip;
                    MarkDirty();
                });
            });

            // ── 2. Spawning State ──
            BuildFoldoutCard("Spawning State", "proj_spawn_state", new Color(0.18f, 0.72f, 0.38f), body =>
            {
                AddEnumField(body, "Spawn Position", data.spawnPositionMode, value =>
                {
                    RecordUndo("Edit Spawn Position Mode");
                    data.spawnPositionMode = (SpawnPositionMode)value;
                    MarkDirty();
                });
                AddToggle(body, "Play Spawn Animation", data.playSpawnAnimation, value =>
                {
                    RecordUndo("Toggle Spawn Animation");
                    data.playSpawnAnimation = value;
                    MarkDirty();
                });
                AddFloatField(body, "Spawn Duration", data.spawnDuration, value =>
                {
                    RecordUndo("Edit Spawn Duration");
                    data.spawnDuration = value;
                    MarkDirty();
                });
                AddToggle(body, "Inherit Facing Direction", data.inheritFacingDirection, value =>
                {
                    RecordUndo("Toggle Inherit Facing");
                    data.inheritFacingDirection = value;
                    MarkDirty();
                });

                AddSeparator(body, "Spawning Phase Hitbox");
                BuildPhaseHitboxCard(body, data.spawningHitbox, "spawn_hitbox", "Spawning");
            });

            // ── 3. Updating State ──
            BuildFoldoutCard("Updating State", "proj_update_state", new Color(0.2f, 0.6f, 1f), body =>
            {
                AddEnumField(body, "Traversal Mode", data.traversalMode, value =>
                {
                    RecordUndo("Edit Traversal Mode");
                    data.traversalMode = (ProjectileTraversalMode)value;
                    MarkDirtyAndRebuild();
                });

                if (data.traversalMode == ProjectileTraversalMode.Homing || data.homeToTarget)
                {
                    AddToggle(body, "Home to Target", data.homeToTarget, value =>
                    {
                        RecordUndo("Toggle Homing");
                        data.homeToTarget = value;
                        MarkDirtyAndRebuild();
                    });
                    AddFloatField(body, "Homing Turn Rate", data.homingTurnRate, value =>
                    {
                        RecordUndo("Edit Turn Rate");
                        data.homingTurnRate = value;
                        MarkDirty();
                    });
                }

                if (data.traversalMode == ProjectileTraversalMode.Anchored || data.anchorToCaster)
                {
                    AddToggle(body, "Anchor to Caster", data.anchorToCaster, value =>
                    {
                        RecordUndo("Toggle Anchor");
                        data.anchorToCaster = value;
                        MarkDirtyAndRebuild();
                    });
                }

                AddFloatField(body, "Travel Speed", data.speed, value =>
                {
                    RecordUndo("Edit Speed");
                    data.speed = value;
                    MarkDirty();
                });
                AddFloatField(body, "Lifetime", data.lifetime, value =>
                {
                    RecordUndo("Edit Lifetime");
                    data.lifetime = value;
                    MarkDirty();
                });
                AddIntField(body, "Max Hits", data.maxHits, value =>
                {
                    RecordUndo("Edit Max Hits");
                    data.maxHits = value;
                    MarkDirty();
                });

                AddSeparator(body, "Routine Phase Hitbox");
                BuildPhaseHitboxCard(body, data.routineHitbox, "routine_hitbox", "Routine");
            });

            // ── 4. Behavior ──
            BuildFoldoutCard("Behavior", "proj_behavior", new Color(0.5f, 0.5f, 0.55f), body =>
            {
                AddIntField(body, "Hit Interval (Frames)", data.hitIntervalFrames, value =>
                {
                    RecordUndo("Edit Hit Interval");
                    data.hitIntervalFrames = value;
                    MarkDirty();
                });
                AddToggle(body, "Stop on Impact", data.stopOnImpact, value =>
                {
                    RecordUndo("Toggle Stop on Impact");
                    data.stopOnImpact = value;
                    MarkDirty();
                });
                AddToggle(body, "Destroy when Hits Exhausted", data.destroyWhenHitsExhausted, value =>
                {
                    RecordUndo("Toggle Destroy Exhausted");
                    data.destroyWhenHitsExhausted = value;
                    MarkDirty();
                });
                AddToggle(body, "Destroy on Lifetime Expire", data.destroyOnLifetimeExpire, value =>
                {
                    RecordUndo("Toggle Destroy on Expire");
                    data.destroyOnLifetimeExpire = value;
                    MarkDirty();
                });
            });

            // ── 5. Detection ──
            BuildFoldoutCard("Detection", "proj_detection", new Color(0.8f, 0.2f, 0.2f), body =>
            {
                AddVector2Field(body, "Travel Collider Size", data.travelColliderSize, value =>
                {
                    RecordUndo("Edit Detection Size");
                    data.travelColliderSize = value;
                    MarkDirty();
                });
                AddLayerField(body, "Enemy Layer Mask", data.enemyLayerMask, value =>
                {
                    RecordUndo("Edit Layers");
                    data.enemyLayerMask = value;
                    MarkDirty();
                });
            });

            // ── 6. Spawn Feedback ──
            BuildFoldoutCard("Spawn Feedback", "proj_spawn_fb", new Color(0.6f, 0.1f, 0.9f), body =>
            {
                BuildFeedbackDefinitionCard(body, data.spawnFeedback, "Spawn");
            });

            // ── 7. Expire Feedback ──
            BuildFoldoutCard("Expire Feedback", "proj_expire_fb", new Color(0.7f, 0.2f, 0.8f), body =>
            {
                BuildFeedbackDefinitionCard(body, data.expireFeedback, "Expire");
            });

            // ── 8. Default Impact Feedback ──
            BuildFoldoutCard("Default Impact Feedback", "proj_impact_fb", new Color(1f, 0.8f, 0f), body =>
            {
                AddEnumField(body, "Impact VFX Type", data.impactVFXType, value =>
                {
                    RecordUndo("Edit Impact VFX");
                    data.impactVFXType = (Project.Combat.VFXType)value;
                    MarkDirty();
                });
                AddObjectField(body, "Impact Audio", data.impactAudio, value =>
                {
                    RecordUndo("Edit Impact Audio");
                    data.impactAudio = value as AudioClip;
                    MarkDirty();
                });
            });
        }

        private void BuildPhaseHitboxCard(VisualElement parent, ProjectilePhaseHitboxData phaseData, string foldoutKey, string undoPrefix)
        {
            if (phaseData == null) return;

            AddToggle(parent, "Enabled", phaseData.enabled, value =>
            {
                RecordUndo($"Toggle {undoPrefix} Hitbox Enabled");
                phaseData.enabled = value;
                MarkDirtyAndRebuild();
            });

            if (!phaseData.enabled) return;

            AddToggle(parent, "Decrements Max Hits", phaseData.decrementsMaxHits, value =>
            {
                RecordUndo($"Toggle {undoPrefix} Decrements Hits");
                phaseData.decrementsMaxHits = value;
                MarkDirty();
            });

            // Hitbox type selector
            string currentTypeName = phaseData.hitbox == null ? "None" : phaseData.hitbox.HitboxTypeName;
            var typeChoices = new System.Collections.Generic.List<string> { "None", "Normal", "Grab" };
            int currentIndex = phaseData.hitbox == null ? 0 : (phaseData.hitbox is NormalHitbox ? 1 : (phaseData.hitbox is GrabHitbox ? 2 : 0));

            VisualElement typeRow = new VisualElement();
            typeRow.AddToClassList("card-field-row");
            DropdownField typeDropdown = new DropdownField("Hitbox Type", typeChoices, currentIndex);
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                RecordUndo($"Change {undoPrefix} Hitbox Type");
                switch (typeDropdown.index)
                {
                    case 0: phaseData.hitbox = null; break;
                    case 1: phaseData.hitbox = new NormalHitbox(); break;
                    case 2: phaseData.hitbox = new GrabHitbox(); break;
                }
                MarkDirtyAndRebuild();
            });
            typeRow.Add(typeDropdown);
            parent.Add(typeRow);

            // Render fields for the active hitbox type
            if (phaseData.hitbox != null)
            {
                AddSeparator(parent, $"{phaseData.hitbox.HitboxTypeName} Hitbox Properties");

                AddIntField(parent, "Start Offset", phaseData.hitbox.startFrameOffset, value =>
                {
                    RecordUndo($"Edit {undoPrefix} Start Offset");
                    phaseData.hitbox.startFrameOffset = value;
                    MarkDirty();
                });
                AddIntField(parent, "Duration", phaseData.hitbox.activeFrames, value =>
                {
                    RecordUndo($"Edit {undoPrefix} Duration");
                    phaseData.hitbox.activeFrames = value;
                    MarkDirty();
                });
                AddEnumField(parent, "Damage Type", phaseData.hitbox.damageType, value =>
                {
                    RecordUndo($"Edit {undoPrefix} Damage Type");
                    phaseData.hitbox.damageType = (DamageType)value;
                    MarkDirty();
                });
                AddColorField(parent, "Debug Color", phaseData.hitbox.debugColor, value =>
                {
                    RecordUndo($"Edit {undoPrefix} Debug Color");
                    phaseData.hitbox.debugColor = value;
                    MarkDirty();
                });

                if (phaseData.hitbox is NormalHitbox normalHitbox)
                {
                    BuildNormalHitboxFields(parent, normalHitbox);
                }
                else if (phaseData.hitbox is GrabHitbox grabHitbox)
                {
                    BuildGrabHitboxFields(parent, grabHitbox);
                }
            }
        }

        private void BuildFeedbackDefinitionCard(VisualElement parent, ProjectileFeedbackDefinition feedback, string undoPrefix)
        {
            if (feedback == null) return;

            AddEnumField(parent, "VFX Type", feedback.vfxType, value =>
            {
                RecordUndo($"Edit {undoPrefix} VFX Type");
                feedback.vfxType = (Project.Combat.VFXType)value;
                MarkDirty();
            });
            AddObjectField(parent, "VFX Clip (Override)", feedback.vfxClip, value =>
            {
                RecordUndo($"Edit {undoPrefix} VFX Clip");
                feedback.vfxClip = value as AnimationClip;
                MarkDirty();
            });
            AddObjectField(parent, "Audio", feedback.audio, value =>
            {
                RecordUndo($"Edit {undoPrefix} Audio");
                feedback.audio = value as AudioClip;
                MarkDirty();
            });
            AddVector2Field(parent, "Offset", feedback.offset, value =>
            {
                RecordUndo($"Edit {undoPrefix} Offset");
                feedback.offset = value;
                MarkDirty();
            });
        }

        private void BuildAttackStepCards(AttackStepData step)
        {
            if (step == null)
            {
                return;
            }

            BuildFoldoutCard("Phase Configuration", "phase_config", new Color(0f, 0.83f, 1f), body =>
            {
                AddTextField(body, "Step Name", step.stepName, value =>
                {
                    RecordUndo("Edit Step Name");
                    step.stepName = value;
                    MarkDirtyAndRebuild();
                });

                if (step.animationClip != null)
                {
                    int total = Mathf.RoundToInt(step.animationClip.length * FrameDataConstants.LOGIC_FPS);
                    AddPhaseSlider(body, step.startupFrames, step.activeFrames, step.recoveryFrames, total, (startup, active, recovery) =>
                    {
                        RecordUndo("Adjust Phases");
                        step.startupFrames = startup;
                        step.activeFrames = active;
                        step.recoveryFrames = recovery;
                        MarkDirty();
                    });

                    // Synchronize Clip length ratio
                    var syncBtn = new Button(() => 
                    {
                        RecordUndo("Sync Logic frames to Clip length");
                        int newTotal = Mathf.RoundToInt(step.animationClip.length * FrameDataConstants.LOGIC_FPS);
                        int oldTotal = Mathf.Max(1, step.startupFrames + step.activeFrames + step.recoveryFrames); // avoid divide by zero
                        
                        float scale = (float)newTotal / oldTotal;
                        
                        // Perform deep rescale on all sub-data (Hitboxes, VFX, Motion, etc.)
                        step.Rescale(scale);

                        // Final adjustment to ensure absolute match with clip length
                        // Rounding errors might leave a small gap; we dump it into recovery.
                        int currentTotal = step.startupFrames + step.activeFrames + step.recoveryFrames;
                        if (currentTotal != newTotal)
                        {
                            step.recoveryFrames = Mathf.Max(0, step.recoveryFrames + (newTotal - currentTotal));
                        }
                        
                        MarkDirtyAndRebuild();
                    });
                    syncBtn.text = $"Sync Logic frames to Game Logic ({FrameDataConstants.LOGIC_FPS}fps)";
                    syncBtn.style.marginTop = 4;
                    body.Add(syncBtn);

                    AddToggleField(body, "Loop Active Phase", step.loopActivePhase, value =>
                    {
                        RecordUndo("Edit Loop Active Phase");
                        step.loopActivePhase = value;
                        MarkDirty();
                    });
                }
                else
                {
                    body.Add(new HelpBox("Assign an Animation Clip to enable sync slider.", HelpBoxMessageType.Warning));
                    AddIntField(body, "Startup", step.startupFrames, value =>
                    {
                        RecordUndo("Edit Startup");
                        step.startupFrames = value;
                        MarkDirty();
                    });
                    AddIntField(body, "Active", step.activeFrames, value =>
                    {
                        RecordUndo("Edit Active");
                        step.activeFrames = value;
                        MarkDirty();
                    });
                    AddIntField(body, "Recovery", step.recoveryFrames, value =>
                    {
                        RecordUndo("Edit Recovery");
                        step.recoveryFrames = value;
                        MarkDirty();
                    });
                }

                AddIntField(body, "Cancel Frame", step.cancelWindowStartFrame, value =>
                {
                    RecordUndo("Edit Cancel Frame");
                    step.cancelWindowStartFrame = value;
                    MarkDirty();
                });

                AddSeparator(body, "Behavior Flags");
                AddToggleField(body, "Auto Chain to Next", step.autoChainToNext, value =>
                {
                    RecordUndo("Edit Auto Chain");
                    step.autoChainToNext = value;
                    MarkDirty();
                });
                AddToggleField(body, "Lock Air Position", step.lockAirPosition, value =>
                {
                    RecordUndo("Edit Lock Air Position");
                    step.lockAirPosition = value;
                    MarkDirty();
                });
            });

            VisualElement componentList = new VisualElement();
            componentList.AddToClassList("component-card-list");
            container.Add(componentList);

            List<string> orderedComponents = step.GetOrderedComponentKeys();
            for (int i = 0; i < orderedComponents.Count; i++)
            {
                int reorderIndex = i;
                string componentKey = orderedComponents[i];

                switch (componentKey)
                {
                    case AttackStepData.MotionComponentKey when step.motionData != null:
                        BuildFoldoutCard("Motion Frame Data", "motion_data", step.motionData.TrackColor, body =>
                        {
                            BuildMotionCard(body, step.motionData);
                        }, () =>
                        {
                            RecordUndo("Remove Motion");
                            step.motionData = null;
                            MarkDirtyAndRebuild();
                        }, false, componentList, newIndex =>
                        {
                            if (newIndex == reorderIndex) return;
                            RecordUndo("Reorder Components");
                            step.MoveComponentKey(AttackStepData.MotionComponentKey, newIndex);
                            MarkDirtyAndRebuild();
                        }, reorderIndex, orderedComponents.Count);
                        break;

                    case AttackStepData.VfxComponentKey when step.vfxData != null:
                        BuildFoldoutCard("VFX Frame Data", "vfx_data", step.vfxData.TrackColor, body =>
                        {
                            BuildVFXCard(body, step.vfxData);
                        }, () =>
                        {
                            RecordUndo("Remove VFX");
                            step.vfxData = null;
                            MarkDirtyAndRebuild();
                        }, false, componentList, newIndex =>
                        {
                            if (newIndex == reorderIndex) return;
                            RecordUndo("Reorder Components");
                            step.MoveComponentKey(AttackStepData.VfxComponentKey, newIndex);
                            MarkDirtyAndRebuild();
                        }, reorderIndex, orderedComponents.Count);
                        break;

                    case AttackStepData.AudioComponentKey when step.audioData != null:
                        BuildFoldoutCard("Audio Frame Data", "audio_data", step.audioData.TrackColor, body =>
                        {
                            BuildAudioCard(body, step.audioData);
                        }, () =>
                        {
                            RecordUndo("Remove Audio");
                            step.audioData = null;
                            MarkDirtyAndRebuild();
                        }, false, componentList, newIndex =>
                        {
                            if (newIndex == reorderIndex) return;
                            RecordUndo("Reorder Components");
                            step.MoveComponentKey(AttackStepData.AudioComponentKey, newIndex);
                            MarkDirtyAndRebuild();
                        }, reorderIndex, orderedComponents.Count);
                        break;

                    case AttackStepData.HitboxComponentKey when step.hitData != null:
                        BuildFoldoutCard("Hitbox Data", "hit_data", step.hitData.TrackColor, body =>
                        {
                            BuildHitDataCard(body, step.hitData);
                        }, () =>
                        {
                            RecordUndo("Remove Hit Data");
                            step.hitData = null;
                            MarkDirtyAndRebuild();
                        }, false, componentList, newIndex =>
                        {
                            if (newIndex == reorderIndex) return;
                            RecordUndo("Reorder Components");
                            step.MoveComponentKey(AttackStepData.HitboxComponentKey, newIndex);
                            MarkDirtyAndRebuild();
                        }, reorderIndex, orderedComponents.Count);
                        break;

                    case AttackStepData.EventComponentKey when step.eventData != null:
                        BuildFoldoutCard("Action Event Data", "event_data", step.eventData.TrackColor, body =>
                        {
                            BuildActionEventCard(body, step.eventData);
                        }, () =>
                        {
                            RecordUndo("Remove Action Events");
                            step.eventData = null;
                            MarkDirtyAndRebuild();
                        }, false, componentList, newIndex =>
                        {
                            if (newIndex == reorderIndex) return;
                            RecordUndo("Reorder Components");
                            step.MoveComponentKey(AttackStepData.EventComponentKey, newIndex);
                            MarkDirtyAndRebuild();
                        }, reorderIndex, orderedComponents.Count);
                        break;
                }
            }



            BuildAddComponentButton(step);
        }

        private void BuildCombatActionCards(CombatAction action)
        {
            if (action == null) return;

            BuildFoldoutCard($"{action.Category} Setup", "action_setup", new Color(0.8f, 0.2f, 0.8f), body =>
            {
                AddTextField(body, "Action Name", action.actionName, value => {
                    RecordUndo("Edit Action Name");
                    action.actionName = value;
                    MarkDirtyAndRebuild();
                });

                AddEnumField(body, "Context", action.context, value => {
                    RecordUndo("Edit Context");
                    action.context = (CombatActionContext)value;
                    MarkDirtyAndRebuild();
                });

                AddIntField(body, "Priority", action.priority, value => {
                    RecordUndo("Edit Priority");
                    action.priority = value;
                    MarkDirty();
                });

                AddSeparator(body, "Advanced Conditions");
                BuildConditionList(body, action.conditions, $"action_conditions_{action.actionName}", "Action");

                if (action is SkillAction skill)
                {
                    AddSeparator(body, "Resource Cost");
                    AddFloatField(body, "Cooldown", skill.cooldown, value => {
                        RecordUndo("Edit Cooldown");
                        skill.cooldown = value;
                        MarkDirty();
                    });
                    AddFloatField(body, "Energy Cost", skill.energyCost, value => {
                        RecordUndo("Edit Energy Cost");
                        skill.energyCost = value;
                        MarkDirty();
                    });
                }

                if (action is UltimateAction ultimate)
                {
                    AddSeparator(body, "Activation FX (Plays during black screen)");
                    AddEnumField(body, "VFX Type", ultimate.activationVFXType, value => {
                        RecordUndo("Edit Ultimate VFX Type");
                        ultimate.activationVFXType = (Project.Combat.VFXType)value;
                        MarkDirty();
                    });
                    AddObjectField(body, "VFX Prefab (Override)", ultimate.activationVFXPrefab, value => {
                        RecordUndo("Edit Ultimate VFX Prefab");
                        ultimate.activationVFXPrefab = value;
                        MarkDirty();
                    });
                    AddVector3Field(body, "VFX Offset", ultimate.activationVFXOffset, value => {
                        RecordUndo("Edit Ultimate VFX Offset");
                        ultimate.activationVFXOffset = value;
                        MarkDirty();
                    });

                    AddSeparator(body, "Activation Audio");
                    AddObjectField(body, "Activation Voice", ultimate.activationVoice, value => {
                        RecordUndo("Edit Voice");
                        ultimate.activationVoice = value;
                        MarkDirty();
                    });
                    AddObjectField(body, "Activation SFX", ultimate.activationSFX, value => {
                        RecordUndo("Edit SFX");
                        ultimate.activationSFX = value;
                        MarkDirty();
                    });
                    AddFloatField(body, "Voice Delay", ultimate.voiceDelay, value => {
                        RecordUndo("Edit Delay");
                        ultimate.voiceDelay = value;
                        MarkDirty();
                    });
                }

                if (action is StandaloneAction sa)
                {
                    AddSeparator(body, "Plunge Behavior");
                    AddToggleField(body, "Use Plunge Flow", sa.isPlungeAction, value => {
                        RecordUndo("Toggle Plunge Flow");
                        sa.isPlungeAction = value;
                        MarkDirty();
                    });

                    AddFloatField(body, "Dive Downward Speed", sa.plungeDownwardSpeed, value => {
                        RecordUndo("Edit Plunge Dive Speed");
                        sa.plungeDownwardSpeed = value;
                        MarkDirty();
                    });

                    AddFloatField(body, "Charge Steering Strength", sa.plungeChargeSteeringStrength, value => {
                        RecordUndo("Edit Plunge Steering Strength");
                        sa.plungeChargeSteeringStrength = value;
                        MarkDirty();
                    });

                    AddFloatField(body, "Charge Max Horizontal Redirect", sa.plungeChargeMaxHorizontalRedirect, value => {
                        RecordUndo("Edit Plunge Redirect");
                        sa.plungeChargeMaxHorizontalRedirect = value;
                        MarkDirty();
                    });

                    AddToggleField(body, "Update Facing During Charge", sa.updateFacingDuringPlungeCharge, value => {
                        RecordUndo("Toggle Plunge Facing Update");
                        sa.updateFacingDuringPlungeCharge = value;
                        MarkDirty();
                    });

                    AddTextField(body, "Legacy Dive Anim Fallback", sa.plungeFallAnimState, value => {
                        RecordUndo("Edit Legacy Plunge Anim");
                        sa.plungeFallAnimState = value;
                        MarkDirty();
                    });

                    AddSeparator(body, "Tree Authoring");
                    if (sa.comboTree == null || sa.comboTree.rootNode == null)
                    {
                        body.Add(new HelpBox("Standalone actions now author their flow through comboTree. Create or migrate the root node here, then add the dive step as Default Next and the impact step through a landing trigger.", HelpBoxMessageType.Info));

                        var createTreeButton = new Button(() => {
                            RecordUndo("Create Standalone Combo Tree");
                            sa.comboTree ??= new ComboTree { treeName = sa.actionName };
                            sa.comboTree.rootNode = new ComboNode
                            {
                                stepData = sa.stepData ?? new AttackStepData
                                {
                                    stepName = sa.isPlungeAction ? "Plunge Charge" : "Context Action Entry"
                                },
                                defaultNext = sa.followUpNode
                            };
                            MarkDirtyAndRebuild();
                        })
                        { text = sa.stepData != null || sa.followUpNode != null ? "Migrate Legacy Step Into Combo Tree" : "Create Combo Tree Root" };
                        body.Add(createTreeButton);
                    }
                }
            });
        }

        private CombatAction GetCurrentAction()
        {
            if (!(targetObject is CharacterObject character) || character.actionResolverTable == null || currentNode == null) return null;
            string cat = currentNode.Category;
            if (string.IsNullOrEmpty(cat)) return null;

            int idx = -1;
            if (cat.StartsWith("Combo_") && int.TryParse(cat.Substring(6), out idx) && idx < character.actionResolverTable.comboActions.Count)
                return character.actionResolverTable.comboActions[idx];
            if (cat.StartsWith("Skill_") && int.TryParse(cat.Substring(6), out idx) && idx < character.actionResolverTable.skillActions.Count)
                return character.actionResolverTable.skillActions[idx];
            if (cat.StartsWith("Ultimate_") && int.TryParse(cat.Substring(9), out idx) && idx < character.actionResolverTable.ultimateActions.Count)
                return character.actionResolverTable.ultimateActions[idx];
            if (cat.StartsWith("Standalone_") && int.TryParse(cat.Substring(11), out idx) && idx < character.actionResolverTable.standaloneActions.Count)
                return character.actionResolverTable.standaloneActions[idx];
            return null;
        }

        private List<ComboNode> GetAllNodesInAction(CombatAction action)
        {
            var list = new List<ComboNode>();
            if (action == null || action.comboTree == null || action.comboTree.rootNode == null) return list;

            var visited = new HashSet<ComboNode>();
            void Walk(ComboNode n) {
                if (n == null || visited.Contains(n)) return;
                visited.Add(n);
                list.Add(n);
                Walk(n.defaultNext);
                Walk(n.onGrabSuccessNode);
                if (n.routes != null) {
                    foreach(var r in n.routes) Walk(r.targetNode);
                }
                if (n.stepData != null && n.stepData.triggerWindows != null) {
                    foreach(var trigger in n.stepData.triggerWindows) Walk(trigger.targetNode);
                }
            }
            Walk(action.comboTree.rootNode);
            return list;
        }

        private void BuildComboNodeCards(ComboNode node)
        {
            if (node == null) return;

            // 1. Attack Step Card (Main Properties)
            BuildAttackStepCards(node.stepData);

            // 2. Routing Card (New system)
            BuildFoldoutCard("Combo Routing", "combo_routing", new Color(0.9f, 0.4f, 1f), body =>
            {
                CombatAction currentAction = GetCurrentAction();
                List<ComboNode> allNodes = GetAllNodesInAction(currentAction);
                List<string> nodeNames = new List<string> { "NONE (End)" };
                foreach (var n in allNodes) nodeNames.Add(n.stepData?.stepName ?? "Unnamed");

                // Default Next
                AddSeparator(body, "Default Transition (Linear Phase)");
                
                VisualElement defaultNextRow = new VisualElement();
                defaultNextRow.AddToClassList("card-field-row");
                
                int defaultNextIndex = node.defaultNext == null ? 0 : allNodes.IndexOf(node.defaultNext) + 1;
                if (defaultNextIndex == 0 && node.defaultNext != null) {
                    nodeNames.Add(node.defaultNext.stepData?.stepName ?? "Unknown");
                    defaultNextIndex = nodeNames.Count - 1;
                }

                DropdownField defaultNextDropdown = new DropdownField("Next Node", nodeNames, defaultNextIndex);
                defaultNextDropdown.RegisterValueChangedCallback(evt => {
                    RecordUndo("Change Default Next");
                    int newIdx = defaultNextDropdown.index;
                    if (newIdx == 0) {
                        node.defaultNext = null;
                    } else if (newIdx - 1 < allNodes.Count) {
                        node.defaultNext = allNodes[newIdx - 1];
                    }
                    MarkDirtyAndRebuild();
                });
                defaultNextRow.Add(defaultNextDropdown);

                if (node.defaultNext != null)
                {
                    var goBtn = new Button(() => {
                        treeController.SelectNodeByData(node.defaultNext);
                    }) { text = "->" };
                    defaultNextRow.Add(goBtn);
                }
                else
                {
                    var createNextBtn = new Button(() => {
                        RecordUndo("Create Default Next");
                        node.defaultNext = new ComboNode { stepData = new AttackStepData { stepName = "New Linear Step" } };
                        MarkDirtyAndRebuild();
                    }) { text = "+ Create Target" };
                    defaultNextRow.Add(createNextBtn);
                }
                body.Add(defaultNextRow);

                // --- On Grab Success Next ---
                bool hasGrab = false;
                if (node.stepData?.hitData?.hitboxes != null) {
                    foreach (var hb in node.stepData.hitData.hitboxes) {
                        if (hb is GrabHitbox) { hasGrab = true; break; }
                    }
                }

                if (hasGrab)
                {
                    AddSeparator(body, "Grab Routing");
                    VisualElement grabNextRow = new VisualElement();
                    grabNextRow.AddToClassList("card-field-row");

                    int currentGrabIdx = node.onGrabSuccessNode == null ? 0 : allNodes.IndexOf(node.onGrabSuccessNode) + 1;
                    DropdownField grabNextDropdown = new DropdownField("On Grab Success Target", nodeNames, currentGrabIdx);
                    grabNextDropdown.RegisterValueChangedCallback(evt => {
                        RecordUndo("Change Grab Success Next");
                        int newIdx = grabNextDropdown.index;
                        if (newIdx == 0) {
                            node.onGrabSuccessNode = null;
                        } else if (newIdx - 1 < allNodes.Count) {
                            node.onGrabSuccessNode = allNodes[newIdx - 1];
                        }
                        MarkDirtyAndRebuild();
                    });
                    grabNextRow.Add(grabNextDropdown);

                    if (node.onGrabSuccessNode != null)
                    {
                        var goBtn = new Button(() => {
                            treeController.SelectNodeByData(node.onGrabSuccessNode);
                        }) { text = "->" };
                        grabNextRow.Add(goBtn);
                    }
                    else
                    {
                        var createNextBtn = new Button(() => {
                            RecordUndo("Create Grab Success Next");
                            node.onGrabSuccessNode = new ComboNode { stepData = new AttackStepData { stepName = "Grab Followup" } };
                            MarkDirtyAndRebuild();
                        }) { text = "+ Create Target" };
                        grabNextRow.Add(createNextBtn);
                    }
                    body.Add(grabNextRow);
                }



                // Routes List
                AddSeparator(body, "Conditional Routes");
                var routeHelp = new Label("Determined at the moment of next input (standard transitions).") { style = { opacity = 0.6f, fontSize = 10, unityFontStyleAndWeight = FontStyle.Italic, marginBottom = 5 } };
                body.Add(routeHelp);

                if (node.routes == null) node.routes = new List<ComboRoute>();

                for (int i = 0; i < node.routes.Count; i++)
                {
                    int index = i;
                    var route = node.routes[i];
                    
                    VisualElement routeBox = new VisualElement();
                    routeBox.AddToClassList("route-box");
                    routeBox.style.backgroundColor = new Color(0, 0, 0, 0.2f);
                    routeBox.style.paddingLeft = 5;
                    routeBox.style.marginBottom = 5;

                    AddIntField(routeBox, "Priority", route.priority, val => {
                        RecordUndo("Change Route Priority");
                        route.priority = val;
                        MarkDirty();
                    });

                    AddSeparator(routeBox, "Advanced Conditions");
                    BuildConditionList(routeBox, route.conditions, $"route_conditions_{index}", "Route");

                    VisualElement targetRow = new VisualElement();
                    targetRow.AddToClassList("card-field-row");
                    
                    int currentIndex = route.targetNode == null ? 0 : allNodes.IndexOf(route.targetNode) + 1;
                    if (currentIndex == 0 && route.targetNode != null) {
                        nodeNames.Add(route.targetNode.stepData?.stepName ?? "Unknown");
                        currentIndex = nodeNames.Count - 1;
                    }

                    DropdownField targetDropdown = new DropdownField("Target Node", nodeNames, currentIndex);
                    targetDropdown.RegisterValueChangedCallback(evt => {
                        RecordUndo("Change Route Target");
                        int newIdx = targetDropdown.index;
                        if (newIdx == 0) {
                            route.targetNode = null;
                        } else if (newIdx - 1 < allNodes.Count) {
                            route.targetNode = allNodes[newIdx - 1];
                        }
                        MarkDirtyAndRebuild();
                    });
                    
                    targetRow.Add(targetDropdown);

                    if (route.targetNode != null)
                    {
                        var goBtn = new Button(() => {
                            treeController.SelectNodeByData(route.targetNode);
                        }) { text = "->" };
                        targetRow.Add(goBtn);
                    }
                    else
                    {
                        var createTargetBtn = new Button(() => {
                            RecordUndo("Create Branch Target");
                            route.targetNode = new ComboNode { stepData = new AttackStepData { stepName = "New Branch Step" } };
                            MarkDirtyAndRebuild();
                        }) { text = "+ Create Target" };
                        targetRow.Add(createTargetBtn);
                    }
                    
                    routeBox.Add(targetRow);

                    var removeBtn = new Button(() => {
                        RecordUndo("Remove Route");
                        node.routes.RemoveAt(index);
                        MarkDirtyAndRebuild();
                    }) { text = "Remove Route" };
                    routeBox.Add(removeBtn);

                    body.Add(routeBox);
                }

                var addRouteBtn = new Button(() => {
                    RecordUndo("Add Route");
                    node.routes.Add(new ComboRoute());
                    MarkDirtyAndRebuild();
                }) { text = "+ Add Branching Route" };
                body.Add(addRouteBtn);

                // --- Automatic Transitions (Implicit Triggers) ---
                AddSeparator(body, "Automatic Transitions (Interrupts)");
                var triggerHelp = new Label("Triggered immediately during active phase if conditions are met.") { style = { opacity = 0.6f, fontSize = 10, unityFontStyleAndWeight = FontStyle.Italic, marginBottom = 5 } };
                body.Add(triggerHelp);

                if (node.stepData.triggerWindows == null) node.stepData.triggerWindows = new List<TriggerWindow>();

                for (int i = 0; i < node.stepData.triggerWindows.Count; i++)
                {
                    int index = i;
                    var trigger = node.stepData.triggerWindows[i];

                    VisualElement triggerBox = new VisualElement();
                    triggerBox.AddToClassList("route-box");
                    triggerBox.style.backgroundColor = new Color(0.2f, 0.2f, 0.1f, 0.3f); // Slightly different tint
                    triggerBox.style.paddingLeft = 5;
                    triggerBox.style.paddingBottom = 5;
                    triggerBox.style.marginBottom = 10;

                    AddIntField(triggerBox, "Priority", trigger.priority, val => {
                        RecordUndo("Change Trigger Priority");
                        trigger.priority = val;
                        MarkDirty();
                    });

                    AddSeparator(triggerBox, "Conditions (Checked every active frame)");
                    BuildConditionList(triggerBox, trigger.triggerConditions, $"trigger_conditions_{index}", "Trigger");

                    VisualElement targetRow = new VisualElement();
                    targetRow.AddToClassList("card-field-row");
                    
                    int currentIndex = trigger.targetNode == null ? 0 : allNodes.IndexOf(trigger.targetNode) + 1;
                    DropdownField targetDropdown = new DropdownField("Target", nodeNames, currentIndex);
                    targetDropdown.RegisterValueChangedCallback(evt => {
                        RecordUndo("Change Trigger Target");
                        int newIdx = targetDropdown.index;
                        if (newIdx == 0) trigger.targetNode = null;
                        else if (newIdx - 1 < allNodes.Count) trigger.targetNode = allNodes[newIdx - 1];
                        MarkDirtyAndRebuild();
                    });
                    targetRow.Add(targetDropdown);
                    
                    if (trigger.targetNode != null)
                    {
                        var goBtn = new Button(() => treeController.SelectNodeByData(trigger.targetNode)) { text = "->" };
                        targetRow.Add(goBtn);
                    }
                    else
                    {
                        var createTargetBtn = new Button(() => {
                            RecordUndo("Create Trigger Target");
                            trigger.targetNode = new ComboNode { stepData = new AttackStepData { stepName = "New Trigger Step" } };
                            MarkDirtyAndRebuild();
                        }) { text = "+ Create Target" };
                        targetRow.Add(createTargetBtn);
                    }
                    triggerBox.Add(targetRow);

                    var removeBtn = new Button(() => {
                        RecordUndo("Remove Trigger");
                        node.stepData.triggerWindows.RemoveAt(index);
                        MarkDirtyAndRebuild();
                    }) { text = "Remove Automatic Transition" };
                    triggerBox.Add(removeBtn);

                    body.Add(triggerBox);
                }

                var addAutoBtn = new Button(() => {
                    RecordUndo("Add Automatic Transition");
                    // Hardcode window to full active phase implicitly via logic, 
                    // but data-wise we can just set it to 0 / -1 for now.
                    node.stepData.triggerWindows.Add(new TriggerWindow { startFrame = 0, endFrame = -1 });
                    MarkDirtyAndRebuild();
                }) { text = "+ Add Automatic Transition" };
                body.Add(addAutoBtn);
            });
        }

        private void BuildResolveTableCards(ActionResolverTable table)
        {
            if (table == null) return;

            BuildFoldoutCard("Global Resolver Tools", "resolver_tools", new Color(0.2f, 0.7f, 0.4f), body =>
            {
                body.Add(new HelpBox("Data migration tools are no longer available. Combat actions must be authored using advanced Condition lists inside the combo trees natively.", HelpBoxMessageType.Info));
            });
        }

        private void BuildMotionCard(VisualElement body, MotionFrameData motion)
        {
            if (motion.keyframes == null) motion.keyframes = new List<MotionKeyframe>();

            VisualElement itemList = new VisualElement();
            body.Add(itemList);

            for (int i = 0; i < motion.keyframes.Count; i++)
            {
                int idx = i;
                MotionKeyframe keyframe = motion.keyframes[i];

                BuildFoldoutCard($"Keyframe #{idx + 1} - Start Frame {keyframe.startFrame}", $"motion_kf_{idx}", new Color(0.18f, 0.72f, 0.38f, 0.5f), inner =>
                {
                    AddIntField(inner, "Start Frame", keyframe.startFrame, value =>
                    {
                        RecordUndo("Edit Motion Start Frame");
                        keyframe.startFrame = value;
                        MarkDirty();
                    });
                    AddIntField(inner, "End Frame", keyframe.endFrame, value =>
                    {
                        RecordUndo("Edit Motion End Frame");
                        keyframe.endFrame = value;
                        MarkDirty();
                    });
                    AddEnumField(inner, "Task Type", keyframe.taskType, value =>
                    {
                        RecordUndo("Edit Motion Task Type");
                        keyframe.taskType = (MotionTaskType)value;
                        MarkDirty();
                    });
                    AddEnumField(inner, "Easing", keyframe.easing, value =>
                    {
                        RecordUndo("Edit Motion Easing");
                        keyframe.easing = (LeanTweenType)value;
                        MarkDirty();
                    });
                    AddVector3Field(inner, "Position", keyframe.position, value =>
                    {
                        RecordUndo("Edit Motion Position");
                        keyframe.position = value;
                        MarkDirty();
                    });
                    AddVector3Field(inner, "Rotation", keyframe.rotation, value =>
                    {
                        RecordUndo("Edit Motion Rotation");
                        keyframe.rotation = value;
                        MarkDirty();
                    });
                    AddVector3Field(inner, "Scale", keyframe.scale, value =>
                    {
                        RecordUndo("Edit Motion Scale");
                        keyframe.scale = value;
                        MarkDirty();
                    });
                }, () =>
                {
                    RecordUndo("Delete Motion Keyframe");
                    motion.keyframes.RemoveAt(idx);
                    MarkDirtyAndRebuild();
                }, isSubCard: true, parent: itemList, onReorder: newIndex =>
                {
                    ReorderListItem(motion.keyframes, idx, newIndex, "Reorder Motion Keyframe");
                }, reorderIndex: idx, reorderCount: motion.keyframes.Count);
            }

            Button addButton = new Button(() =>
            {
                RecordUndo("Add Motion Keyframe");
                motion.keyframes.Add(new MotionKeyframe());
                MarkDirtyAndRebuild();
            })
            {
                text = "Add Keyframe"
            };
            addButton.AddToClassList("component-add-entry-btn");
            body.Add(addButton);
        }

        private void BuildVFXCard(VisualElement body, VFXFrameData vfx)
        {
            if (vfx.entries == null) vfx.entries = new List<VFXEntry>();

            VisualElement itemList = new VisualElement();
            body.Add(itemList);

            for (int i = 0; i < vfx.entries.Count; i++)
            {
                int idx = i;
                VFXEntry entry = vfx.entries[i];

                BuildFoldoutCard($"VFX #{idx + 1}: {entry.label}", $"vfx_entry_{idx}", entry.trackColor, inner =>
                {
                    AddTextField(inner, "Label", entry.label, value =>
                    {
                        RecordUndo("Edit VFX Label");
                        entry.label = value;
                        MarkDirty();
                    });
                    AddEnumField(inner, "VFX Type", entry.vfxType, value =>
                    {
                        RecordUndo("Edit VFX Type");
                        entry.vfxType = (Project.Combat.VFXType)value;
                        MarkDirty();
                    });
                    AddObjectField(inner, "VFX Prefab (Override)", entry.vfxPrefab, value =>
                    {
                        RecordUndo("Edit VFX Prefab");
                        entry.vfxPrefab = value;
                        MarkDirty();
                    });
                    AddIntField(inner, "Trigger Frame", entry.triggerFrame, value =>
                    {
                        RecordUndo("Edit VFX Frame");
                        entry.triggerFrame = value;
                        MarkDirty();
                    });
                    AddIntField(inner, "Duration", entry.duration, value =>
                    {
                        RecordUndo("Edit VFX Duration");
                        entry.duration = value;
                        MarkDirty();
                    });
                    AddColorField(inner, "Track Color", entry.trackColor, value =>
                    {
                        RecordUndo("Edit VFX Color");
                        entry.trackColor = value;
                        MarkDirty();
                    });
                    AddToggleField(inner, "Loop During Active", entry.loopDuringActive, value =>
                    {
                        RecordUndo("Toggle VFX Looping");
                        entry.loopDuringActive = value;
                        MarkDirty();
                    });

                    AddSeparator(inner, "Transform Keys");
                    if (entry.transformKeys == null) entry.transformKeys = new List<VFXTransformKey>();

                    for (int keyIndex = 0; keyIndex < entry.transformKeys.Count; keyIndex++)
                    {
                        int transformKeyIndex = keyIndex;
                        VFXTransformKey transformKey = entry.transformKeys[keyIndex];
                        AddIntField(inner, $"Key {transformKeyIndex + 1} Frame", transformKey.frame, value =>
                        {
                            RecordUndo("Edit VFX Transform Key Frame");
                            transformKey.frame = value;
                            MarkDirty();
                        });
                        AddVector3Field(inner, $"Key {transformKeyIndex + 1} Position", transformKey.position, value =>
                        {
                            RecordUndo("Edit VFX Transform Key Position");
                            transformKey.position = value;
                            MarkDirty();
                        });
                    }

                    Button addTransformKeyButton = new Button(() =>
                    {
                        RecordUndo("Add VFX Transform Key");
                        entry.transformKeys.Add(new VFXTransformKey());
                        MarkDirtyAndRebuild();
                    })
                    {
                        text = "Add Transform Key"
                    };
                    addTransformKeyButton.AddToClassList("component-add-entry-btn");
                    inner.Add(addTransformKeyButton);
                }, () =>
                {
                    RecordUndo("Delete VFX Entry");
                    vfx.entries.RemoveAt(idx);
                    MarkDirtyAndRebuild();
                }, isSubCard: true, parent: itemList, onReorder: newIndex =>
                {
                    ReorderListItem(vfx.entries, idx, newIndex, "Reorder VFX Entry");
                }, reorderIndex: idx, reorderCount: vfx.entries.Count);
            }

            Button addButton = new Button(() =>
            {
                RecordUndo("Add VFX Entry");
                vfx.entries.Add(new VFXEntry());
                MarkDirtyAndRebuild();
            })
            {
                text = "Add VFX Entry"
            };
            addButton.AddToClassList("component-add-entry-btn");
            body.Add(addButton);
        }

        private void BuildAudioCard(VisualElement body, AudioFrameData audio)
        {
            if (audio.entries == null) audio.entries = new List<AudioEntry>();

            VisualElement itemList = new VisualElement();
            body.Add(itemList);

            for (int i = 0; i < audio.entries.Count; i++)
            {
                int idx = i;
                AudioEntry entry = audio.entries[i];

                BuildFoldoutCard($"Audio #{idx + 1}: {entry.label}", $"audio_entry_{idx}", entry.trackColor, inner =>
                {
                    AddTextField(inner, "Label", entry.label, value =>
                    {
                        RecordUndo("Edit Audio Label");
                        entry.label = value;
                        MarkDirty();
                    });
                    AddObjectField(inner, "Audio Clip", entry.clip, value =>
                    {
                        RecordUndo("Edit Audio Clip");
                        entry.clip = value;
                        MarkDirty();
                    });
                    AddIntField(inner, "Trigger Frame", entry.triggerFrame, value =>
                    {
                        RecordUndo("Edit Audio Frame");
                        entry.triggerFrame = value;
                        MarkDirty();
                    });
                    AddIntField(inner, "Duration", entry.duration, value =>
                    {
                        RecordUndo("Edit Audio Duration");
                        entry.duration = value;
                        MarkDirty();
                    });
                    AddEnumField(inner, "Channel", entry.channel, value =>
                    {
                        RecordUndo("Edit Audio Channel");
                        entry.channel = (AudioChannel)value;
                        MarkDirty();
                    });
                    AddFloatSlider(inner, "Volume", entry.volume, 0f, 1f, value =>
                    {
                        RecordUndo("Edit Audio Volume");
                        entry.volume = value;
                        MarkDirty();
                    });
                    AddFloatSlider(inner, "Pitch", entry.pitchVariation, 0.1f, 3f, value =>
                    {
                        RecordUndo("Edit Audio Pitch");
                        entry.pitchVariation = value;
                        MarkDirty();
                    });
                    AddColorField(inner, "Track Color", entry.trackColor, value =>
                    {
                        RecordUndo("Edit Audio Color");
                        entry.trackColor = value;
                        MarkDirty();
                    });
                }, () =>
                {
                    RecordUndo("Delete Audio Entry");
                    audio.entries.RemoveAt(idx);
                    MarkDirtyAndRebuild();
                }, isSubCard: true, parent: itemList, onReorder: newIndex =>
                {
                    ReorderListItem(audio.entries, idx, newIndex, "Reorder Audio Entry");
                }, reorderIndex: idx, reorderCount: audio.entries.Count);
            }

            Button addButton = new Button(() =>
            {
                RecordUndo("Add Audio Entry");
                audio.entries.Add(new AudioEntry());
                MarkDirtyAndRebuild();
            })
            {
                text = "Add Audio Entry"
            };
            addButton.AddToClassList("component-add-entry-btn");
            body.Add(addButton);
        }

        private void BuildActionEventCard(VisualElement body, ActionEventFrameData eventData)
        {
            if (eventData.entries == null) eventData.entries = new List<ActionEventEntry>();

            VisualElement itemList = new VisualElement();
            body.Add(itemList);

            for (int i = 0; i < eventData.entries.Count; i++)
            {
                int idx = i;
                ActionEventEntry entry = eventData.entries[i];
                if (entry.overlaySettings == null)
                {
                    entry.overlaySettings = new UltimateOverlaySequenceSettings();
                }

                BuildFoldoutCard($"Event #{idx + 1}: {entry.label}", $"event_entry_{idx}", entry.trackColor, inner =>
                {
                    AddTextField(inner, "Label", entry.label, value =>
                    {
                        RecordUndo("Edit Event Label");
                        entry.label = value;
                        MarkDirty();
                    });
                    AddEnumField(inner, "Event Type", entry.eventType, value =>
                    {
                        RecordUndo("Edit Event Type");
                        entry.eventType = (ActionEventType)value;
                        MarkDirty();
                    });
                    AddIntField(inner, "Trigger Frame", entry.triggerFrame, value =>
                    {
                        RecordUndo("Edit Event Frame");
                        entry.triggerFrame = value;
                        MarkDirty();
                    });
                    AddColorField(inner, "Track Color", entry.trackColor, value =>
                    {
                        RecordUndo("Edit Event Color");
                        entry.trackColor = value;
                        MarkDirty();
                    });

                    AddSeparator(inner, "Freeze Behavior");
                    AddToggleField(inner, "Freeze World", entry.freezeWorld, value =>
                    {
                        RecordUndo("Edit Event Freeze");
                        entry.freezeWorld = value;
                        MarkDirty();
                    });
                    AddEnumField(inner, "Timeline Mode", entry.timelineMode, value =>
                    {
                        RecordUndo("Edit Event Timeline Mode");
                        entry.timelineMode = (UltimateFreezeTimelineMode)value;
                        MarkDirty();
                    });

                    AddSeparator(inner, "Overlay Sequence");
                    AddFloatField(inner, "Fade In", entry.overlaySettings.fadeInDuration, value =>
                    {
                        RecordUndo("Edit Event Fade In");
                        entry.overlaySettings.fadeInDuration = Mathf.Max(0f, value);
                        MarkDirty();
                    });
                    AddFloatField(inner, "Hold", entry.overlaySettings.holdDuration, value =>
                    {
                        RecordUndo("Edit Event Hold");
                        entry.overlaySettings.holdDuration = Mathf.Max(0f, value);
                        MarkDirty();
                    });
                    AddFloatField(inner, "Darken", entry.overlaySettings.darkenDuration, value =>
                    {
                        RecordUndo("Edit Event Darken");
                        entry.overlaySettings.darkenDuration = Mathf.Max(0f, value);
                        MarkDirty();
                    });
                    AddFloatField(inner, "Black Hold", entry.overlaySettings.blackHoldDuration, value =>
                    {
                        RecordUndo("Edit Event Black Hold");
                        entry.overlaySettings.blackHoldDuration = Mathf.Max(0f, value);
                        MarkDirty();
                    });
                    AddFloatField(inner, "Fade Out", entry.overlaySettings.fadeOutDuration, value =>
                    {
                        RecordUndo("Edit Event Fade Out");
                        entry.overlaySettings.fadeOutDuration = Mathf.Max(0f, value);
                        MarkDirty();
                    });
                    AddColorField(inner, "Flash Color", entry.overlaySettings.flashColor, value =>
                    {
                        RecordUndo("Edit Event Flash Color");
                        entry.overlaySettings.flashColor = value;
                        MarkDirty();
                    });
                    AddColorField(inner, "Dark Color", entry.overlaySettings.darkColor, value =>
                    {
                        RecordUndo("Edit Event Dark Color");
                        entry.overlaySettings.darkColor = value;
                        MarkDirty();
                    });

                    AddSeparator(inner, "Activation Payload");
                    AddEnumField(inner, "VFX Type", entry.activationVFXType, value =>
                    {
                        RecordUndo("Edit Event VFX Type");
                        entry.activationVFXType = (Project.Combat.VFXType)value;
                        MarkDirty();
                    });
                    AddObjectField(inner, "VFX Prefab (Override)", entry.activationVFXPrefab, value =>
                    {
                        RecordUndo("Edit Event VFX Prefab");
                        entry.activationVFXPrefab = value as GameObject;
                        MarkDirty();
                    });
                    AddVector3Field(inner, "VFX Offset", entry.activationVFXOffset, value =>
                    {
                        RecordUndo("Edit Event VFX Offset");
                        entry.activationVFXOffset = value;
                        MarkDirty();
                    });
                    AddObjectField(inner, "Voice Clip", entry.activationVoice, value =>
                    {
                        RecordUndo("Edit Event Voice");
                        entry.activationVoice = value as AudioClip;
                        MarkDirty();
                    });
                    AddObjectField(inner, "SFX Clip", entry.activationSFX, value =>
                    {
                        RecordUndo("Edit Event SFX");
                        entry.activationSFX = value as AudioClip;
                        MarkDirty();
                    });
                    AddFloatField(inner, "Voice Delay", entry.voiceDelay, value =>
                    {
                        RecordUndo("Edit Event Voice Delay");
                        entry.voiceDelay = Mathf.Max(0f, value);
                        MarkDirty();
                    });
                }, () =>
                {
                    RecordUndo("Delete Action Event");
                    eventData.entries.RemoveAt(idx);
                    MarkDirtyAndRebuild();
                }, isSubCard: true, parent: itemList, onReorder: newIndex =>
                {
                    ReorderListItem(eventData.entries, idx, newIndex, "Reorder Action Event");
                }, reorderIndex: idx, reorderCount: eventData.entries.Count);
            }

            Button addButton = new Button(() =>
            {
                RecordUndo("Add Action Event");
                eventData.entries.Add(new ActionEventEntry());
                MarkDirtyAndRebuild();
            })
            {
                text = "Add Action Event"
            };
            addButton.AddToClassList("component-add-entry-btn");
            body.Add(addButton);
        }

        private void BuildHitDataCard(VisualElement body, HitFrameData hitData)
        {
            if (hitData.hitboxes == null) hitData.hitboxes = new List<HitboxBase>();

            VisualElement itemList = new VisualElement();
            body.Add(itemList);

            Dictionary<string, int> hitboxTypeCounts = new Dictionary<string, int>();

            for (int i = 0; i < hitData.hitboxes.Count; i++)
            {
                HitboxBase hitbox = hitData.hitboxes[i];
                if (hitbox == null) continue;

                string typeName = hitbox.HitboxTypeName;
                hitboxTypeCounts.TryGetValue(typeName, out int typeIndex);
                typeIndex++;
                hitboxTypeCounts[typeName] = typeIndex;

                BuildHitboxEntryCard(itemList, hitData, i, typeIndex);
            }

            BuildAddHitboxButton(body, hitData);
        }

        private void BuildHitboxEntryCard(VisualElement parent, HitFrameData hitData, int idx, int typeIndex)
        {
            HitboxBase hitbox = hitData.hitboxes[idx];
            if (hitbox == null) return;

            Color accent = hitbox.debugColor;
            accent.a = 0.7f;

            BuildFoldoutCard($"{hitbox.HitboxTypeName} #{typeIndex}", $"hitbox_{idx}", accent, inner =>
            {
                AddIntField(inner, "Start Offset", hitbox.startFrameOffset, value =>
                {
                    RecordUndo("Edit Hitbox Start Offset");
                    hitbox.startFrameOffset = value;
                    MarkDirty();
                });
                AddIntField(inner, "Duration", hitbox.activeFrames, value =>
                {
                    RecordUndo("Edit Hitbox Duration");
                    hitbox.activeFrames = value;
                    MarkDirty();
                });
                AddColorField(inner, "Debug Color", hitbox.debugColor, value =>
                {
                    RecordUndo("Edit Hitbox Color");
                    hitbox.debugColor = value;
                    MarkDirty();
                });
                AddEnumField(inner, "Damage Type", hitbox.damageType, value =>
                {
                    RecordUndo("Edit Damage Type");
                    hitbox.damageType = (DamageType)value;
                    MarkDirty();
                });

                if (hitbox is NormalHitbox normalHitbox)
                {
                    BuildNormalHitboxFields(inner, normalHitbox);
                }
                else if (hitbox is GrabHitbox grabHitbox)
                {
                    BuildGrabHitboxFields(inner, grabHitbox);
                }
                else if (hitbox is ProjectileSpawnHitbox projectileHitbox)
                {
                    BuildProjectileHitboxFields(inner, projectileHitbox);
                }
            }, () =>
            {
                RecordUndo("Delete Hitbox");
                hitData.hitboxes.RemoveAt(idx);
                MarkDirtyAndRebuild();
            }, isSubCard: true, parent: parent, onReorder: newIndex =>
            {
                ReorderListItem(hitData.hitboxes, idx, newIndex, "Reorder Hitbox");
            }, reorderIndex: idx, reorderCount: hitData.hitboxes.Count);
        }

        private void BuildNormalHitboxFields(VisualElement body, NormalHitbox hitbox)
        {
            AddSeparator(body, "Hitbox Geometry");
            AddVector2Field(body, "Offset", hitbox.offset, value =>
            {
                RecordUndo("Edit Offset");
                hitbox.offset = value;
                MarkDirty();
            });
            AddVector2Field(body, "Size", hitbox.size, value =>
            {
                RecordUndo("Edit Size");
                hitbox.size = value;
                MarkDirty();
            });

            AddSeparator(body, "Damage and Impact");
            AddFloatField(body, "Damage Scale", hitbox.damageMultiplier, value =>
            {
                RecordUndo("Edit Damage");
                hitbox.damageMultiplier = value;
                MarkDirty();
            });
            AddEnumField(body, "Impact Tier", hitbox.impactTier, value =>
            {
                RecordUndo("Edit Impact Tier");
                hitbox.impactTier = (DamageImpactTier)value;
                MarkDirty();
            });

            AddSeparator(body, "Reaction and Hitstop");
            AddToggle(body, "Deterministic Reaction", hitbox.useDeterministicReaction, value =>
            {
                RecordUndo("Edit Deterministic Reaction");
                hitbox.useDeterministicReaction = value;
                MarkDirty();
            });

            bool usesHorizontalSpeed = HitReactionData.UsesHorizontalSpeed(hitbox.reactionKind);
            bool usesLaunchSpeed = HitReactionData.UsesLaunchSpeed(hitbox.reactionKind);
            bool usesLandingBehavior = HitReactionData.UsesLandingBehavior(hitbox.reactionKind);
            bool usesAirJuggle = HitReactionData.UsesAirJuggleSettings(hitbox.reactionKind);

            AddEnumField(body, "Reaction Kind", hitbox.reactionKind, value =>
            {
                RecordUndo("Edit Reaction Kind");
                hitbox.reactionKind = (EnemyReactionKind)value;
                MarkDirtyAndRebuild();
            });
            AddIntField(body, "HitStop / Impact Freeze", hitbox.impactFreezeFrames, value =>
            {
                RecordUndo("Edit Impact Freeze");
                hitbox.impactFreezeFrames = value;
                MarkDirty();
            });

            if (hitbox.reactionKind == EnemyReactionKind.Knockback || hitbox.reactionKind == EnemyReactionKind.Flinch)
            {
                AddIntField(body, "Knockback Frames", hitbox.knockbackFrames, value =>
                {
                    RecordUndo("Edit Knockback Frames");
                    hitbox.knockbackFrames = value;
                    MarkDirty();
                });
            }

            if (hitbox.reactionKind == EnemyReactionKind.Knockup)
            {
                AddIntField(body, "Knockup Frames", hitbox.knockupFrames, value =>
                {
                    RecordUndo("Edit Knockup Frames");
                    hitbox.knockupFrames = value;
                    MarkDirty();
                });
                AddIntField(body, "Knockfall Frames", hitbox.knockfallFrames, value =>
                {
                    RecordUndo("Edit Knockfall Frames");
                    hitbox.knockfallFrames = value;
                    MarkDirty();
                });
            }

            AddIntField(body, "Recovery Frames", hitbox.recoveryFrames, value =>
            {
                RecordUndo("Edit Recovery Frames");
                hitbox.recoveryFrames = value;
                MarkDirty();
            });

            if (usesHorizontalSpeed)
            {
                AddFloatField(body, "Horizontal Speed", hitbox.horizontalSpeed, value =>
                {
                    RecordUndo("Edit Horizontal Speed");
                    hitbox.horizontalSpeed = value;
                    MarkDirty();
                });
            }

            if (usesLaunchSpeed)
            {
                AddFloatField(body, "Launch Up", hitbox.launchSpeed, value =>
                {
                    RecordUndo("Edit Launch Speed");
                    hitbox.launchSpeed = value;
                    MarkDirty();
                });
            }

            if (usesLandingBehavior)
            {
                AddEnumField(body, "Landing Behavior", hitbox.landingBehavior, value =>
                {
                    RecordUndo("Edit Landing Behavior");
                    hitbox.landingBehavior = (EnemyLandingBehavior)value;
                    MarkDirty();
                });
            }

            if (usesAirJuggle)
            {
                AddToggle(body, "Can Air Juggle", hitbox.canAirJuggle, value =>
                {
                    RecordUndo("Edit Can Air Juggle");
                    hitbox.canAirJuggle = value;
                    MarkDirtyAndRebuild();
                });

                if (hitbox.canAirJuggle)
                {
                    AddIntField(body, "Max Juggles", hitbox.maxJuggleCount, value =>
                    {
                        RecordUndo("Edit Max Juggles");
                        hitbox.maxJuggleCount = value;
                        MarkDirty();
                    });
                    AddFloatField(body, "Juggle Launch Scale", hitbox.juggleLaunchScale, value =>
                    {
                        RecordUndo("Edit Juggle Launch Scale");
                        hitbox.juggleLaunchScale = value;
                        MarkDirty();
                    });
                    AddFloatField(body, "Juggle Stun Scale", hitbox.juggleStunScale, value =>
                    {
                        RecordUndo("Edit Juggle Stun Scale");
                        hitbox.juggleStunScale = value;
                        MarkDirty();
                    });
                }
            }

            AddSeparator(body, "Snapping");
            AddToggle(body, "Use Snap to Anchor", hitbox.useSnapToAnchor, value =>
            {
                RecordUndo("Toggle Snap to Anchor");
                hitbox.useSnapToAnchor = value;
                MarkDirtyAndRebuild();
            });
            if (hitbox.useSnapToAnchor)
            {
                AddVector2Field(body, "Snap Offset", hitbox.snapOffset, value =>
                {
                    RecordUndo("Edit Snap Offset");
                    hitbox.snapOffset = value;
                    MarkDirty();
                });
            }

            AddColorField(body, "Flash Color", hitbox.flashColor, value =>
            {
                RecordUndo("Edit Flash Color");
                hitbox.flashColor = value;
                MarkDirty();
            });
            AddIntField(body, "Flash Frames", hitbox.flashFrames, value =>
            {
                RecordUndo("Edit Flash Frames");
                hitbox.flashFrames = value;
                MarkDirty();
            });

            AddSeparator(body, "Grab Interaction");
            AddToggle(body, "Breaks Grab", hitbox.breaksGrab, value =>
            {
                RecordUndo("Edit Breaks Grab");
                hitbox.breaksGrab = value;
                MarkDirty();
            });

            AddSeparator(body, "Hit Feedback (Override)");
            AddObjectField(body, "Hit SFX", hitbox.hitSFX, value =>
            {
                RecordUndo("Edit Hit SFX");
                hitbox.hitSFX = value;
                MarkDirty();
            });
            AddEnumField(body, "Hit VFX Type", hitbox.hitVFXType, value =>
            {
                RecordUndo("Edit Hit VFX Type");
                hitbox.hitVFXType = (Project.Combat.VFXType)value;
                MarkDirty();
            });
            AddObjectField(body, "Hit VFX Prefab (Override)", hitbox.hitVFXPrefab, value =>
            {
                RecordUndo("Edit Hit VFX Prefab");
                hitbox.hitVFXPrefab = value;
                MarkDirty();
            });
            AddVector2Field(body, "Hit VFX Offset", hitbox.hitVFXOffset, value =>
            {
                RecordUndo("Edit Hit VFX Offset");
                hitbox.hitVFXOffset = value;
                MarkDirty();
            });
        }

        private void BuildGrabHitboxFields(VisualElement body, GrabHitbox hitbox)
        {
            AddSeparator(body, "Hitbox Geometry");
            AddVector2Field(body, "Offset", hitbox.offset, value =>
            {
                RecordUndo("Edit Offset");
                hitbox.offset = value;
                MarkDirty();
            });
            AddVector2Field(body, "Size", hitbox.size, value =>
            {
                RecordUndo("Edit Size");
                hitbox.size = value;
                MarkDirty();
            });

            AddSeparator(body, "Grab Context");
            AddToggle(body, "Can Grab Air", hitbox.canGrabAirborne, value =>
            {
                RecordUndo("Edit Can Grab Air");
                hitbox.canGrabAirborne = value;
                MarkDirty();
            });

            AddSeparator(body, "Damage and Impact");
            AddFloatField(body, "Damage Scale", hitbox.damageMultiplier, value =>
            {
                RecordUndo("Edit Damage Scale");
                hitbox.damageMultiplier = value;
                MarkDirty();
            });
            AddEnumField(body, "Impact Tier", hitbox.impactTier, value =>
            {
                RecordUndo("Edit Grab Impact Tier");
                hitbox.impactTier = (DamageImpactTier)value;
                MarkDirty();
            });

            AddSeparator(body, "Reaction and Hitstop");
            AddIntField(body, "HitStop / Impact Freeze", hitbox.impactFreezeFrames, value =>
            {
                RecordUndo("Edit Grab Impact Freeze");
                hitbox.impactFreezeFrames = value;
                MarkDirty();
            });
            AddIntField(body, "Hold Frames", hitbox.holdFrames, value =>
            {
                RecordUndo("Edit Grab Hold Frames");
                hitbox.holdFrames = value;
                MarkDirty();
            });
            AddIntField(body, "Recovery Frames", hitbox.recoveryFrames, value =>
            {
                RecordUndo("Edit Grab Recovery Frames");
                hitbox.recoveryFrames = value;
                MarkDirty();
            });



            AddSeparator(body, "Flash");
            AddColorField(body, "Flash Color", hitbox.flashColor, value =>
            {
                RecordUndo("Edit Grab Flash Color");
                hitbox.flashColor = value;
                MarkDirty();
            });
            AddIntField(body, "Flash Frames", hitbox.flashFrames, value =>
            {
                RecordUndo("Edit Grab Flash Frames");
                hitbox.flashFrames = value;
                MarkDirty();
            });

            AddSeparator(body, "Hit Feedback (Override)");
            AddObjectField(body, "Hit SFX", hitbox.hitSFX, value =>
            {
                RecordUndo("Edit Grab Hit SFX");
                hitbox.hitSFX = value;
                MarkDirty();
            });
            AddEnumField(body, "Hit VFX Type", hitbox.hitVFXType, value =>
            {
                RecordUndo("Edit Hit VFX Type");
                hitbox.hitVFXType = (Project.Combat.VFXType)value;
                MarkDirty();
            });
            AddObjectField(body, "Hit VFX Prefab (Override)", hitbox.hitVFXPrefab, value =>
            {
                RecordUndo("Edit Hit VFX Prefab");
                hitbox.hitVFXPrefab = value;
                MarkDirty();
            });
            AddVector2Field(body, "Hit VFX Offset", hitbox.hitVFXOffset, value =>
            {
                RecordUndo("Edit Grab Hit VFX Offset");
                hitbox.hitVFXOffset = value;
                MarkDirty();
            });
        }

        private void BuildProjectileHitboxFields(VisualElement body, ProjectileSpawnHitbox hitbox)
        {
            AddObjectField(body, "Projectile Data", hitbox.projectileData, value =>
            {
                RecordUndo("Edit Projectile Data");
                hitbox.projectileData = value;
                MarkDirty();
            });

            if (hitbox.projectileData != null)
            {
                Button editBtn = new Button(() => ProjectileEditorWindow.OpenWindow(hitbox.projectileData)) 
                { 
                    text = "Edit Projectile Hitboxes",
                    style = { marginTop = 5, marginBottom = 5 }
                };
                editBtn.AddToClassList("component-add-entry-btn");
                body.Add(editBtn);
            }
            AddToggle(body, "Override Spawn Offset", hitbox.overrideSpawnOffset, value =>
            {
                RecordUndo("Edit Projectile Override Spawn");
                hitbox.overrideSpawnOffset = value;
                MarkDirtyAndRebuild();
            });

            if (hitbox.overrideSpawnOffset)
            {
                AddVector2Field(body, "Spawn Offset", hitbox.spawnOffset, value =>
                {
                    RecordUndo("Edit Projectile Spawn Offset");
                    hitbox.spawnOffset = value;
                    MarkDirty();
                });
            }
        }

        private void BuildAddComponentButton(AttackStepData step)
        {
            VisualElement addRow = new VisualElement();
            addRow.AddToClassList("add-component-row");

            Button addButton = new Button(() =>
            {
                GenericMenu menu = new GenericMenu();

                if (step.motionData == null)
                {
                    menu.AddItem(new GUIContent("Motion Frame Data"), false, () =>
                    {
                        RecordUndo("Add Motion");
                        step.motionData = new MotionFrameData();
                        MarkDirtyAndRebuild();
                    });
                }
                else menu.AddDisabledItem(new GUIContent("Motion Frame Data (added)"));

                if (step.vfxData == null)
                {
                    menu.AddItem(new GUIContent("VFX Frame Data"), false, () =>
                    {
                        RecordUndo("Add VFX");
                        step.vfxData = new VFXFrameData();
                        MarkDirtyAndRebuild();
                    });
                }
                else menu.AddDisabledItem(new GUIContent("VFX Frame Data (added)"));

                if (step.audioData == null)
                {
                    menu.AddItem(new GUIContent("Audio Frame Data"), false, () =>
                    {
                        RecordUndo("Add Audio");
                        step.audioData = new AudioFrameData();
                        MarkDirtyAndRebuild();
                    });
                }
                else menu.AddDisabledItem(new GUIContent("Audio Frame Data (added)"));

                if (step.hitData == null)
                {
                    menu.AddItem(new GUIContent("Hitbox Data"), false, () =>
                    {
                        RecordUndo("Add Hit Data");
                        step.hitData = new HitFrameData();
                        MarkDirtyAndRebuild();
                    });
                }
                else menu.AddDisabledItem(new GUIContent("Hitbox Data (added)"));

                if (step.eventData == null)
                {
                    menu.AddItem(new GUIContent("Action Event Data"), false, () =>
                    {
                        RecordUndo("Add Action Events");
                        step.eventData = new ActionEventFrameData();
                        MarkDirtyAndRebuild();
                    });
                }
                else menu.AddDisabledItem(new GUIContent("Action Event Data (added)"));

                menu.ShowAsContext();
            })
            { text = "Add Component" };
            addButton.AddToClassList("add-component-btn");
            addRow.Add(addButton);

            container.Add(addRow);
        }

        private void BuildAddHitboxButton(VisualElement body, HitFrameData hitData)
        {
            Button addButton = new Button(() =>
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Normal Hitbox"), false, () =>
                {
                    RecordUndo("Add Normal Hitbox");
                    hitData.hitboxes.Add(new NormalHitbox());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Grab Hitbox"), false, () =>
                {
                    RecordUndo("Add Grab Hitbox");
                    hitData.hitboxes.Add(new GrabHitbox());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Projectile Spawn Hitbox"), false, () =>
                {
                    RecordUndo("Add Projectile Spawn Hitbox");
                    hitData.hitboxes.Add(new ProjectileSpawnHitbox());
                    MarkDirtyAndRebuild();
                });
                menu.ShowAsContext();
            })
            { text = "Add Hitbox" };
            addButton.AddToClassList("component-add-entry-btn");
            body.Add(addButton);
        }

        private void BuildDashCards(DashData dash)
        {
            if (dash == null) return;
            BuildFoldoutCard("Dash Configuration", "dash_config", new Color(0f, 0.83f, 1f), body =>
            {
                if (dash.animationClip != null)
                {
                    int total = Mathf.RoundToInt(dash.animationClip.length * FrameDataConstants.LOGIC_FPS);
                    AddPhaseSlider(body, dash.startupFrames, dash.activeFrames, dash.recoveryFrames, total, (startup, active, recovery) =>
                    {
                        RecordUndo("Adjust Dash Phases");
                        dash.startupFrames = startup;
                        dash.activeFrames = active;
                        dash.recoveryFrames = recovery;
                        MarkDirty();
                    });
                }
                else
                {
                    AddIntField(body, "Startup", dash.startupFrames, value =>
                    {
                        RecordUndo("Edit Dash Startup");
                        dash.startupFrames = value;
                        MarkDirty();
                    });
                    AddIntField(body, "Active", dash.activeFrames, value =>
                    {
                        RecordUndo("Edit Dash Active");
                        dash.activeFrames = value;
                        MarkDirty();
                    });
                    AddIntField(body, "Recovery", dash.recoveryFrames, value =>
                    {
                        RecordUndo("Edit Dash Recovery");
                        dash.recoveryFrames = value;
                        MarkDirty();
                    });
                }

                AddIntField(body, "Cancel Frame", dash.cancelWindowStartFrame, value =>
                {
                    RecordUndo("Edit Dash Cancel Frame");
                    dash.cancelWindowStartFrame = value;
                    MarkDirty();
                });
                AddIntField(body, "Cooldown Frames", dash.cooldownFrames, value =>
                {
                    RecordUndo("Edit Dash Cooldown");
                    dash.cooldownFrames = value;
                    MarkDirty();
                });
            });
        }

        private void BuildPlaceholder(string message)
        {
            VisualElement empty = new VisualElement();
            empty.AddToClassList("empty-state");
            Label text = new Label(message);
            text.AddToClassList("empty-state__text");
            empty.Add(text);
            container.Add(empty);
        }

        private void BuildFoldoutCard(string title, string foldoutKey, Color accentColor, Action<VisualElement> buildBody, Action onDelete = null, bool isSubCard = false)
        {
            if (!foldoutState.ContainsKey(foldoutKey)) foldoutState[foldoutKey] = true;
            bool expanded = foldoutState[foldoutKey];

            VisualElement card = new VisualElement();
            card.AddToClassList(isSubCard ? "property-subcard" : "property-card");

            VisualElement header = new VisualElement();
            header.AddToClassList("property-card__header");

            VisualElement accent = new VisualElement();
            accent.AddToClassList("property-card__accent");
            accent.style.backgroundColor = accentColor;
            header.Add(accent);

            Label arrow = new Label(expanded ? "▼" : "▶");
            arrow.AddToClassList("property-card__arrow");
            header.Add(arrow);

            Label headerText = new Label(title);
            headerText.AddToClassList("property-card__header-text");
            header.Add(headerText);

            if (onDelete != null)
            {
                Button deleteButton = new Button(onDelete) { text = "X" };
                deleteButton.AddToClassList("property-card__delete");
                header.Add(deleteButton);
            }

            VisualElement body = new VisualElement();
            body.AddToClassList("property-card__body");
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            buildBody(body);

            string key = foldoutKey;
            header.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;
                foldoutState[key] = !foldoutState[key];
                bool isExpanded = foldoutState[key];
                arrow.text = isExpanded ? "▼" : "▶";
                body.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            });

            card.Add(header);
            card.Add(body);
            container.Add(card);
        }

        private VisualElement BuildFoldoutCard(string title, string foldoutKey, Color accentColor, Action<VisualElement> buildBody, Action onDelete, bool isSubCard, VisualElement parent, Action<int> onReorder, int reorderIndex, int reorderCount)
        {
            if (!foldoutState.ContainsKey(foldoutKey)) foldoutState[foldoutKey] = true;
            bool expanded = foldoutState[foldoutKey];

            VisualElement card = new VisualElement();
            card.AddToClassList(isSubCard ? "property-subcard" : "property-card");

            VisualElement header = new VisualElement();
            header.AddToClassList("property-card__header");

            VisualElement accent = new VisualElement();
            accent.AddToClassList("property-card__accent");
            accent.style.backgroundColor = accentColor;
            header.Add(accent);

            VisualElement dragHandle = null;
            if (onReorder != null && reorderCount > 1)
            {
                dragHandle = new Label("|||");
                dragHandle.AddToClassList("property-card__drag-handle");
                header.Add(dragHandle);
            }

            Label arrow = new Label(expanded ? "▼" : "▶");
            arrow.AddToClassList("property-card__arrow");
            header.Add(arrow);

            Label headerText = new Label(title);
            headerText.AddToClassList("property-card__header-text");
            header.Add(headerText);

            if (onDelete != null)
            {
                Button deleteButton = new Button(onDelete) { text = "X" };
                deleteButton.AddToClassList("property-card__delete");
                header.Add(deleteButton);
            }

            VisualElement body = new VisualElement();
            body.AddToClassList("property-card__body");
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            buildBody(body);

            string key = foldoutKey;
            header.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;
                foldoutState[key] = !foldoutState[key];
                bool isExpanded = foldoutState[key];
                arrow.text = isExpanded ? "▼" : "▶";
                body.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            });

            card.Add(header);
            card.Add(body);

            VisualElement targetParent = parent ?? container;
            targetParent.Add(card);

            if (dragHandle != null)
            {
                dragHandle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                dragHandle.AddManipulator(new ReorderableCardDragManipulator(card, targetParent, reorderIndex, onReorder));
            }

            return card;
        }

        private void AddTextField(VisualElement parent, string label, string value, Action<string> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            TextField field = new TextField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddIntField(VisualElement parent, string label, int value, Action<int> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            IntegerField field = new IntegerField(label) { value = value, isDelayed = false };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddFloatField(VisualElement parent, string label, float value, Action<float> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            FloatField field = new FloatField(label) { value = value, isDelayed = false };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddFloatSlider(VisualElement parent, string label, float value, float min, float max, Action<float> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            Slider slider = new Slider(label, min, max) { value = value, showInputField = true };
            slider.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(slider);
            parent.Add(row);
        }

        private void AddToggleField(VisualElement parent, string label, bool value, Action<bool> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            Toggle field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddToggle(VisualElement parent, string label, bool value, Action<bool> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            Toggle field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddVector2Field(VisualElement parent, string label, Vector2 value, Action<Vector2> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            Vector2Field field = new Vector2Field(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddVector3Field(VisualElement parent, string label, Vector3 value, Action<Vector3> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            Vector3Field field = new Vector3Field(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddColorField(VisualElement parent, string label, Color value, Action<Color> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            ColorField field = new ColorField(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddEnumField(VisualElement parent, string label, Enum value, Action<Enum> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            EnumField field = new EnumField(label, value);
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddObjectField<T>(VisualElement parent, string label, T value, Action<T> onChange) where T : UnityEngine.Object
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            ObjectField field = new ObjectField(label) { objectType = typeof(T), value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue as T));
            row.Add(field);
            parent.Add(row);
        }

        private void AddLayerField(VisualElement parent, string label, LayerMask value, Action<LayerMask> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            LayerMaskField field = new LayerMaskField(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void BuildConditionList(VisualElement parent, List<ActionConditionBase> conditions, string foldoutKey, string undoLabelPrefix)
        {
            if (conditions == null) return;
            VisualElement list = new VisualElement();
            parent.Add(list);

            for (int i = 0; i < conditions.Count; i++)
            {
                int index = i;
                ActionConditionBase condition = conditions[i];
                if (condition == null) continue;

                BuildFoldoutCard(GetConditionTitle(condition), $"{foldoutKey}_{i}", new Color(0.3f, 0.72f, 0.92f, 0.7f),
                    body => BuildConditionFields(body, condition),
                    () => {
                        RecordUndo($"Remove {undoLabelPrefix} Condition");
                        conditions.RemoveAt(index);
                        MarkDirtyAndRebuild();
                    }, true, list, null, 0, 0);
            }

            Button addButton = new Button(() =>
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Grounded / Airborne"), false, () => {
                    RecordUndo($"Add {undoLabelPrefix} Condition");
                    conditions.Add(new GroundedActionCondition());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Airborne Landing"), false, () => {
                    RecordUndo($"Add {undoLabelPrefix} Condition");
                    conditions.Add(new AirborneLandingActionCondition());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Direction"), false, () => {
                    RecordUndo($"Add {undoLabelPrefix} Condition");
                    conditions.Add(new DirectionActionCondition());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Previous State"), false, () => {
                    RecordUndo($"Add {undoLabelPrefix} Condition");
                    conditions.Add(new PreviousStateActionCondition());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Grabbed Target"), false, () => {
                    RecordUndo($"Add {undoLabelPrefix} Condition");
                    conditions.Add(new GrabbedEnemyActionCondition());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Against Wall"), false, () => {
                    RecordUndo($"Add {undoLabelPrefix} Condition");
                    conditions.Add(new WallActionCondition());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Progression Rank"), false, () => {
                    RecordUndo($"Add {undoLabelPrefix} Condition");
                    conditions.Add(new ProgressionRankActionCondition());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Runtime Tag"), false, () => {
                    RecordUndo($"Add {undoLabelPrefix} Condition");
                    conditions.Add(new RuntimeTagActionCondition());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Runtime Counter"), false, () => {
                    RecordUndo($"Add {undoLabelPrefix} Condition");
                    conditions.Add(new RuntimeCounterActionCondition());
                    MarkDirtyAndRebuild();
                });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Wait for Frame / Sec"), false, () => {
                    RecordUndo($"Add {undoLabelPrefix} Condition");
                    conditions.Add(new WaitActionCondition());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Player Got Hit (Counter)"), false, () => {
                    RecordUndo($"Add {undoLabelPrefix} Condition");
                    conditions.Add(new PlayerGotHitCondition());
                    MarkDirtyAndRebuild();
                });
                menu.ShowAsContext();
            })
            { text = $"+ Add {undoLabelPrefix} Condition" };
            addButton.AddToClassList("component-add-entry-btn");
            parent.Add(addButton);
        }

        private void BuildConditionFields(VisualElement parent, ActionConditionBase condition)
        {
            switch (condition)
            {
                case GroundedActionCondition groundedCondition:
                    AddToggleField(parent, "Require Grounded", groundedCondition.requireGrounded, value => {
                        RecordUndo("Edit Condition Grounded");
                        groundedCondition.requireGrounded = value;
                        MarkDirty();
                    });
                    break;
                case AirborneLandingActionCondition landingCondition:
                    AddToggleField(parent, "Require Just Landed", landingCondition.requireJustLanded, value => {
                        RecordUndo("Edit Condition Airborne Landing");
                        landingCondition.requireJustLanded = value;
                        MarkDirty();
                    });
                    break;
                case DirectionActionCondition directionCondition:
                    AddEnumField(parent, "Direction", directionCondition.direction, value => {
                        RecordUndo("Edit Condition Direction");
                        directionCondition.direction = (DirectionRequirement)value;
                        MarkDirty();
                    });
                    break;
                case PreviousStateActionCondition previousStateCondition:
                    AddEnumField(parent, "Previous State", previousStateCondition.previousState, value => {
                        RecordUndo("Edit Condition Previous State");
                        previousStateCondition.previousState = (PlayerStateId)value;
                        MarkDirty();
                    });
                    break;
                case GrabbedEnemyActionCondition grabbedCondition:
                    AddToggleField(parent, "Require Grabbed Enemy", grabbedCondition.requireGrabbedEnemy, value => {
                        RecordUndo("Edit Condition Grabbed");
                        grabbedCondition.requireGrabbedEnemy = value;
                        MarkDirty();
                    });
                    break;
                case WallActionCondition wallCondition:
                    AddToggleField(parent, "Require Hit Wall", wallCondition.requireAgainstWall, value => {
                        RecordUndo("Edit Condition Wall");
                        wallCondition.requireAgainstWall = value;
                        MarkDirty();
                    });
                    break;
                case ProgressionRankActionCondition progressionCondition:
                    AddEnumField(parent, "Comparison", progressionCondition.comparison, value => {
                        RecordUndo("Edit Condition Rank Compare");
                        progressionCondition.comparison = (CounterComparison)value;
                        MarkDirty();
                    });
                    AddIntField(parent, "Rank", progressionCondition.rank, value => {
                        RecordUndo("Edit Condition Rank");
                        progressionCondition.rank = value;
                        MarkDirty();
                    });
                    break;
                case RuntimeTagActionCondition tagCondition:
                    AddTextField(parent, "Tag", tagCondition.tag, value => {
                        RecordUndo("Edit Condition Tag");
                        tagCondition.tag = value;
                        MarkDirty();
                    });
                    AddToggleField(parent, "Should Exist", tagCondition.shouldExist, value => {
                        RecordUndo("Edit Condition Tag Exists");
                        tagCondition.shouldExist = value;
                        MarkDirty();
                    });
                    break;
                case RuntimeCounterActionCondition counterCondition:
                    AddTextField(parent, "Counter", counterCondition.counterId, value => {
                        RecordUndo("Edit Condition Counter");
                        counterCondition.counterId = value;
                        MarkDirty();
                    });
                    AddEnumField(parent, "Comparison", counterCondition.comparison, value => {
                        RecordUndo("Edit Condition Counter Compare");
                        counterCondition.comparison = (CounterComparison)value;
                        MarkDirty();
                    });
                    AddIntField(parent, "Value", counterCondition.value, value => {
                        RecordUndo("Edit Condition Counter Value");
                        counterCondition.value = value;
                        MarkDirty();
                    });
                    break;
                case WaitActionCondition waitCondition:
                    AddEnumField(parent, "Wait Unit", waitCondition.waitUnit, value => {
                        RecordUndo("Edit Wait Unit");
                        waitCondition.waitUnit = (WaitUnit)value;
                        MarkDirty();
                    });
                    AddFloatField(parent, "Wait Value", waitCondition.waitValue, value => {
                        RecordUndo("Edit Wait Value");
                        waitCondition.waitValue = value;
                        MarkDirty();
                    });
                    break;
                case PlayerGotHitCondition hitCondition:
                    AddSeparator(parent, "Counter Reaction (On Attacker)");
                    AddEnumField(parent, "Reaction Kind", hitCondition.counterReaction.reactionKind, value => {
                        RecordUndo("Edit Counter Reaction Kind");
                        hitCondition.counterReaction.reactionKind = (EnemyReactionKind)value;
                        MarkDirty();
                    });
                    AddIntField(parent, "Hitstun Frames", hitCondition.counterReaction.hitstunFrames, value => {
                        RecordUndo("Edit Counter Hitstun");
                        hitCondition.counterReaction.hitstunFrames = value;
                        MarkDirty();
                    });
                    AddFloatField(parent, "Horizontal Speed", hitCondition.counterReaction.horizontalSpeed, value => {
                        RecordUndo("Edit Counter H-Speed");
                        hitCondition.counterReaction.horizontalSpeed = value;
                        MarkDirty();
                    });
                    AddFloatField(parent, "Launch Speed", hitCondition.counterReaction.launchSpeed, value => {
                        RecordUndo("Edit Counter Launch");
                        hitCondition.counterReaction.launchSpeed = value;
                        MarkDirty();
                    });
                    break;
            }
        }

        private static string GetConditionTitle(ActionConditionBase condition)
        {
            return condition switch
            {
                GroundedActionCondition => "Grounded Condition",
                AirborneLandingActionCondition => "Airborne Landing Condition",
                DirectionActionCondition => "Direction Condition",
                PreviousStateActionCondition => "Previous State Condition",
                GrabbedEnemyActionCondition => "Grabbed Target Condition",
                WallActionCondition => "Wall Condition",
                ProgressionRankActionCondition => "Progression Rank Condition",
                RuntimeTagActionCondition => "Runtime Tag Condition",
                RuntimeCounterActionCondition => "Runtime Counter Condition",
                WaitActionCondition => "Wait Timer Condition",
                PlayerGotHitCondition => "Counter Trigger Condition",
                _ => "Condition"
            };
        }

        private void AddPhaseSlider(VisualElement parent, int startup, int active, int recovery, int total, Action<int, int, int> onChanges)
        {
            Label readout = new Label($"Total Animation Frames: {total}");
            readout.AddToClassList("phase-readout");
            parent.Add(readout);
            MinMaxSlider slider = new MinMaxSlider(startup, startup + active, 0, total);
            VisualElement fields = new VisualElement();
            fields.AddToClassList("phase-fields");
            IntegerField startupField = new IntegerField("SU") { value = startup };
            IntegerField activeField = new IntegerField("ACT") { value = active };
            IntegerField recoveryField = new IntegerField("REC") { value = recovery };

            slider.RegisterValueChangedCallback(evt => {
                int newStartup = Mathf.RoundToInt(evt.newValue.x);
                int newActive = Mathf.RoundToInt(evt.newValue.y) - newStartup;
                int newRecovery = total - newStartup - newActive;
                startupField.SetValueWithoutNotify(newStartup);
                activeField.SetValueWithoutNotify(newActive);
                recoveryField.SetValueWithoutNotify(newRecovery);
                onChanges?.Invoke(Mathf.Max(0, newStartup), Mathf.Max(0, newActive), Mathf.Max(0, newRecovery));
            });
            parent.Add(slider);
            startupField.RegisterValueChangedCallback(evt => {
                int value = evt.newValue;
                slider.SetValueWithoutNotify(new Vector2(value, value + activeField.value));
                onChanges?.Invoke(value, activeField.value, recoveryField.value);
            });
            fields.Add(startupField);
            activeField.RegisterValueChangedCallback(evt => {
                int value = evt.newValue;
                slider.SetValueWithoutNotify(new Vector2(startupField.value, startupField.value + value));
                onChanges?.Invoke(startupField.value, value, recoveryField.value);
            });
            fields.Add(activeField);
            recoveryField.RegisterValueChangedCallback(evt => {
                int value = evt.newValue;
                int nextActive = total - startupField.value - value;
                activeField.SetValueWithoutNotify(nextActive);
                slider.SetValueWithoutNotify(new Vector2(startupField.value, startupField.value + nextActive));
                onChanges?.Invoke(startupField.value, nextActive, value);
            });
            fields.Add(recoveryField);
            parent.Add(fields);
        }

        private void AddSeparator(VisualElement parent, string label)
        {
            VisualElement separator = new VisualElement();
            separator.AddToClassList("card-separator");
            parent.Add(separator);
            if (!string.IsNullOrEmpty(label))
            {
                Label text = new Label(label);
                text.AddToClassList("card-sub-header");
                parent.Add(text);
            }
        }

        private void ReorderListItem<T>(List<T> list, int fromIndex, int toIndex, string action)
        {
            if (list == null || fromIndex < 0 || fromIndex >= list.Count) return;
            int clampedIndex = Mathf.Clamp(toIndex, 0, list.Count - 1);
            if (fromIndex == clampedIndex) return;
            RecordUndo(action);
            T movedItem = list[fromIndex];
            list.RemoveAt(fromIndex);
            list.Insert(Mathf.Clamp(clampedIndex, 0, list.Count), movedItem);
            MarkDirtyAndRebuild();
        }

        private sealed class ReorderableCardDragManipulator : PointerManipulator
        {
            private const float DragThreshold = 4f;
            private readonly VisualElement card;
            private readonly VisualElement listContainer;
            private readonly Action<int> onReorder;
            private readonly int originalIndex;
            private readonly VisualElement placeholder;
            private int activePointerId = -1;
            private float startPointerY;
            private float startCardTop;
            private bool isSwapping;
            private bool isDragging;

            public ReorderableCardDragManipulator(VisualElement card, VisualElement listContainer, int originalIndex, Action<int> onReorder)
            {
                this.card = card;
                this.listContainer = listContainer;
                this.originalIndex = originalIndex;
                this.onReorder = onReorder;
                placeholder = new VisualElement();
                placeholder.AddToClassList("property-card__placeholder");
            }

            protected override void RegisterCallbacksOnTarget() {
                target.RegisterCallback<PointerDownEvent>(OnPointerDown);
                target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                target.RegisterCallback<PointerUpEvent>(OnPointerUp);
                target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            protected override void UnregisterCallbacksFromTarget() {
                target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            private void OnPointerDown(PointerDownEvent evt) {
                if (evt.button != 0 || activePointerId != -1) return;
                activePointerId = evt.pointerId;
                startPointerY = evt.position.y;
                startCardTop = card.layout.y;
                target.CapturePointer(activePointerId);
                evt.StopImmediatePropagation();
            }

            private void OnPointerMove(PointerMoveEvent evt) {
                if (evt.pointerId != activePointerId) return;
                float deltaY = evt.position.y - startPointerY;
                if (!isDragging) {
                    if (Mathf.Abs(deltaY) < DragThreshold) return;
                    BeginDrag();
                }
                card.style.top = startCardTop + deltaY;
                int newIndex = GetInsertionIndex(evt.position.y);
                if (listContainer.Contains(placeholder) && listContainer.IndexOf(placeholder) != newIndex) {
                    placeholder.RemoveFromHierarchy();
                    listContainer.Insert(newIndex, placeholder);
                }
                evt.StopPropagation();
            }

            private void OnPointerUp(PointerUpEvent evt) {
                if (evt.pointerId != activePointerId) return;
                FinishDrag();
                evt.StopPropagation();
            }

            private void OnPointerCaptureOut(PointerCaptureOutEvent evt) {
                if (isSwapping) return;
                if (activePointerId == -1) return;
                FinishDrag();
            }

            private void BeginDrag() {
                isDragging = true;
                isSwapping = true;
                
                placeholder.style.height = card.resolvedStyle.height;
                placeholder.style.marginTop = card.resolvedStyle.marginTop;
                placeholder.style.marginBottom = card.resolvedStyle.marginBottom;
                placeholder.style.marginLeft = card.resolvedStyle.marginLeft;
                placeholder.style.marginRight = card.resolvedStyle.marginRight;
                
                // UX Styling for placeholder
                placeholder.style.borderTopWidth = 2f;
                placeholder.style.borderBottomWidth = 2f;
                placeholder.style.borderLeftWidth = 2f;
                placeholder.style.borderRightWidth = 2f;
                placeholder.style.borderTopColor = new StyleColor(new Color(0.3f, 0.6f, 1f, 0.8f));
                placeholder.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.6f, 1f, 0.8f));
                placeholder.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.6f, 1f, 0.8f));
                placeholder.style.borderRightColor = new StyleColor(new Color(0.3f, 0.6f, 1f, 0.8f));
                placeholder.style.backgroundColor = new StyleColor(new Color(0.1f, 0.2f, 0.3f, 0.3f));
                placeholder.style.borderTopLeftRadius = 6f;
                placeholder.style.borderTopRightRadius = 6f;
                placeholder.style.borderBottomLeftRadius = 6f;
                placeholder.style.borderBottomRightRadius = 6f;

                card.RemoveFromHierarchy();
                listContainer.Insert(originalIndex, placeholder);
                listContainer.Add(card);
                
                // CRITICAL: Re-capture because hierarchy change releases it
                target.CapturePointer(activePointerId);
                isSwapping = false;

                card.AddToClassList("property-card--dragging");
                
                // UX Styling for dragged card
                card.style.opacity = 0.85f;
                card.style.scale = new StyleScale(new Scale(new Vector3(0.98f, 0.98f, 1f)));

                card.style.position = Position.Absolute;
                card.style.left = 0f;
                card.style.right = 0f;
                card.style.top = startCardTop;
                card.BringToFront();
            }

            private void FinishDrag() {
                int pointerId = activePointerId;
                activePointerId = -1;
                if (pointerId != -1 && target.HasPointerCapture(pointerId)) target.ReleasePointer(pointerId);
                if (isDragging) {
                    int finalIndex = listContainer.IndexOf(placeholder);
                    placeholder.RemoveFromHierarchy();
                    if (listContainer.Contains(card)) card.RemoveFromHierarchy();
                    card.RemoveFromClassList("property-card--dragging");
                    card.style.position = Position.Relative;
                    card.style.left = new StyleLength(StyleKeyword.Null);
                    card.style.right = new StyleLength(StyleKeyword.Null);
                    card.style.top = new StyleLength(StyleKeyword.Null);
                    
                    card.style.opacity = new StyleFloat(StyleKeyword.Null);
                    card.style.scale = new StyleScale(StyleKeyword.Null);

                    listContainer.Insert(Mathf.Clamp(finalIndex, 0, listContainer.childCount), card);
                    if (finalIndex != originalIndex) onReorder?.Invoke(finalIndex);
                }
                isDragging = false;
            }

            private int GetInsertionIndex(float pointerY) {
                int index = 0;
                foreach (VisualElement child in listContainer.Children()) {
                    if (child == card || child == placeholder) continue;
                    if (pointerY > child.worldBound.center.y) index++;
                }
                return index;
            }
        }

        private void RecordUndo(string action)
        {
            if (targetObject != null) {
                Undo.RecordObject(targetObject, action);
                if (targetObject is CharacterObject character && character.actionResolverTable != null) 
                    Undo.RecordObject(character.actionResolverTable, action);
            }
        }

        private void MarkDirty()
        {
            if (targetObject != null) {
                EditorUtility.SetDirty(targetObject);
                if (targetObject is CharacterObject character && character.actionResolverTable != null) 
                    EditorUtility.SetDirty(character.actionResolverTable);
            }
            onDataChanged?.Invoke();
        }

        private void MarkDirtyAndRebuild()
        {
            if (targetObject != null) {
                EditorUtility.SetDirty(targetObject);
                if (targetObject is CharacterObject character && character.actionResolverTable != null) 
                    EditorUtility.SetDirty(character.actionResolverTable);
            }
            if (onStructureChanged != null && currentNode != null) {
                onStructureChanged.Invoke();
                return;
            }
            onDataChanged?.Invoke();
        }
    }
}
