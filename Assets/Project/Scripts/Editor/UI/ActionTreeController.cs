using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEditor;

namespace CombatEditor.UI
{
    /// <summary>
    /// Represents a selectable action item in the sidebar tree.
    /// Supports hierarchical nesting for combo trees.
    /// </summary>
    public class ActionNode
    {
        public string Label;
        public string Category;      // e.g., "Ground", "Air", "Dash"
        public string DataType;      // "AttackStep", "Dash", "Skill", "Ultimate", "ComboNode"
        public object DataRef;       // Direct reference to data object (AttackStepData, DashData, or ComboNode)
        public int Depth;            // For visual indentation
        public System.Action DeleteCallback; // Callback to remove this node from its parent

        public ActionNode(string label, string category, string dataType, object dataRef, int depth = 0, System.Action deleteCallback = null)
        {
            Label = label;
            Category = category;
            DataType = dataType;
            DataRef = dataRef;
            Depth = depth;
            DeleteCallback = deleteCallback;
        }

        // Helper to get step data regardless of wrapper
        public AttackStepData GetStepData()
        {
            if (DataRef is AttackStepData step) return step;
            if (DataRef is ComboNode node) return node.stepData;
            return null;
        }
    }

    /// <summary>
    /// Builds and manages the sidebar action tree from a CharacterObject.
    /// Prioritizes ActionResolverTable if assigned.
    /// </summary>
    public class ActionTreeController
    {
        private VisualElement container;
        private CharacterObject character;
        private ActionNode selectedNode;

        public event System.Action<ActionNode> OnSelectionChanged;

        // Category collapse state
        private Dictionary<string, bool> expandedState = new Dictionary<string, bool>();

        // All leaf items for lookup
        private List<(VisualElement element, ActionNode node)> leafElements = new();

        public ActionTreeController(VisualElement sidebarContainer)
        {
            container = sidebarContainer;
        }

        public ActionNode SelectedNode => selectedNode;

        public void Build(CharacterObject character)
        {
            this.character = character;
            container.Clear();
            leafElements.Clear();

            if (character == null) return;

            // 0. Resolver Table Settings
            if (character.actionResolverTable != null)
            {
                BuildSingleCategory("⚁EResolver Settings", "Settings", "Global Settings", "ResolverTable", character.actionResolverTable);
                BuildResolverTableCategories(character.actionResolverTable);
            }

            BuildSingleCategory("🎯 Dash", "Dash", "Dash Move", "Dash", character.DashData);
            if (character.actionResolverTable?.qteActions?.Count is not > 0 && character.qteSwap?.enabled == true
                && character.qteSwap.TryResolveIntro(character.actionResolverTable, out _, out var introStep))
            {
                const string category = "QteIntro";
                if (!expandedState.ContainsKey(category)) expandedState[category] = true;
                var qteRoot = BuildCategoryHeader("QTE Entry Combo", category, () => Build(character));
                if (expandedState[category])
                {
                    var items = new VisualElement(); items.AddToClassList("sidebar-items");
                    ComboNode introRoot = character.qteSwap.introComboTree?.rootNode;
                    if (introRoot != null)
                        BuildTreeNodeRecursive(items, introRoot, category, 0, null, null, new HashSet<ComboNode>());
                    else
                        items.Add(CreateLeafItem(new ActionNode(introStep.stepName, category, "AttackStep", introStep)));
                    qteRoot.Add(items);
                }
                container.Add(qteRoot);
            }

            // Auto-select first item if current selection is lost
            if (selectedNode == null && leafElements.Count > 0)
                SelectNode(leafElements[0].node);
        }

        private void BuildResolverTableCategories(ActionResolverTable table)
        {
            BuildActionsForList("QTE Entry", "QTE", table.qteActions, table, AttackCategory.Normal);
            BuildActionsForList("⚁ECombo", "Combo", table.comboActions, table, AttackCategory.Normal);
            BuildActionsForList("🌀 Skill", "Skill", table.skillActions, table, AttackCategory.Skill);
            BuildActionsForList("⚡ Ultimate", "Ultimate", table.ultimateActions, table, AttackCategory.Ultimate);
            BuildActionsForList("💫 Contextual", "Standalone", table.standaloneActions, table, AttackCategory.Normal);
        }

