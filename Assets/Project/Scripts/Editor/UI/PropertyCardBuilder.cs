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

        private CharacterObject character;
        private ActionNode currentNode;

        public PropertyCardBuilder(VisualElement panelContainer, Action onDataChanged, Action onStructureChanged)
        {
            container = panelContainer;
            this.onDataChanged = onDataChanged;
            this.onStructureChanged = onStructureChanged;
        }

        public void SetCharacter(CharacterObject character)
        {
            this.character = character;
        }

        public void Build(ActionNode node)
        {
            container.Clear();
            currentNode = node;

            if (node == null || character == null)
            {
                return;
            }

            switch (node.DataType)
            {
                case "AttackStep":
                    BuildAttackStepCards(node.DataRef as AttackStepData);
                    break;
                case "Dash":
                    BuildDashCards(node.DataRef as DashData);
                    break;
                case "Skill":
                    BuildPlaceholder("Skill data - edit in Inspector");
                    break;
                case "Ultimate":
                    BuildPlaceholder("Ultimate data - edit in Inspector");
                    break;
            }
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
                    MarkDirty();
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
                            if (newIndex == reorderIndex)
                            {
                                return;
                            }

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
                            if (newIndex == reorderIndex)
                            {
                                return;
                            }

                            RecordUndo("Reorder Components");
                            step.MoveComponentKey(AttackStepData.VfxComponentKey, newIndex);
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
                            if (newIndex == reorderIndex)
                            {
                                return;
                            }

                            RecordUndo("Reorder Components");
                            step.MoveComponentKey(AttackStepData.HitboxComponentKey, newIndex);
                            MarkDirtyAndRebuild();
                        }, reorderIndex, orderedComponents.Count);
                        break;
                }
            }

            BuildAddComponentButton(step);
        }

        private void BuildMotionCard(VisualElement body, MotionFrameData motion)
        {
            if (motion.keyframes == null)
            {
                motion.keyframes = new List<MotionKeyframe>();
            }

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
            if (vfx.entries == null)
            {
                vfx.entries = new List<VFXEntry>();
            }

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
                    AddObjectField(inner, "VFX Prefab", entry.vfxPrefab, value =>
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

                    AddSeparator(inner, "Transform Keys");
                    if (entry.transformKeys == null)
                    {
                        entry.transformKeys = new List<VFXTransformKey>();
                    }

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

        private void BuildHitDataCard(VisualElement body, HitFrameData hitData)
        {
            if (hitData.hitboxes == null)
            {
                hitData.hitboxes = new List<HitboxBase>();
            }

            VisualElement itemList = new VisualElement();
            body.Add(itemList);

            Dictionary<string, int> hitboxTypeCounts = new Dictionary<string, int>();

            for (int i = 0; i < hitData.hitboxes.Count; i++)
            {
                HitboxBase hitbox = hitData.hitboxes[i];
                if (hitbox == null)
                {
                    continue;
                }

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
            if (hitbox == null)
            {
                return;
            }

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

                if (hitbox is NormalHitbox normalHitbox)
                {
                    BuildNormalHitboxFields(inner, normalHitbox);
                }
                else if (hitbox is GrabHitbox grabHitbox)
                {
                    BuildGrabHitboxFields(inner, grabHitbox);
                }
                else if (hitbox is ProjectileHitbox projectileHitbox)
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
            AddToggle(body, "Use Deterministic Reaction", hitbox.useDeterministicReaction, value =>
            {
                RecordUndo("Toggle Deterministic Reaction");
                if (value && !hitbox.useDeterministicReaction)
                {
                    hitbox.reactionKind = HitReactionData.InferLegacyReactionKind(hitbox.reactionVelocity, hitbox.isKnockdown);
                    hitbox.impactFreezeFrames = hitbox.hitstunFrames;
                    hitbox.reactionFrames = hitbox.knockbackFrames;
                    hitbox.recoveryFrames = Mathf.Max(1, hitbox.recoveryFrames);
                    hitbox.horizontalSpeed = Mathf.Abs(hitbox.reactionVelocity.x);
                    hitbox.launchSpeed = Mathf.Max(0f, hitbox.reactionVelocity.y);
                    hitbox.landingBehavior = HitReactionData.InferLandingBehavior(hitbox.reactionVelocity, hitbox.isKnockdown);
                    hitbox.canAirJuggle = hitbox.isKnockdown || hitbox.reactionVelocity.y > 0.01f;
                    hitbox.maxJuggleCount = hitbox.canAirJuggle ? Mathf.Max(1, hitbox.maxJuggleCount) : 0;
                }

                hitbox.useDeterministicReaction = value;
                MarkDirtyAndRebuild();
            });

            if (hitbox.useDeterministicReaction)
            {
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
                AddIntField(body, "Reaction Frames", hitbox.reactionFrames, value =>
                {
                    RecordUndo("Edit Reaction Frames");
                    hitbox.reactionFrames = value;
                    MarkDirty();
                });
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
            }
            else
            {
                body.Add(new HelpBox(
                    "Legacy reaction velocity mode is active. Enable deterministic reaction to author knockback, knockup, landing, and juggle timing directly.",
                    HelpBoxMessageType.Info));

                AddIntField(body, "HitStop / Freeze", hitbox.hitstunFrames, value =>
                {
                    RecordUndo("Edit Hitstop");
                    hitbox.hitstunFrames = value;
                    MarkDirty();
                });
                AddVector2Field(body, "Reaction Velocity", hitbox.reactionVelocity, value =>
                {
                    RecordUndo("Edit Reaction Velocity");
                    hitbox.reactionVelocity = value;
                    MarkDirty();
                });
                AddIntField(body, "Knockback Frames", hitbox.knockbackFrames, value =>
                {
                    RecordUndo("Edit Knockback Frames");
                    hitbox.knockbackFrames = value;
                    MarkDirty();
                });
                AddToggle(body, "Is Knockdown", hitbox.isKnockdown, value =>
                {
                    RecordUndo("Edit Knockdown");
                    hitbox.isKnockdown = value;
                    MarkDirty();
                });
            }

            AddToggle(body, "Legacy Snap To Attacker", hitbox.snapToAttacker, value =>
            {
                RecordUndo("Edit Snap To Attacker");
                hitbox.snapToAttacker = value;
                MarkDirtyAndRebuild();
            });

            if (hitbox.snapToAttacker)
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

            AddSeparator(body, "Grab Condition");
            AddFloatField(body, "Grab Range", hitbox.grabRange, value =>
            {
                RecordUndo("Edit Grab Range");
                hitbox.grabRange = value;
                MarkDirty();
            });
            AddFloatField(body, "Grab Angle", hitbox.grabAngle, value =>
            {
                RecordUndo("Edit Grab Angle");
                hitbox.grabAngle = value;
                MarkDirty();
            });
            AddToggle(body, "Can Grab Air", hitbox.canGrabAirborne, value =>
            {
                RecordUndo("Edit Can Grab Air");
                hitbox.canGrabAirborne = value;
                MarkDirty();
            });

            AddSeparator(body, "Grab Reaction");
            AddIntField(body, "Impact Freeze", hitbox.impactFreezeFrames, value =>
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
            AddVector2Field(body, "Snap Offset", hitbox.snapOffset, value =>
            {
                RecordUndo("Edit Grab Snap Offset");
                hitbox.snapOffset = value;
                MarkDirty();
            });
            AddEnumField(body, "Impact Tier", hitbox.impactTier, value =>
            {
                RecordUndo("Edit Grab Impact Tier");
                hitbox.impactTier = (DamageImpactTier)value;
                MarkDirty();
            });
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

            AddSeparator(body, "Branching (Nested Step)");
            AddToggle(body, "Branch On Success", hitbox.branchOnSuccess, value =>
            {
                RecordUndo("Edit Branch On Success");
                hitbox.branchOnSuccess = value;
                MarkDirtyAndRebuild();
            });

            if (hitbox.branchOnSuccess)
            {
                if (hitbox.successSteps == null)
                    hitbox.successSteps = new List<AttackStepData>();

                VisualElement stepList = new VisualElement();
                body.Add(stepList);

                for (int i = 0; i < hitbox.successSteps.Count; i++)
                {
                    int stepIdx = i;
                    AttackStepData step = hitbox.successSteps[i];
                    string stepName = step != null && !string.IsNullOrEmpty(step.stepName) ? step.stepName : "Untitled";

                    BuildFoldoutCard($"Success Step {stepIdx + 1}: {stepName}", $"grab_success_{hitbox.GetHashCode()}_{stepIdx}", new Color(1f, 0.45f, 0.15f), inner =>
                    {
                        inner.Add(new HelpBox("To edit this step completely, select it from the left action tree sidebar.", HelpBoxMessageType.Info));
                        
                        // We give them just the name field here so they can rename it from the parent card
                        if (step != null)
                        {
                            AddTextField(inner, "Step Name", step.stepName, value => 
                            {
                                RecordUndo("Rename Success Step");
                                step.stepName = value;
                                // Need full rebuild to update sidebar and parent card titles
                                onStructureChanged?.Invoke(); 
                            });
                        }
                    }, () =>
                    {
                        RecordUndo("Delete Success Step");
                        hitbox.successSteps.RemoveAt(stepIdx);
                        MarkDirtyAndRebuild();
                    }, isSubCard: true, parent: stepList, onReorder: newIndex =>
                    {
                        ReorderListItem(hitbox.successSteps, stepIdx, newIndex, "Reorder Success Step");
                    }, reorderIndex: stepIdx, reorderCount: hitbox.successSteps.Count);
                }

                Button addStepBtn = new Button(() =>
                {
                    RecordUndo("Add Success Step");
                    
                    // Create a completely NEW instance! 
                    // This avoids Unity's default inspector duplicate behavior which shallow copies [SerializeReference] fields.
                    var newStep = new AttackStepData
                    {
                        stepName = "Grab Success",
                        motionData = new MotionFrameData(),
                        vfxData = new VFXFrameData(),
                        hitData = new HitFrameData()
                    };
                    
                    hitbox.successSteps.Add(newStep);
                    MarkDirtyAndRebuild();
                })
                {
                    text = "Add Success Step"
                };
                addStepBtn.AddToClassList("component-add-entry-btn");
                body.Add(addStepBtn);
            }
        }

        private void BuildProjectileHitboxFields(VisualElement body, ProjectileHitbox hitbox)
        {
            AddSeparator(body, "Projectile");
            AddObjectField(body, "Prefab", hitbox.projectilePrefab, value =>
            {
                RecordUndo("Edit Projectile Prefab");
                hitbox.projectilePrefab = value;
                MarkDirty();
            });
            AddVector3Field(body, "Spawn Point", hitbox.spawnPoint, value =>
            {
                RecordUndo("Edit Spawn Point");
                hitbox.spawnPoint = value;
                MarkDirty();
            });

            AddSeparator(body, "Trajectory");
            AddFloatField(body, "Speed", hitbox.trajectory.speed, value =>
            {
                RecordUndo("Edit Projectile Speed");
                hitbox.trajectory.speed = value;
                MarkDirty();
            });
            AddFloatField(body, "Arc", hitbox.trajectory.arc, value =>
            {
                RecordUndo("Edit Projectile Arc");
                hitbox.trajectory.arc = value;
                MarkDirty();
            });
            AddFloatField(body, "Gravity", hitbox.trajectory.gravity, value =>
            {
                RecordUndo("Edit Projectile Gravity");
                hitbox.trajectory.gravity = value;
                MarkDirty();
            });

            AddSeparator(body, "Projectile Stats");
            AddFloatField(body, "Damage", hitbox.stats.damage, value =>
            {
                RecordUndo("Edit Projectile Damage");
                hitbox.stats.damage = value;
                MarkDirty();
            });
            AddIntField(body, "Hit Count", hitbox.stats.hitCount, value =>
            {
                RecordUndo("Edit Projectile Hit Count");
                hitbox.stats.hitCount = value;
                MarkDirty();
            });
            AddFloatField(body, "Lifespan", hitbox.stats.lifespan, value =>
            {
                RecordUndo("Edit Projectile Lifespan");
                hitbox.stats.lifespan = value;
                MarkDirty();
            });
            AddEnumField(body, "Impact Tier", hitbox.stats.impactTier, value =>
            {
                RecordUndo("Edit Projectile Impact Tier");
                hitbox.stats.impactTier = (DamageImpactTier)value;
                MarkDirty();
            });
            AddToggle(body, "Use Deterministic Reaction", hitbox.stats.useDeterministicReaction, value =>
            {
                RecordUndo("Toggle Projectile Deterministic Reaction");
                if (value && !hitbox.stats.useDeterministicReaction)
                {
                    hitbox.stats.reactionKind = HitReactionData.InferLegacyReactionKind(hitbox.stats.reactionVelocity, false);
                    hitbox.stats.impactFreezeFrames = hitbox.stats.hitstunFrames;
                    hitbox.stats.reactionFrames = hitbox.stats.knockbackFrames;
                    hitbox.stats.recoveryFrames = Mathf.Max(1, hitbox.stats.recoveryFrames);
                    hitbox.stats.horizontalSpeed = Mathf.Abs(hitbox.stats.reactionVelocity.x);
                    hitbox.stats.launchSpeed = Mathf.Max(0f, hitbox.stats.reactionVelocity.y);
                    hitbox.stats.landingBehavior = HitReactionData.InferLandingBehavior(hitbox.stats.reactionVelocity, false);
                    hitbox.stats.canAirJuggle = hitbox.stats.reactionVelocity.y > 0.01f;
                    hitbox.stats.maxJuggleCount = hitbox.stats.canAirJuggle ? Mathf.Max(1, hitbox.stats.maxJuggleCount) : 0;
                }

                hitbox.stats.useDeterministicReaction = value;
                MarkDirtyAndRebuild();
            });

            if (hitbox.stats.useDeterministicReaction)
            {
                bool usesHorizontalSpeed = HitReactionData.UsesHorizontalSpeed(hitbox.stats.reactionKind);
                bool usesLaunchSpeed = HitReactionData.UsesLaunchSpeed(hitbox.stats.reactionKind);
                bool usesLandingBehavior = HitReactionData.UsesLandingBehavior(hitbox.stats.reactionKind);
                bool usesAirJuggle = HitReactionData.UsesAirJuggleSettings(hitbox.stats.reactionKind);

                AddEnumField(body, "Reaction Kind", hitbox.stats.reactionKind, value =>
                {
                    RecordUndo("Edit Projectile Reaction Kind");
                    hitbox.stats.reactionKind = (EnemyReactionKind)value;
                    MarkDirtyAndRebuild();
                });
                AddIntField(body, "HitStop / Impact Freeze", hitbox.stats.impactFreezeFrames, value =>
                {
                    RecordUndo("Edit Projectile Impact Freeze");
                    hitbox.stats.impactFreezeFrames = value;
                    MarkDirty();
                });
                AddIntField(body, "Reaction Frames", hitbox.stats.reactionFrames, value =>
                {
                    RecordUndo("Edit Projectile Reaction Frames");
                    hitbox.stats.reactionFrames = value;
                    MarkDirty();
                });
                AddIntField(body, "Recovery Frames", hitbox.stats.recoveryFrames, value =>
                {
                    RecordUndo("Edit Projectile Recovery Frames");
                    hitbox.stats.recoveryFrames = value;
                    MarkDirty();
                });

                if (usesHorizontalSpeed)
                {
                    AddFloatField(body, "Horizontal Speed", hitbox.stats.horizontalSpeed, value =>
                    {
                        RecordUndo("Edit Projectile Horizontal Speed");
                        hitbox.stats.horizontalSpeed = value;
                        MarkDirty();
                    });
                }

                if (usesLaunchSpeed)
                {
                    AddFloatField(body, "Launch Up", hitbox.stats.launchSpeed, value =>
                    {
                        RecordUndo("Edit Projectile Launch Speed");
                        hitbox.stats.launchSpeed = value;
                        MarkDirty();
                    });
                }

                if (usesLandingBehavior)
                {
                    AddEnumField(body, "Landing Behavior", hitbox.stats.landingBehavior, value =>
                    {
                        RecordUndo("Edit Projectile Landing Behavior");
                        hitbox.stats.landingBehavior = (EnemyLandingBehavior)value;
                        MarkDirty();
                    });
                }

                if (usesAirJuggle)
                {
                    AddToggle(body, "Can Air Juggle", hitbox.stats.canAirJuggle, value =>
                    {
                        RecordUndo("Edit Projectile Can Air Juggle");
                        hitbox.stats.canAirJuggle = value;
                        MarkDirtyAndRebuild();
                    });

                    if (hitbox.stats.canAirJuggle)
                    {
                        AddIntField(body, "Max Juggles", hitbox.stats.maxJuggleCount, value =>
                        {
                            RecordUndo("Edit Projectile Max Juggles");
                            hitbox.stats.maxJuggleCount = value;
                            MarkDirty();
                        });
                        AddFloatField(body, "Juggle Launch Scale", hitbox.stats.juggleLaunchScale, value =>
                        {
                            RecordUndo("Edit Projectile Juggle Launch Scale");
                            hitbox.stats.juggleLaunchScale = value;
                            MarkDirty();
                        });
                        AddFloatField(body, "Juggle Stun Scale", hitbox.stats.juggleStunScale, value =>
                        {
                            RecordUndo("Edit Projectile Juggle Stun Scale");
                            hitbox.stats.juggleStunScale = value;
                            MarkDirty();
                        });
                    }
                }
            }
            else
            {
                body.Add(new HelpBox(
                    "Legacy projectile reaction velocity mode is active. Enable deterministic reaction to author projectile knockback and launch timing directly.",
                    HelpBoxMessageType.Info));

                AddIntField(body, "Hitstun", hitbox.stats.hitstunFrames, value =>
                {
                    RecordUndo("Edit Projectile Hitstun");
                    hitbox.stats.hitstunFrames = value;
                    MarkDirty();
                });
                AddVector2Field(body, "Reaction Velocity", hitbox.stats.reactionVelocity, value =>
                {
                    RecordUndo("Edit Projectile Reaction Velocity");
                    hitbox.stats.reactionVelocity = value;
                    MarkDirty();
                });
                AddIntField(body, "Knockback Frames", hitbox.stats.knockbackFrames, value =>
                {
                    RecordUndo("Edit Projectile Knockback Frames");
                    hitbox.stats.knockbackFrames = value;
                    MarkDirty();
                });
            }

            AddColorField(body, "Flash Color", hitbox.stats.flashColor, value =>
            {
                RecordUndo("Edit Projectile Flash Color");
                hitbox.stats.flashColor = value;
                MarkDirty();
            });
            AddIntField(body, "Flash Frames", hitbox.stats.flashFrames, value =>
            {
                RecordUndo("Edit Projectile Flash Frames");
                hitbox.stats.flashFrames = value;
                MarkDirty();
            });
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
                else
                {
                    menu.AddDisabledItem(new GUIContent("Motion Frame Data (added)"));
                }

                if (step.vfxData == null)
                {
                    menu.AddItem(new GUIContent("VFX Frame Data"), false, () =>
                    {
                        RecordUndo("Add VFX");
                        step.vfxData = new VFXFrameData();
                        MarkDirtyAndRebuild();
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("VFX Frame Data (added)"));
                }

                if (step.hitData == null)
                {
                    menu.AddItem(new GUIContent("Hitbox Data"), false, () =>
                    {
                        RecordUndo("Add Hit Data");
                        step.hitData = new HitFrameData();
                        MarkDirtyAndRebuild();
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Hitbox Data (added)"));
                }

                menu.ShowAsContext();
            })
            {
                text = "Add Component"
            };
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
                    hitData.hitboxes.Add(new NormalHitbox { useDeterministicReaction = true });
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Grab Hitbox"), false, () =>
                {
                    RecordUndo("Add Grab Hitbox");
                    hitData.hitboxes.Add(new GrabHitbox());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Projectile Hitbox"), false, () =>
                {
                    RecordUndo("Add Projectile Hitbox");
                    hitData.hitboxes.Add(new ProjectileHitbox
                    {
                        stats = new ProjectileStats { useDeterministicReaction = true }
                    });
                    MarkDirtyAndRebuild();
                });
                menu.ShowAsContext();
            })
            {
                text = "Add Hitbox"
            };
            addButton.AddToClassList("component-add-entry-btn");
            body.Add(addButton);
        }

        private void BuildDashCards(DashData dash)
        {
            if (dash == null)
            {
                return;
            }

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
            if (!foldoutState.ContainsKey(foldoutKey))
            {
                foldoutState[foldoutKey] = true;
            }

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
                Button deleteButton = new Button(onDelete)
                {
                    text = "X"
                };
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
                if (evt.target is Button)
                {
                    return;
                }

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
            if (!foldoutState.ContainsKey(foldoutKey))
            {
                foldoutState[foldoutKey] = true;
            }

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
                Button deleteButton = new Button(onDelete)
                {
                    text = "X"
                };
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
                if (evt.target is Button)
                {
                    return;
                }

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
            TextField field = new TextField(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddIntField(VisualElement parent, string label, int value, Action<int> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            IntegerField field = new IntegerField(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddFloatField(VisualElement parent, string label, float value, Action<float> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            FloatField field = new FloatField(label) { value = value };
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
            ObjectField field = new ObjectField(label)
            {
                objectType = typeof(T),
                value = value
            };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue as T));
            row.Add(field);
            parent.Add(row);
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

            slider.RegisterValueChangedCallback(evt =>
            {
                int newStartup = Mathf.RoundToInt(evt.newValue.x);
                int newActive = Mathf.RoundToInt(evt.newValue.y) - newStartup;
                int newRecovery = total - newStartup - newActive;

                startupField.SetValueWithoutNotify(newStartup);
                activeField.SetValueWithoutNotify(newActive);
                recoveryField.SetValueWithoutNotify(newRecovery);

                onChanges?.Invoke(Mathf.Max(0, newStartup), Mathf.Max(0, newActive), Mathf.Max(0, newRecovery));
            });

            parent.Add(slider);

            startupField.RegisterValueChangedCallback(evt =>
            {
                int value = evt.newValue;
                slider.SetValueWithoutNotify(new Vector2(value, value + activeField.value));
                onChanges?.Invoke(value, activeField.value, recoveryField.value);
            });
            fields.Add(startupField);

            activeField.RegisterValueChangedCallback(evt =>
            {
                int value = evt.newValue;
                slider.SetValueWithoutNotify(new Vector2(startupField.value, startupField.value + value));
                onChanges?.Invoke(startupField.value, value, recoveryField.value);
            });
            fields.Add(activeField);

            recoveryField.RegisterValueChangedCallback(evt =>
            {
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
            if (list == null || fromIndex < 0 || fromIndex >= list.Count)
            {
                return;
            }

            int clampedIndex = Mathf.Clamp(toIndex, 0, list.Count - 1);
            if (fromIndex == clampedIndex)
            {
                return;
            }

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

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnPointerDown);
                target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                target.RegisterCallback<PointerUpEvent>(OnPointerUp);
                target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                if (evt.button != 0 || activePointerId != -1)
                {
                    return;
                }

                activePointerId = evt.pointerId;
                startPointerY = evt.position.y;
                startCardTop = card.layout.y;
                target.CapturePointer(activePointerId);
                evt.StopImmediatePropagation();
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (evt.pointerId != activePointerId)
                {
                    return;
                }

                float deltaY = evt.position.y - startPointerY;
                if (!isDragging)
                {
                    if (Mathf.Abs(deltaY) < DragThreshold)
                    {
                        return;
                    }

                    BeginDrag();
                }

                card.style.top = startCardTop + deltaY;
                int newIndex = GetInsertionIndex(evt.position.y);
                if (listContainer.Contains(placeholder) && listContainer.IndexOf(placeholder) != newIndex)
                {
                    placeholder.RemoveFromHierarchy();
                    listContainer.Insert(newIndex, placeholder);
                }

                evt.StopPropagation();
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                if (evt.pointerId != activePointerId)
                {
                    return;
                }

                FinishDrag();
                evt.StopPropagation();
            }

            private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
            {
                if (activePointerId == -1)
                {
                    return;
                }

                FinishDrag();
            }

            private void BeginDrag()
            {
                isDragging = true;
                placeholder.style.height = card.resolvedStyle.height;
                placeholder.style.marginTop = card.resolvedStyle.marginTop;
                placeholder.style.marginBottom = card.resolvedStyle.marginBottom;
                placeholder.style.marginLeft = card.resolvedStyle.marginLeft;
                placeholder.style.marginRight = card.resolvedStyle.marginRight;

                card.RemoveFromHierarchy();
                listContainer.Insert(originalIndex, placeholder);
                listContainer.Add(card);

                card.AddToClassList("property-card--dragging");
                card.style.position = Position.Absolute;
                card.style.left = 0f;
                card.style.right = 0f;
                card.style.top = startCardTop;
                card.BringToFront();
            }

            private void FinishDrag()
            {
                int pointerId = activePointerId;
                activePointerId = -1;

                if (pointerId != -1 && target.HasPointerCapture(pointerId))
                {
                    target.ReleasePointer(pointerId);
                }

                if (isDragging)
                {
                    int finalIndex = listContainer.IndexOf(placeholder);
                    placeholder.RemoveFromHierarchy();

                    if (listContainer.Contains(card))
                    {
                        card.RemoveFromHierarchy();
                    }

                    card.RemoveFromClassList("property-card--dragging");
                    card.style.position = Position.Relative;
                    card.style.left = new StyleLength(StyleKeyword.Null);
                    card.style.right = new StyleLength(StyleKeyword.Null);
                    card.style.top = new StyleLength(StyleKeyword.Null);

                    listContainer.Insert(Mathf.Clamp(finalIndex, 0, listContainer.childCount), card);
                    if (finalIndex != originalIndex)
                    {
                        onReorder?.Invoke(finalIndex);
                    }
                }
                isDragging = false;
            }

            private int GetInsertionIndex(float pointerY)
            {
                int index = 0;
                foreach (VisualElement child in listContainer.Children())
                {
                    if (child == card || child == placeholder)
                    {
                        continue;
                    }

                    if (pointerY > child.worldBound.center.y)
                    {
                        index++;
                    }
                }

                return index;
            }
        }

        private void RecordUndo(string action)
        {
            if (character != null)
            {
                Undo.RecordObject(character, action);
            }
        }

        private void MarkDirty()
        {
            if (character != null)
            {
                EditorUtility.SetDirty(character);
            }

            onDataChanged?.Invoke();
        }

        private void MarkDirtyAndRebuild()
        {
            if (character != null)
            {
                EditorUtility.SetDirty(character);
            }

            if (onStructureChanged != null && currentNode != null)
            {
                onStructureChanged.Invoke();
                return;
            }

            onDataChanged?.Invoke();
        }
    }
}
