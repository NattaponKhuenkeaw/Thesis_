using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace CombatEditor.UI
{
    /// <summary>
    /// Represents a selectable action item in the sidebar tree.
    /// </summary>
    public class ActionNode
    {
        public string Label;
        public string Category;      // e.g., "Ground Combo"
        public int StepIndex;        // -1 for non-step items
        public string DataType;      // "AttackStep", "Dash", "Skill", "Ultimate"
        public object DataRef;       // Direct reference to data object

        public ActionNode(string label, string category, int stepIndex, string dataType, object dataRef)
        {
            Label = label;
            Category = category;
            StepIndex = stepIndex;
            DataType = dataType;
            DataRef = dataRef;
        }
    }

    /// <summary>
    /// Builds and manages the sidebar action tree from a CharacterObject.
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

            // ── Ground Combo ──
            if (character.basicAttackComboData?.steps != null && character.basicAttackComboData.steps.Length > 0)
            {
                BuildCategory("⚔ Ground Combo", "Ground", character.basicAttackComboData.steps);
            }

            // ── Air Combo ──
            if (character.neutralAirComboData?.steps != null && character.neutralAirComboData.steps.Length > 0)
            {
                BuildCategory("✈ Air Combo", "Air", character.neutralAirComboData.steps);
            }

            // ── Plunge ──
            if (character.plungeAttackData?.impactStep != null)
            {
                BuildSingleCategory("💫 Plunge Impact", "Plunge", 
                    character.plungeAttackData.impactStep.stepName ?? "Impact",
                    "AttackStep", character.plungeAttackData.impactStep);
            }

            // ── Dash ──
            BuildSingleCategory("🎯 Dash", "Dash", "Dash Move", "Dash", character.dashData);

            // ── Skill ──
            string skillLabel = !string.IsNullOrEmpty(character.skillData?.skillName) ? character.skillData.skillName : "Elemental Skill";
            BuildSingleCategory("🌀 Skill", "Skill", skillLabel, "Skill", character.skillData);

            // ── Ultimate ──
            string ultLabel = !string.IsNullOrEmpty(character.ultimateData?.ultimateName) ? character.ultimateData.ultimateName : "Elemental Burst";
            BuildSingleCategory("⚡ Ultimate", "Ultimate", ultLabel, "Ultimate", character.ultimateData);

            // Auto-select first item
            if (selectedNode == null && leafElements.Count > 0)
                SelectNode(leafElements[0].node);
        }

        private void BuildCategory(string title, string category, AttackStepData[] steps)
        {
            if (!expandedState.ContainsKey(category)) expandedState[category] = true;

            var catRoot = new VisualElement();
            catRoot.AddToClassList("sidebar-category");

            // Header
            var header = new VisualElement();
            header.AddToClassList("sidebar-category__header");

            var arrow = new Label(expandedState[category] ? "▾" : "►");
            arrow.AddToClassList("sidebar-category__arrow");
            header.Add(arrow);

            var label = new Label(title);
            label.AddToClassList("sidebar-category__label");
            header.Add(label);

            string cat = category; // capture for closure
            header.RegisterCallback<ClickEvent>(evt =>
            {
                expandedState[cat] = !expandedState[cat];
                Build(character); // rebuild
            });

            catRoot.Add(header);

            // Items container
            if (expandedState[category])
            {
                var itemsContainer = new VisualElement();
                itemsContainer.AddToClassList("sidebar-items");

                for (int i = 0; i < steps.Length; i++)
                {
                    string stepName = steps[i].stepName ?? "Untitled";
                    string itemLabel = $"Step {i + 1}: {stepName}";
                    var node = new ActionNode(itemLabel, category, i, "AttackStep", steps[i]);

                    var item = CreateLeafItem(node);
                    itemsContainer.Add(item);
                }

                catRoot.Add(itemsContainer);
            }

            container.Add(catRoot);
        }

        private void BuildSingleCategory(string title, string category, string itemLabel, string dataType, object data)
        {
            if (!expandedState.ContainsKey(category)) expandedState[category] = true;

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

            string cat = category;
            header.RegisterCallback<ClickEvent>(evt =>
            {
                expandedState[cat] = !expandedState[cat];
                Build(character);
            });

            catRoot.Add(header);

            if (expandedState[category])
            {
                var itemsContainer = new VisualElement();
                itemsContainer.AddToClassList("sidebar-items");

                var node = new ActionNode(itemLabel, category, 0, dataType, data);
                var item = CreateLeafItem(node);
                itemsContainer.Add(item);

                catRoot.Add(itemsContainer);
            }

            container.Add(catRoot);
        }

        private VisualElement CreateLeafItem(ActionNode node)
        {
            var item = new VisualElement();
            item.AddToClassList("sidebar-item");

            if (selectedNode != null && selectedNode.Label == node.Label && selectedNode.Category == node.Category)
                item.AddToClassList("sidebar-item--active");

            var lbl = new Label(node.Label);
            lbl.AddToClassList("sidebar-item__label");
            item.Add(lbl);

            var capturedNode = node;
            item.RegisterCallback<ClickEvent>(evt =>
            {
                SelectNode(capturedNode);
            });

            leafElements.Add((item, node));
            return item;
        }

        public void SelectNode(ActionNode node)
        {
            selectedNode = node;

            // Update visual selection
            foreach (var (el, n) in leafElements)
            {
                if (n == node)
                    el.AddToClassList("sidebar-item--active");
                else
                    el.RemoveFromClassList("sidebar-item--active");
            }

            OnSelectionChanged?.Invoke(node);
        }
    }
}