        private void BuildActionsForList<T>(string titlePrefix, string categoryPrefix, List<T> actions, ActionResolverTable table, AttackCategory cat) where T : CombatAction, new()
        {
            if (actions != null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    string displayLabel = (action is StandaloneAction sa && sa.isPlungeAction) ? "Plunge Attack" : action.actionName;
                    string title = $"{titlePrefix} {displayLabel} ({action.context} \u2192 {action.outputContext})";
                    string category = $"{categoryPrefix}_{i}";

                    System.Action deleteAction = () => {
                        Undo.RecordObject(table, $"Delete {categoryPrefix} Action");
                        actions.Remove(action);
                        EditorUtility.SetDirty(table);
                        if (selectedNode != null && (selectedNode.DataRef == action || selectedNode.Category == category))
                            selectedNode = null;
                        Build(character);
                    };

                    BuildTreeCategory(title, category, action.comboTree?.rootNode, table, action, deleteAction);
                }
            }

            var addBtn = new Button(() => 
            {
                Undo.RecordObject(table, $"Add {categoryPrefix} Action");
                var newAction = new T {
                    actionName = $"New {categoryPrefix}",
                    context = CombatActionContext.Ground
                };
                actions.Add(newAction);
                EditorUtility.SetDirty(table);
                Build(character);
            }) { text = $"+ Add {categoryPrefix} Action" };
            addBtn.AddToClassList("component-add-entry-btn");
            addBtn.style.marginTop = 5;
            addBtn.style.marginBottom = 10;
            container.Add(addBtn);
        }


        private void BuildTreeCategory(string title, string category, ComboNode root, ActionResolverTable table, CombatAction action, System.Action deleteAction)
        {
            if (!expandedState.ContainsKey(category)) expandedState[category] = true;

            var catRoot = BuildCategoryHeader(title, category, () => Build(character));
            
            if (expandedState[category])
            {
                var itemsContainer = new VisualElement();
                itemsContainer.AddToClassList("sidebar-items");

                System.Action deleteRoot = () => {
                    Undo.RecordObject(table, $"Delete Root Step from {category}");
                    if (action.comboTree != null) action.comboTree.rootNode = null;
                    EditorUtility.SetDirty(table);
                    Build(character);
                };
                
                /* Removed internal deleteAction declaration */

                var settingsNode = new ActionNode($"⚁E{action.actionName} setup", category, "CombatAction", action, 0, deleteAction);
                itemsContainer.Add(CreateLeafItem(settingsNode));

                if (root != null)
                {
                    BuildTreeNodeRecursive(itemsContainer, root, category, 0, null, deleteRoot, new HashSet<ComboNode>());
                }
                else
                {
                    var emptyLabel = new Label("Empty Tree");
                    emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    emptyLabel.style.opacity = 0.5f;
                    emptyLabel.style.marginLeft = 20;
                    itemsContainer.Add(emptyLabel);

                    // Add Node Button
                    var addBtn = new Button(() => 
                    {
                        Undo.RecordObject(table, $"Add Root Step to {category}");
                        var newNode = new ComboNode { stepData = new AttackStepData { stepName = "New Entry" } };
                        
                        if (action.comboTree == null) action.comboTree = new ComboTree();
                        action.comboTree.rootNode = newNode;
                        
                        EditorUtility.SetDirty(table);
                        Build(character);
                    }) { text = "+ Set Root Step" };
                    addBtn.AddToClassList("component-add-entry-btn");
                    addBtn.style.marginTop = 10;
                    itemsContainer.Add(addBtn);
                }

                // Add Sub Step button for extending the combo chain
                var addSubBtn = new Button(() => {
                    Undo.RecordObject(table, "Add Sub Step");
                    var newSubNode = new ComboNode { stepData = new AttackStepData { stepName = "New Sub Step" } };
                    // Find the last node in the defaultNext chain and append
                    ComboNode lastNode = root;
                    if (lastNode != null)
                    {
                        while (lastNode.defaultNext != null) lastNode = lastNode.defaultNext;
                        lastNode.defaultNext = newSubNode;
                    }
                    EditorUtility.SetDirty(table);
                    Build(character);
                }) { text = "+ New Sub Step" };
                addSubBtn.AddToClassList("component-add-entry-btn");
                addSubBtn.style.marginTop = 5;
                if (root != null) itemsContainer.Add(addSubBtn);

                catRoot.Add(itemsContainer);
            }
            container.Add(catRoot);
        }

