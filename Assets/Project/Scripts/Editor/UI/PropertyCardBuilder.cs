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
        private readonly ActionTreeController treeController;

        public PropertyCardBuilder(VisualElement panelContainer, ActionTreeController treeController, Action onDataChanged, Action onStructureChanged)
        {
            container = panelContainer;
            this.treeController = treeController;
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
                case "ComboNode":
                    BuildComboNodeCards(node.DataRef as ComboNode);
                    break;
                case "Dash":
                    BuildDashCards(node.DataRef as DashData);
                    break;
                case "Skill":
                case "Ultimate":
                    // Skill and Ultimate are now handled as standard AttackStepData via the ResolveTree.
                    // This case is kept for category identification but expects AttackStepData in DataRef if any.
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
            });
        }

        private CombatAction GetCurrentAction()
        {
            if (character == null || character.actionResolverTable == null || currentNode == null) return null;
            string cat = currentNode.Category;
            if (string.IsNullOrEmpty(cat)) return null;

            int idx = -1;
            if (cat.StartsWith("Combo_") && int.TryParse(cat.Substring(6), out idx) && idx < character.actionResolverTable.comboActions.Count)
                return character.actionResolverTable.comboActions[idx];
            if (cat.StartsWith("Skill_") && int.TryParse(cat.Substring(6), out idx) && idx < character.actionResolverTable.skillActions.Count)
                return character.actionResolverTable.skillActions[idx];
            if (cat.StartsWith("Ultimate_") && int.TryParse(cat.Substring(9), out idx) && idx < character.actionResolverTable.ultimateActions.Count)
                return character.actionResolverTable.ultimateActions[idx];
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
                if (n.routes != null) {
                    foreach(var r in n.routes) Walk(r.targetNode);
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
                    }) { text = "→" };
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

                // Routes List
                AddSeparator(body, "Conditional Routes");
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

                    AddEnumField(routeBox, "Input", route.direction, val => {
                        RecordUndo("Change Route Input");
                        route.direction = (DirectionRequirement)val;
                        MarkDirty();
                    });

                    AddToggleField(routeBox, "Grounded Only", route.requireGrounded, val => {
                        RecordUndo("Change Route Grounded");
                        route.requireGrounded = val;
                        MarkDirty();
                    });

                    AddToggleField(routeBox, "Airborne Only", route.requireAirborne, val => {
                        RecordUndo("Change Route Airborne");
                        route.requireAirborne = val;
                        MarkDirty();
                    });

                    AddToggleField(routeBox, "Require Grabbed", route.requireGrabbedEnemy, val => {
                        RecordUndo("Change Route Grabbed");
                        route.requireGrabbedEnemy = val;
                        MarkDirty();
                    });

                    AddIntField(routeBox, "Priority", route.priority, val => {
                        RecordUndo("Change Route Priority");
                        route.priority = val;
                        MarkDirty();
                    });

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
                        }) { text = "→" };
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
            });
        }

        private void BuildResolveTableCards(ActionResolverTable table)
        {
            if (table == null) return;

            BuildFoldoutCard("Global Resolver Tools", "resolver_tools", new Color(0.2f, 0.7f, 0.4f), body =>
            {
                body.Add(new HelpBox("Data migration tools are no longer available for this table. All combat actions must be authored directly in the combo trees.", HelpBoxMessageType.Info));
            });
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
                    hitData.hitboxes.Add(new NormalHitbox());
                    MarkDirtyAndRebuild();
                });
                menu.AddItem(new GUIContent("Grab Hitbox"), false, () =>
                {
                    RecordUndo("Add Grab Hitbox");
                    hitData.hitboxes.Add(new GrabHitbox());
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

        private void AddToggleField(VisualElement parent, string label, bool value, Action<bool> onChange)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-field-row");
            Toggle field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
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
                if (character.actionResolverTable != null)
                    Undo.RecordObject(character.actionResolverTable, action);
            }
        }

        private void MarkDirty()
        {
            if (character != null)
            {
                EditorUtility.SetDirty(character);
                if (character.actionResolverTable != null)
                    EditorUtility.SetDirty(character.actionResolverTable);
            }

            onDataChanged?.Invoke();
        }

        private void MarkDirtyAndRebuild()
        {
            if (character != null)
            {
                EditorUtility.SetDirty(character);
                if (character.actionResolverTable != null)
                    EditorUtility.SetDirty(character.actionResolverTable);
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
