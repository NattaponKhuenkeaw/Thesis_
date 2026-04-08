using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace CombatEditor.UI
{
    /// <summary>
    /// Factory that builds property cards for the right panel based on selected data.
    /// </summary>
    public class PropertyCardBuilder
    {
        private VisualElement container;
        private CharacterObject character;
        private System.Action onDataChanged;

        public PropertyCardBuilder(VisualElement panelContainer, System.Action onDataChanged)
        {
            container = panelContainer;
            this.onDataChanged = onDataChanged;
        }

        public void SetCharacter(CharacterObject character)
        {
            this.character = character;
        }

        /// <summary>Clear and rebuild cards based on the selected action node.</summary>
        public void Build(ActionNode node)
        {
            container.Clear();

            if (node == null || character == null) return;

            switch (node.DataType)
            {
                case "AttackStep":
                    BuildAttackStepCards(node.DataRef as AttackStepData);
                    break;
                case "Dash":
                    BuildDashCards(node.DataRef as DashData);
                    break;
                case "Skill":
                    BuildPlaceholder("Skill data — edit in Inspector");
                    break;
                case "Ultimate":
                    BuildPlaceholder("Ultimate data — edit in Inspector");
                    break;
            }
        }

        // =========================================================
        // ATTACK STEP CARDS
        // =========================================================
        private void BuildAttackStepCards(AttackStepData step)
        {
            if (step == null) return;

            // ── Phase Config ──
            BuildCard("🎬 Phase Configuration", body =>
            {
                AddTextField(body, "Step Name", step.stepName, v =>
                {
                    RecordUndo("Edit Step Name");
                    step.stepName = v;
                    MarkDirty();
                });

                if (step.animationClip != null)
                {
                    int total = Mathf.RoundToInt(step.animationClip.length * 24f);
                    AddPhaseSlider(body, ref step.startupFrames, ref step.activeFrames, ref step.recoveryFrames, total);
                }
                else
                {
                    var helpBox = new HelpBox("Assign an Animation Clip to enable sync slider.", HelpBoxMessageType.Warning);
                    body.Add(helpBox);
                    AddIntField(body, "Startup", step.startupFrames, v => { RecordUndo("Edit Startup"); step.startupFrames = v; MarkDirty(); });
                    AddIntField(body, "Active", step.activeFrames, v => { RecordUndo("Edit Active"); step.activeFrames = v; MarkDirty(); });
                    AddIntField(body, "Recovery", step.recoveryFrames, v => { RecordUndo("Edit Recovery"); step.recoveryFrames = v; MarkDirty(); });
                }

                AddIntField(body, "Cancel Frame", step.cancelWindowStartFrame, v =>
                {
                    RecordUndo("Edit Cancel Frame");
                    step.cancelWindowStartFrame = v;
                    MarkDirty();
                });
            });

            // ── Forward Motion ──
            BuildCard("🏃 Forward Motion", body =>
            {
                AddToggle(body, "Enable", step.useForwardMotion, v =>
                {
                    RecordUndo("Toggle Motion");
                    step.useForwardMotion = v;
                    MarkDirty();
                });

                if (step.useForwardMotion)
                {
                    AddFloatField(body, "Distance", step.forwardDistance, v => { RecordUndo("Edit Motion Distance"); step.forwardDistance = v; MarkDirty(); });
                    AddIntField(body, "Start Frame", step.motionStartFrame, v => { RecordUndo("Edit Motion Start"); step.motionStartFrame = v; MarkDirty(); });
                    AddIntField(body, "End Frame", step.motionEndFrame, v => { RecordUndo("Edit Motion End"); step.motionEndFrame = v; MarkDirty(); });
                }
            });

            // ── Hit Frames ──
            if (step.hitFrames != null)
            {
                for (int i = 0; i < step.hitFrames.Length; i++)
                {
                    int idx = i; // capture
                    AttackHitFrameData h = step.hitFrames[i];
                    int hitStart = step.startupFrames + h.startFrameOffset;
                    int hitEnd = hitStart + h.activeFrames;

                    // Accordion Header
                    var header = new VisualElement();
                    header.AddToClassList("hitframe-header");

                    var accent = new VisualElement();
                    accent.AddToClassList("hitframe-header__accent");
                    accent.style.backgroundColor = h.debugColor;
                    header.Add(accent);

                    var text = new Label($"Impact #{idx + 1} — Frame {hitStart}→{hitEnd} ({h.activeFrames}f)  ●Dmg: {h.damageMultiplier:F1}x");
                    text.AddToClassList("hitframe-header__text");
                    header.Add(text);

                    var deleteBtn = new Button(() =>
                    {
                        RecordUndo("Delete Hit Frame");
                        var list = new List<AttackHitFrameData>(step.hitFrames);
                        list.RemoveAt(idx);
                        step.hitFrames = list.ToArray();
                        MarkDirty();
                        Build(new ActionNode("", "", 0, "AttackStep", step)); // rebuild
                    });
                    deleteBtn.text = "Delete";
                    deleteBtn.AddToClassList("hitframe-header__delete");
                    header.Add(deleteBtn);

                    container.Add(header);

                    // Hit frame detail cards
                    BuildHitFrameCards(h, idx);
                }
            }
        }

        private void BuildHitFrameCards(AttackHitFrameData h, int index)
        {
            // 📐 Hitbox Geometry
            BuildCard("📐 Hitbox Geometry", body =>
            {
                AddIntField(body, "Start Offset", h.startFrameOffset, v => { RecordUndo("Edit Hit Offset"); h.startFrameOffset = v; MarkDirty(); });
                AddIntField(body, "Duration", h.activeFrames, v => { RecordUndo("Edit Hit Duration"); h.activeFrames = v; MarkDirty(); });
                AddVector2Field(body, "Position", h.offset, v => { RecordUndo("Edit Hit Pos"); h.offset = v; MarkDirty(); });
                AddVector2Field(body, "Size", h.size, v => { RecordUndo("Edit Hit Size"); h.size = v; MarkDirty(); });
                AddColorField(body, "Debug Color", h.debugColor, v => { RecordUndo("Edit Debug Color"); h.debugColor = v; MarkDirty(); });
            });

            // ⚔ Damage & Knockback
            BuildCard("⚔ Damage & Knockback", body =>
            {
                AddFloatField(body, "Damage Scale", h.damageMultiplier, v => { RecordUndo("Edit Damage"); h.damageMultiplier = v; MarkDirty(); });
                AddFloatField(body, "Camera Shake", h.cameraShakeStrength, v => { RecordUndo("Edit Shake"); h.cameraShakeStrength = v; MarkDirty(); });
                AddFloatField(body, "Knockback Force", h.knockbackForce, v => { RecordUndo("Edit KB Force"); h.knockbackForce = v; MarkDirty(); });
                AddEnumField(body, "Impact Tier", h.impactTier, v => { RecordUndo("Edit Tier"); h.impactTier = (DamageImpactTier)v; MarkDirty(); });
            });

            // ❄ Hit Stop
            BuildCard("❄ Hit Stop & Stun", body =>
            {
                AddIntField(body, "Hitstop Freeze", h.hitstunFrames, v => { RecordUndo("Edit Hitstop"); h.hitstunFrames = v; MarkDirty(); });
                AddIntField(body, "Hitstun Duration", h.knockbackFrames, v => { RecordUndo("Edit Hitstun"); h.knockbackFrames = v; MarkDirty(); });
            });

            // 🎯 Enemy Reaction
            BuildCard("🎯 Enemy Reaction", body =>
            {
                AddVector2Field(body, "Reaction Velocity", h.reactionVelocity, v => { RecordUndo("Edit Velocity"); h.reactionVelocity = v; MarkDirty(); });
                AddToggle(body, "Is Knockdown", h.isKnockdown, v => { RecordUndo("Edit Knockdown"); h.isKnockdown = v; MarkDirty(); });

                AddSeparator(body, "Snap");
                AddToggle(body, "Snap To Attacker", h.snapToAttacker, v => { RecordUndo("Edit Snap"); h.snapToAttacker = v; MarkDirty(); });
                if (h.snapToAttacker)
                    AddVector2Field(body, "Snap Offset", h.snapOffset, v => { RecordUndo("Edit Snap Offset"); h.snapOffset = v; MarkDirty(); });

                AddSeparator(body, "Visual");
                AddColorField(body, "Flash Color", h.flashColor, v => { RecordUndo("Edit Flash Color"); h.flashColor = v; MarkDirty(); });
                AddIntField(body, "Flash Frames", h.flashFrames, v => { RecordUndo("Edit Flash Frames"); h.flashFrames = v; MarkDirty(); });
            });
        }

        // =========================================================
        // DASH CARDS
        // =========================================================
        private void BuildDashCards(DashData dash)
        {
            if (dash == null) return;

            BuildCard("⚡ Dash Configuration", body =>
            {
                if (dash.animationClip != null)
                {
                    int total = Mathf.RoundToInt(dash.animationClip.length * 24f);
                    AddPhaseSlider(body, ref dash.startupFrames, ref dash.activeFrames, ref dash.recoveryFrames, total);
                }
                else
                {
                    AddIntField(body, "Startup", dash.startupFrames, v => { RecordUndo("Edit Dash SU"); dash.startupFrames = v; MarkDirty(); });
                    AddIntField(body, "Active", dash.activeFrames, v => { RecordUndo("Edit Dash ACT"); dash.activeFrames = v; MarkDirty(); });
                    AddIntField(body, "Recovery", dash.recoveryFrames, v => { RecordUndo("Edit Dash REC"); dash.recoveryFrames = v; MarkDirty(); });
                }

                AddSeparator(body, "Physics");
                AddFloatField(body, "Dash Speed", dash.dashSpeed, v => { RecordUndo("Edit Dash Speed"); dash.dashSpeed = v; MarkDirty(); });
                AddIntField(body, "Cooldown Frames", dash.cooldownFrames, v => { RecordUndo("Edit Cooldown"); dash.cooldownFrames = v; MarkDirty(); });
            });
        }

        // =========================================================
        // PLACEHOLDER
        // =========================================================
        private void BuildPlaceholder(string message)
        {
            var empty = new VisualElement();
            empty.AddToClassList("empty-state");

            var text = new Label(message);
            text.AddToClassList("empty-state__text");
            empty.Add(text);

            container.Add(empty);
        }

        // =========================================================
        // CARD BUILDER
        // =========================================================
        private void BuildCard(string title, System.Action<VisualElement> buildBody)
        {
            var card = new VisualElement();
            card.AddToClassList("property-card");

            // Header
            var header = new VisualElement();
            header.AddToClassList("property-card__header");
            var headerText = new Label(title);
            headerText.AddToClassList("property-card__header-text");
            header.Add(headerText);
            card.Add(header);

            // Body
            var body = new VisualElement();
            body.AddToClassList("property-card__body");
            buildBody(body);
            card.Add(body);

            container.Add(card);
        }

        // =========================================================
        // FIELD HELPERS
        // =========================================================
        private void AddTextField(VisualElement parent, string label, string value, System.Action<string> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("card-field-row");
            var field = new TextField(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddIntField(VisualElement parent, string label, int value, System.Action<int> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("card-field-row");
            var field = new IntegerField(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddFloatField(VisualElement parent, string label, float value, System.Action<float> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("card-field-row");
            var field = new FloatField(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddToggle(VisualElement parent, string label, bool value, System.Action<bool> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("card-field-row");
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddVector2Field(VisualElement parent, string label, Vector2 value, System.Action<Vector2> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("card-field-row");
            var field = new Vector2Field(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddColorField(VisualElement parent, string label, Color value, System.Action<Color> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("card-field-row");
            var field = new ColorField(label) { value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddEnumField(VisualElement parent, string label, System.Enum value, System.Action<System.Enum> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("card-field-row");
            var field = new EnumField(label, value);
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            parent.Add(row);
        }

        private void AddPhaseSlider(VisualElement parent, ref int startup, ref int active, ref int recovery, int total)
        {
            float minVal = startup;
            float maxVal = startup + active;

            var readout = new Label($"Active Frames between {startup} and {startup + active}");
            readout.AddToClassList("phase-readout");
            parent.Add(readout);

            var slider = new MinMaxSlider(minVal, maxVal, 0, total);
            int su = startup, act = active, rec = recovery; // capture
            slider.RegisterValueChangedCallback(evt =>
            {
                RecordUndo("Adjust Phases");
                // We need to write back through the closure - this requires the caller to rebuild
                // Phase changes trigger a full rebuild
                onDataChanged?.Invoke();
            });
            parent.Add(slider);

            var fields = new VisualElement();
            fields.AddToClassList("phase-fields");

            var suField = new IntegerField("SU") { value = startup };
            suField.SetEnabled(false);
            fields.Add(suField);

            var actField = new IntegerField("ACT") { value = active };
            actField.SetEnabled(false);
            fields.Add(actField);

            var recField = new IntegerField("REC") { value = recovery };
            recField.SetEnabled(false);
            fields.Add(recField);

            parent.Add(fields);
        }

        private void AddSeparator(VisualElement parent, string label)
        {
            var sep = new VisualElement();
            sep.AddToClassList("card-separator");
            parent.Add(sep);

            if (!string.IsNullOrEmpty(label))
            {
                var lbl = new Label($"── {label} ──");
                lbl.AddToClassList("card-sub-header");
                parent.Add(lbl);
            }
        }

        // =========================================================
        // UNDO HELPERS
        // =========================================================
        private void RecordUndo(string action)
        {
            if (character != null)
                Undo.RecordObject(character, action);
        }

        private void MarkDirty()
        {
            if (character != null)
                EditorUtility.SetDirty(character);
            onDataChanged?.Invoke();
        }
    }
}