        private void BuildTreeNodeRecursive(VisualElement parent, ComboNode node, string category, int depth, string prefix, System.Action deleteCallback, HashSet<ComboNode> visited)
        {
            if (node == null) return;
            UnityEngine.Object owner = category == "QteIntro" ? character : character.actionResolverTable;

            bool isVisited = visited.Contains(node);
            visited.Add(node);

            string label;
            if (prefix != null)
                label = $"↳ {prefix} {node.stepData?.stepName ?? "Step"}";
            else
                label = depth == 0 ? (node.stepData?.stepName ?? "Root") : $"↳ {node.stepData?.stepName ?? "Step"}";

            if (isVisited) label += " (Link)";

            var actionNode = new ActionNode(label, category, "ComboNode", node, depth, deleteCallback);
            
            var item = CreateLeafItem(actionNode);
            if (depth > 0) item.AddToClassList("sidebar-subitem");
            if (isVisited) item.style.opacity = 0.5f;

            parent.Add(item);

            if (isVisited) return;

            // 1. Follow linear path (defaultNext)
            if (node.defaultNext != null)
            {
                System.Action delDefaultNext = () => {
                    Undo.RecordObject(owner, "Delete Default Next");
                    node.defaultNext = null;
                    EditorUtility.SetDirty(owner);
                    Build(character);
                };
                BuildTreeNodeRecursive(parent, node.defaultNext, category, depth + 1, null, delDefaultNext, visited);
            }

            // 2. Follow grab success node
            if (node.onGrabSuccessNode != null && node.onGrabSuccessNode != node.defaultNext)
            {
                System.Action delGrabNext = () => {
                    Undo.RecordObject(owner, "Delete Grab Success Link");
                    node.onGrabSuccessNode = null;
                    EditorUtility.SetDirty(owner);
                    Build(character);
                };
                BuildTreeNodeRecursive(parent, node.onGrabSuccessNode, category, depth + 1, "(Grab)", delGrabNext, visited);
            }

            // 3. Follow branches (routes)
            if (node.routes != null)
            {
                for (int i = 0; i < node.routes.Count; i++)
                {
                    var route = node.routes[i];
                    if (route?.targetNode != null)
                    {
                        var capturedRoute = route;
                        System.Action delRouteTarget = () => {
                            Undo.RecordObject(owner, "Delete Route Target");
                            capturedRoute.targetNode = null;
                            EditorUtility.SetDirty(owner);
                            Build(character);
                        };
                        string routePrefix = route.requireGrabbedEnemy ? "(Grab Hit)" : "(Branch)";
                        BuildTreeNodeRecursive(parent, route.targetNode, category, depth + 1, routePrefix, delRouteTarget, visited);
                    }
                }
            }

            // 4. Follow automatic triggers (triggerWindows)
            if (node.stepData != null && node.stepData.triggerWindows != null)
            {
                for (int i = 0; i < node.stepData.triggerWindows.Count; i++)
                {
                    var trigger = node.stepData.triggerWindows[i];
                    if (trigger?.targetNode != null)
                    {
                        var capturedTrigger = trigger;
                        System.Action delTriggerTarget = () => {
                            Undo.RecordObject(owner, "Delete Trigger Target");
                            capturedTrigger.targetNode = null;
                            EditorUtility.SetDirty(owner);
                            Build(character);
                        };
                        BuildTreeNodeRecursive(parent, trigger.targetNode, category, depth + 1, "(Auto)", delTriggerTarget, visited);
                    }
                }
            }
        }

        private void BuildLegacyCategory(string title, string category, AttackStepData[] steps)
        {
            if (!expandedState.ContainsKey(category)) expandedState[category] = true;

            var catRoot = BuildCategoryHeader(title, category, () => Build(character));
            
            if (expandedState[category])
            {
                var itemsContainer = new VisualElement();
                itemsContainer.AddToClassList("sidebar-items");

                for (int i = 0; i < steps.Length; i++)
                {
                    string stepName = steps[i].stepName ?? "Untitled";
                    string itemLabel = $"Step {i + 1}: {stepName}";
                    var node = new ActionNode(itemLabel, category, "AttackStep", steps[i]);
                    itemsContainer.Add(CreateLeafItem(node));
                }
                catRoot.Add(itemsContainer);
            }
            container.Add(catRoot);
        }

        private void BuildSingleCategory(string title, string category, string itemLabel, string dataType, object data)
        {
            if (!expandedState.ContainsKey(category)) expandedState[category] = true;

            var catRoot = BuildCategoryHeader(title, category, () => Build(character));

            if (expandedState[category])
            {
                var itemsContainer = new VisualElement();
                itemsContainer.AddToClassList("sidebar-items");

                var node = new ActionNode(itemLabel, category, dataType, data);
                itemsContainer.Add(CreateLeafItem(node));

                catRoot.Add(itemsContainer);
            }
            container.Add(catRoot);
        }

        private VisualElement BuildCategoryHeader(string title, string category, System.Action onToggle)
        {
            var catRoot = new VisualElement();
            catRoot.AddToClassList("sidebar-category");

            var header = new VisualElement();
            header.AddToClassList("sidebar-category__header");

            var arrow = new Label(expandedState[category] ? "▾" : "►");
            arrow.AddToClassList("sidebar-category__arrow");
            header.Add(arrow);

            var label = new Label(title);
            label.AddToClassList("sidebar-category__label");
            header.Add(label);

            header.RegisterCallback<ClickEvent>(evt =>
            {
                expandedState[category] = !expandedState[category];
                onToggle?.Invoke();
            });

            catRoot.Add(header);
            return catRoot;
        }

        private VisualElement CreateLeafItem(ActionNode node)
        {
            var item = new VisualElement();
            item.AddToClassList("sidebar-item");

            // Indentation
            if (node.Depth > 0)
                item.style.paddingLeft = 16 + (node.Depth * 12);

            bool isActive = selectedNode != null && selectedNode.DataRef == node.DataRef && selectedNode.Category == node.Category;
            if (isActive)
                item.AddToClassList("sidebar-item--active");

            var label = new Label(node.Label);
            label.AddToClassList("sidebar-item__label");
            item.Add(label);

            item.RegisterCallback<ClickEvent>(evt => SelectNode(node));

            if (node.DeleteCallback != null || (node.DataType == "ComboNode" && node.DataRef is ComboNode))
            {
                item.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    if (node.DeleteCallback != null)
                    {
                        string menuLabel = node.DataType == "CombatAction" ? "Delete Action" :
                                           (node.Depth == 0 ? "Delete Root Node" : "Remove Link");
                        evt.menu.AppendAction(menuLabel, _ => node.DeleteCallback());
                    }

                    if (node.DataType == "ComboNode" && node.DataRef is ComboNode originalCombo)
                    {
                        evt.menu.AppendAction("Duplicate as Child", _ => DuplicateNodeAsChild(originalCombo));
                    }
                }));
            }

            leafElements.Add((item, node));
            return item;
        }

        public void SelectNodeByData(object data)
        {
            foreach (var pair in leafElements)
            {
                if (pair.node.DataRef == data)
                {
                    SelectNode(pair.node);
                    return;
                }
            }
        }

        public void SelectNode(ActionNode node)
        {
            selectedNode = node;

            // Update visual selection
            foreach (var (el, n) in leafElements)
            {
                if (n.DataRef == node.DataRef && n.Category == node.Category)
                    el.AddToClassList("sidebar-item--active");
                else
                    el.RemoveFromClassList("sidebar-item--active");
            }

            OnSelectionChanged?.Invoke(node);
        }

        private void DuplicateNodeAsChild(ComboNode originalCombo)
        {
            if (originalCombo == null || character == null || character.actionResolverTable == null) return;

            Undo.RecordObject(character.actionResolverTable, "Duplicate Step as Child");

            var newComboNode = new ComboNode();

            if (originalCombo.stepData != null)
            {
                var originalStep = originalCombo.stepData;
                var newStep = new AttackStepData
                {
                    stepName = originalStep.stepName + " (Copy)",
                    animationClip = originalStep.animationClip,
                    lockAirPosition = originalStep.lockAirPosition,
                    autoChainToNext = originalStep.autoChainToNext,
                    startupFrames = originalStep.startupFrames,
                    activeFrames = originalStep.activeFrames,
                    recoveryFrames = originalStep.recoveryFrames,
                    cancelWindowStartFrame = originalStep.cancelWindowStartFrame,
                    loopActivePhase = originalStep.loopActivePhase,
                    componentOrder = originalStep.componentOrder != null ? new List<string>(originalStep.componentOrder) : new List<string>()
                };

                if (originalStep.triggerWindows != null)
                {
                    newStep.triggerWindows = new List<TriggerWindow>();
                    foreach (var trigger in originalStep.triggerWindows)
                    {
                        newStep.triggerWindows.Add(new TriggerWindow
                        {
                            startFrame = trigger.startFrame,
                            endFrame = trigger.endFrame,
                            priority = trigger.priority,
                            targetNode = trigger.targetNode,
                            triggerConditions = trigger.triggerConditions != null ? new List<ActionConditionBase>(trigger.triggerConditions) : new List<ActionConditionBase>()
                        });
                    }
                }

                if (originalStep.motionData != null)
                {
                    newStep.motionData = JsonUtility.FromJson<MotionFrameData>(JsonUtility.ToJson(originalStep.motionData));
                }
                if (originalStep.vfxData != null)
                {
                    newStep.vfxData = JsonUtility.FromJson<VFXFrameData>(JsonUtility.ToJson(originalStep.vfxData));
                }
                if (originalStep.audioData != null)
                {
                    newStep.audioData = JsonUtility.FromJson<AudioFrameData>(JsonUtility.ToJson(originalStep.audioData));
                }
                if (originalStep.eventData != null)
                {
                    newStep.eventData = JsonUtility.FromJson<ActionEventFrameData>(JsonUtility.ToJson(originalStep.eventData));
                }
                if (originalStep.hitData != null)
                {
                    newStep.hitData = JsonUtility.FromJson<HitFrameData>(JsonUtility.ToJson(originalStep.hitData));
                    newStep.hitData.hitboxes = new List<HitboxBase>();
                    if (originalStep.hitData.hitboxes != null)
                    {
                        foreach (var hb in originalStep.hitData.hitboxes)
                        {
                            if (hb == null) continue;
                            var hbJson = JsonUtility.ToJson(hb);
                            if (hb is NormalHitbox)
                                newStep.hitData.hitboxes.Add(JsonUtility.FromJson<NormalHitbox>(hbJson));
                            else if (hb is GrabHitbox)
                                newStep.hitData.hitboxes.Add(JsonUtility.FromJson<GrabHitbox>(hbJson));
                            else if (hb is ProjectileSpawnHitbox)
                                newStep.hitData.hitboxes.Add(JsonUtility.FromJson<ProjectileSpawnHitbox>(hbJson));
                        }
                    }
                }

                newComboNode.stepData = newStep;
            }

            var oldNext = originalCombo.defaultNext;
            originalCombo.defaultNext = newComboNode;
            newComboNode.defaultNext = oldNext;

            EditorUtility.SetDirty(character.actionResolverTable);
            Build(character);
        }
    }
}
