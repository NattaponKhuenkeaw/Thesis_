using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace CombatEditor.UI
{
    /// <summary>
    /// Builds Inspector-style foldout component cards for the Right Panel.
    /// Phase Configuration always present; MotionFrameData / VFXFrameData /
    /// HitFrameData are optional and toggled via "Add Component" dropdown.
    /// </summary>
    public class PropertyCardBuilder
    {
        private VisualElement container;
        private CharacterObject character;
        private System.Action onDataChanged;
        private Dictionary<string, bool> foldoutState = new Dictionary<string, bool>();

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

            // ── Phase Config (always present) ──
            BuildFoldoutCard("🎬 Phase Configuration", "phase_config", new Color(0f, 0.83f, 1f), body =>
            {
                AddTextField(body, "Step Name", step.stepName, v =>
                {
                    RecordUndo("Edit Step Name");
                    step.stepName = v;
                    MarkDirty();
                });

                if (step.animationClip != null)
                {
                    int total = Mathf.RoundToInt(step.animationClip.length * step.animationClip.frameRate);
                    AddPhaseSlider(body, step.startupFrames, step.activeFrames, step.recoveryFrames, total, (s, a, r) =>
                    {
                        RecordUndo("Adjust Phases");
                        step.startupFrames = s;
                        step.activeFrames = a;
                        step.recoveryFrames = r;
                        MarkDirty();
                    });
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

            // ── Motion Frame Data (optional) ──
            if (step.motionData != null)
            {
                BuildFoldoutCard("🏃 Motion Frame Data", "motion_data", step.motionData.TrackColor, body =>
                {
                    BuildMotionCard(body, step.motionData);
                }, () => { RecordUndo("Remove Motion"); step.motionData = null; MarkDirty(); });
            }

            // ── VFX Frame Data (optional) ──
            if (step.vfxData != null)
            {
                BuildFoldoutCard("✨ VFX Frame Data", "vfx_data", step.vfxData.TrackColor, body =>
                {
                    BuildVFXCard(body, step.vfxData);
                }, () => { RecordUndo("Remove VFX"); step.vfxData = null; MarkDirty(); });
            }

            // ── Hit Frame Data (optional) ──
            if (step.hitData != null)
            {
                BuildFoldoutCard("⚔ Hit Frame Data", "hit_data", step.hitData.TrackColor, body =>
                {
                    BuildHitDataCard(body, step.hitData);
                }, () => { RecordUndo("Remove HitData"); step.hitData = null; MarkDirty(); });
            }

            // ── Add Component Button ──
            BuildAddComponentButton(step);
        }

        // =========================================================
        // MOTION CARD
        // =========================================================
        private void BuildMotionCard(VisualElement body, MotionFrameData motion)
        {
            if (motion.keyframes == null) motion.keyframes = new List<MotionKeyframe>();

            for (int i = 0; i < motion.keyframes.Count; i++)
            {
                int idx = i;
                var kf = motion.keyframes[i];

                BuildFoldoutCard($"Keyframe #{idx + 1} — Frame {kf.frame}", $"motion_kf_{idx}", new Color(0.18f, 0.72f, 0.38f, 0.5f), inner =>
                {
                    AddIntField(inner, "Frame", kf.frame, v => { RecordUndo("Edit Motion Frame"); kf.frame = v; MarkDirty(); });
                    AddVector3Field(inner, "Position", kf.position, v => { RecordUndo("Edit Motion Pos"); kf.position = v; MarkDirty(); });
                    AddVector3Field(inner, "Rotation", kf.rotation, v => { RecordUndo("Edit Motion Rot"); kf.rotation = v; MarkDirty(); });
                    AddVector3Field(inner, "Scale", kf.scale, v => { RecordUndo("Edit Motion Scale"); kf.scale = v; MarkDirty(); });
                }, () =>
                {
                    RecordUndo("Delete Motion Keyframe");
                    motion.keyframes.RemoveAt(idx);
                    MarkDirty();
                }, isSubCard: true);
            }

            var addBtn = new Button(() =>
            {
                RecordUndo("Add Motion Keyframe");
                motion.keyframes.Add(new MotionKeyframe());
                MarkDirty();
            });
            addBtn.text = "＋ Add Keyframe";
            addBtn.AddToClassList("component-add-entry-btn");
            body.Add(addBtn);
        }

        // =========================================================
        // VFX CARD
        // =========================================================
        private void BuildVFXCard(VisualElement body, VFXFrameData vfx)
        {
            if (vfx.entries == null) vfx.entries = new List<VFXEntry>();

            for (int i = 0; i < vfx.entries.Count; i++)
            {
                int idx = i;
                var entry = vfx.entries[i];

                BuildFoldoutCard($"VFX #{idx + 1}: {entry.label}", $"vfx_entry_{idx}", entry.trackColor, inner =>
                {
                    AddTextField(inner, "Label", entry.label, v => { RecordUndo("Edit VFX Label"); entry.label = v; MarkDirty(); });
                    AddObjectField<GameObject>(inner, "VFX Prefab", entry.vfxPrefab, v => { RecordUndo("Edit VFX Prefab"); entry.vfxPrefab = v; MarkDirty(); });
                    AddIntField(inner, "Trigger Frame", entry.triggerFrame, v => { RecordUndo("Edit VFX Frame"); entry.triggerFrame = v; MarkDirty(); });
                    AddIntField(inner, "Duration", entry.duration, v => { RecordUndo("Edit VFX Duration"); entry.duration = v; MarkDirty(); });
                    AddColorField(inner, "Track Color", entry.trackColor, v => { RecordUndo("Edit VFX Color"); entry.trackColor = v; MarkDirty(); });

                    // Transform keys
                    AddSeparator(inner, "Transform Keys");
                    for (int k = 0; k < entry.transformKeys.Count; k++)
                    {
                        int ki = k;
                        var tk = entry.transformKeys[k];
                        AddIntField(inner, $"Key {ki + 1} Frame", tk.frame, v => { RecordUndo("Edit VFX TK Frame"); tk.frame = v; MarkDirty(); });
                        AddVector3Field(inner, $"Key {ki + 1} Pos", tk.position, v => { RecordUndo("Edit VFX TK Pos"); tk.position = v; MarkDirty(); });
                    }

                    var addKeyBtn = new Button(() =>
                    {
                        RecordUndo("Add VFX Transform Key");
                        entry.transformKeys.Add(new VFXTransformKey());
                        MarkDirty();
                    });
                    addKeyBtn.text = "＋ Add Transform Key";
                    addKeyBtn.AddToClassList("component-add-entry-btn");
                    inner.Add(addKeyBtn);

                }, () =>
                {
                    RecordUndo("Delete VFX Entry");
                    vfx.entries.RemoveAt(idx);
                    MarkDirty();
                }, isSubCard: true);
            }

            var addBtn = new Button(() =>
            {
                RecordUndo("Add VFX Entry");
                vfx.entries.Add(new VFXEntry());
                MarkDirty();
            });
            addBtn.text = "＋ Add VFX Entry";
            addBtn.AddToClassList("component-add-entry-btn");
            body.Add(addBtn);
        }

        // =========================================================
        // HIT DATA CARD
        // =========================================================
        private void BuildHitDataCard(VisualElement body, HitFrameData hitData)
        {
            if (hitData.hitboxes == null) hitData.hitboxes = new List<HitboxBase>();

            for (int i = 0; i < hitData.hitboxes.Count; i++)
            {
                int idx = i;
                var hb = hitData.hitboxes[i];

                string typeLabel = hb.HitboxTypeName;
                Color accent = hb.debugColor;
                accent.a = 0.7f;

                BuildFoldoutCard($"{typeLabel} Hitbox #{idx + 1}", $"hitbox_{idx}", accent, inner =>
                {
                    // Common timing
                    AddIntField(inner, "Start Offset", hb.startFrameOffset, v => { RecordUndo("Edit HB Offset"); hb.startFrameOffset = v; MarkDirty(); });
                    AddIntField(inner, "Duration", hb.activeFrames, v => { RecordUndo("Edit HB Duration"); hb.activeFrames = v; MarkDirty(); });
                    AddColorField(inner, "Debug Color", hb.debugColor, v => { RecordUndo("Edit HB Color"); hb.debugColor = v; MarkDirty(); });

                    // Type-specific
                    if (hb is NormalHitbox normal) BuildNormalHitboxFields(inner, normal);
                    else if (hb is GrabHitbox grab) BuildGrabHitboxFields(inner, grab);
                    else if (hb is ProjectileHitbox proj) BuildProjectileHitboxFields(inner, proj);

                }, () =>
                {
                    RecordUndo("Delete Hitbox");
                    hitData.hitboxes.RemoveAt(idx);
                    MarkDirty();
                }, isSubCard: true);
            }

            // Add hitbox type dropdown
            BuildAddHitboxButton(body, hitData);
        }

        private void BuildNormalHitboxFields(VisualElement body, NormalHitbox n)
        {
            AddSeparator(body, "Hitbox Geometry");
            AddVector2Field(body, "Offset", n.offset, v => { RecordUndo("Edit Offset"); n.offset = v; MarkDirty(); });
            AddVector2Field(body, "Size", n.size, v => { RecordUndo("Edit Size"); n.size = v; MarkDirty(); });

            AddSeparator(body, "Damage & Knockback");
            AddFloatField(body, "Damage Scale", n.damageMultiplier, v => { RecordUndo("Edit Damage"); n.damageMultiplier = v; MarkDirty(); });
            AddFloatField(body, "Knockback", n.knockbackForce, v => { RecordUndo("Edit KB"); n.knockbackForce = v; MarkDirty(); });
            AddFloatField(body, "Cam Shake", n.cameraShakeStrength, v => { RecordUndo("Edit Shake"); n.cameraShakeStrength = v; MarkDirty(); });
            AddEnumField(body, "Impact Tier", n.impactTier, v => { RecordUndo("Edit Tier"); n.impactTier = (DamageImpactTier)v; MarkDirty(); });

            AddSeparator(body, "Reaction & HitStop");
            AddIntField(body, "Hitstop Freeze", n.hitstunFrames, v => { RecordUndo("Edit Hitstop"); n.hitstunFrames = v; MarkDirty(); });
            AddVector2Field(body, "Reaction Vel", n.reactionVelocity, v => { RecordUndo("Edit Vel"); n.reactionVelocity = v; MarkDirty(); });
            AddIntField(body, "KB Frames", n.knockbackFrames, v => { RecordUndo("Edit KB Frames"); n.knockbackFrames = v; MarkDirty(); });
            AddToggle(body, "Is Knockdown", n.isKnockdown, v => { RecordUndo("Edit KD"); n.isKnockdown = v; MarkDirty(); });
            AddToggle(body, "Snap to Attacker", n.snapToAttacker, v => { RecordUndo("Edit Snap"); n.snapToAttacker = v; MarkDirty(); });
            if (n.snapToAttacker)
                AddVector2Field(body, "Snap Offset", n.snapOffset, v => { RecordUndo("Edit Snap Offset"); n.snapOffset = v; MarkDirty(); });
            AddColorField(body, "Flash Color", n.flashColor, v => { RecordUndo("Edit Flash"); n.flashColor = v; MarkDirty(); });
            AddIntField(body, "Flash Frames", n.flashFrames, v => { RecordUndo("Edit Flash F"); n.flashFrames = v; MarkDirty(); });
        }

        private void BuildGrabHitboxFields(VisualElement body, GrabHitbox g)
        {
            AddSeparator(body, "Hitbox Geometry");
            AddVector2Field(body, "Offset", g.offset, v => { RecordUndo("Edit Offset"); g.offset = v; MarkDirty(); });
            AddVector2Field(body, "Size", g.size, v => { RecordUndo("Edit Size"); g.size = v; MarkDirty(); });

            AddSeparator(body, "Grab Condition");
            AddFloatField(body, "Grab Range", g.grabRange, v => { RecordUndo("Edit Range"); g.grabRange = v; MarkDirty(); });
            AddFloatField(body, "Grab Angle", g.grabAngle, v => { RecordUndo("Edit Angle"); g.grabAngle = v; MarkDirty(); });
            AddToggle(body, "Can Grab Air", g.canGrabAirborne, v => { RecordUndo("Edit Air Grab"); g.canGrabAirborne = v; MarkDirty(); });
        }

        private void BuildProjectileHitboxFields(VisualElement body, ProjectileHitbox proj)
        {
            AddSeparator(body, "Projectile");
            AddObjectField<GameObject>(body, "Prefab", proj.projectilePrefab, v => { RecordUndo("Edit Proj Prefab"); proj.projectilePrefab = v; MarkDirty(); });
            AddVector3Field(body, "Spawn Point", proj.spawnPoint, v => { RecordUndo("Edit Spawn"); proj.spawnPoint = v; MarkDirty(); });

            AddSeparator(body, "Trajectory");
            AddFloatField(body, "Speed", proj.trajectory.speed, v => { RecordUndo("Edit Proj Speed"); proj.trajectory.speed = v; MarkDirty(); });
            AddFloatField(body, "Arc", proj.trajectory.arc, v => { RecordUndo("Edit Proj Arc"); proj.trajectory.arc = v; MarkDirty(); });
            AddFloatField(body, "Gravity", proj.trajectory.gravity, v => { RecordUndo("Edit Proj Gravity"); proj.trajectory.gravity = v; MarkDirty(); });

            AddSeparator(body, "Projectile Stats");
            AddFloatField(body, "Damage", proj.stats.damage, v => { RecordUndo("Edit Proj Dmg"); proj.stats.damage = v; MarkDirty(); });
            AddIntField(body, "Hit Count", proj.stats.hitCount, v => { RecordUndo("Edit Proj Hits"); proj.stats.hitCount = v; MarkDirty(); });
            AddFloatField(body, "Lifespan", proj.stats.lifespan, v => { RecordUndo("Edit Proj Life"); proj.stats.lifespan = v; MarkDirty(); });
            AddIntField(body, "Hitstun", proj.stats.hitstunFrames, v => { RecordUndo("Edit Proj Hitstun"); proj.stats.hitstunFrames = v; MarkDirty(); });
            AddVector2Field(body, "Reaction Vel", proj.stats.reactionVelocity, v => { RecordUndo("Edit Proj Vel"); proj.stats.reactionVelocity = v; MarkDirty(); });
            AddIntField(body, "KB Frames", proj.stats.knockbackFrames, v => { RecordUndo("Edit Proj KB"); proj.stats.knockbackFrames = v; MarkDirty(); });
        }

        // =========================================================
        // ADD COMPONENT BUTTON
        // =========================================================
        private void BuildAddComponentButton(AttackStepData step)
        {
            var addRow = new VisualElement();
            addRow.AddToClassList("add-component-row");

            var addBtn = new Button(() =>
            {
                var menu = new GenericMenu();
                if (step.motionData == null)
                    menu.AddItem(new GUIContent("🏃 Motion Frame Data"), false, () => { RecordUndo("Add Motion"); step.motionData = new MotionFrameData(); MarkDirty(); });
                else
                    menu.AddDisabledItem(new GUIContent("🏃 Motion Frame Data (added)"));

                if (step.vfxData == null)
                    menu.AddItem(new GUIContent("✨ VFX Frame Data"), false, () => { RecordUndo("Add VFX"); step.vfxData = new VFXFrameData(); MarkDirty(); });
                else
                    menu.AddDisabledItem(new GUIContent("✨ VFX Frame Data (added)"));

                if (step.hitData == null)
                    menu.AddItem(new GUIContent("⚔ Hit Frame Data"), false, () => { RecordUndo("Add HitData"); step.hitData = new HitFrameData(); MarkDirty(); });
                else
                    menu.AddDisabledItem(new GUIContent("⚔ Hit Frame Data (added)"));

                menu.ShowAsContext();
            });
            addBtn.text = "＋  Add Component";
            addBtn.AddToClassList("add-component-btn");
            addRow.Add(addBtn);

            container.Add(addRow);
        }

        private void BuildAddHitboxButton(VisualElement body, HitFrameData hitData)
        {
            var addBtn = new Button(() =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Normal Hitbox"), false, () =>
                {
                    RecordUndo("Add Normal Hitbox");
                    hitData.hitboxes.Add(new NormalHitbox());
                    MarkDirty();
                });
                menu.AddItem(new GUIContent("Grab Hitbox"), false, () =>
                {
                    RecordUndo("Add Grab Hitbox");
                    hitData.hitboxes.Add(new GrabHitbox());
                    MarkDirty();
                });
                menu.AddItem(new GUIContent("Projectile Hitbox"), false, () =>
                {
                    RecordUndo("Add Projectile Hitbox");
                    hitData.hitboxes.Add(new ProjectileHitbox());
                    MarkDirty();
                });
                menu.ShowAsContext();
            });
            addBtn.text = "＋ Add Hitbox";
            addBtn.AddToClassList("component-add-entry-btn");
            body.Add(addBtn);
        }

        // =========================================================
        // DASH CARDS
        // =========================================================
        private void BuildDashCards(DashData dash)
        {
            if (dash == null) return;

            BuildFoldoutCard("⚡ Dash Configuration", "dash_config", new Color(0f, 0.83f, 1f), body =>
            {
                if (dash.animationClip != null)
                {
                    int total = Mathf.RoundToInt(dash.animationClip.length * dash.animationClip.frameRate);
                    AddPhaseSlider(body, dash.startupFrames, dash.activeFrames, dash.recoveryFrames, total, (s, a, r) =>
                    {
                        RecordUndo("Adjust Dash Phases");
                        dash.startupFrames = s;
                        dash.activeFrames = a;
                        dash.recoveryFrames = r;
                        MarkDirty();
                    });
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
        // FOLDOUT CARD BUILDER
        // =========================================================
        private void BuildFoldoutCard(string title, string foldoutKey, Color accentColor,
            System.Action<VisualElement> buildBody, System.Action onDelete = null, bool isSubCard = false)
        {
            if (!foldoutState.ContainsKey(foldoutKey))
                foldoutState[foldoutKey] = true;

            bool expanded = foldoutState[foldoutKey];

            var card = new VisualElement();
            card.AddToClassList(isSubCard ? "property-subcard" : "property-card");

            // Header
            var header = new VisualElement();
            header.AddToClassList("property-card__header");

            // Accent bar
            var accent = new VisualElement();
            accent.AddToClassList("property-card__accent");
            accent.style.backgroundColor = accentColor;
            header.Add(accent);

            // Arrow
            var arrow = new Label(expanded ? "▾" : "►");
            arrow.AddToClassList("property-card__arrow");
            header.Add(arrow);

            var headerText = new Label(title);
            headerText.AddToClassList("property-card__header-text");
            header.Add(headerText);

            // Delete button (if removable)
            if (onDelete != null)
            {
                var deleteBtn = new Button(() => onDelete());
                deleteBtn.text = "✕";
                deleteBtn.AddToClassList("property-card__delete");
                header.Add(deleteBtn);
            }

            // Toggle foldout
            string key = foldoutKey;
            header.RegisterCallback<ClickEvent>(evt =>
            {
                // Don't toggle if delete button was clicked
                if (evt.target is Button) return;
                foldoutState[key] = !foldoutState[key];
                onDataChanged?.Invoke();
            });

            card.Add(header);

            // Body (only if expanded)
            if (expanded)
            {
                var body = new VisualElement();
                body.AddToClassList("property-card__body");
                buildBody(body);
                card.Add(body);
            }

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

        private void AddVector3Field(VisualElement parent, string label, Vector3 value, System.Action<Vector3> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("card-field-row");
            var field = new Vector3Field(label) { value = value };
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

        private void AddObjectField<T>(VisualElement parent, string label, T value, System.Action<T> onChange) where T : UnityEngine.Object
        {
            var row = new VisualElement();
            row.AddToClassList("card-field-row");
            var field = new ObjectField(label) { objectType = typeof(T), value = value };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue as T));
            row.Add(field);
            parent.Add(row);
        }

        private void AddPhaseSlider(VisualElement parent, int su, int act, int rec, int total, System.Action<int, int, int> onChanges)
        {
            var readout = new Label($"Total Animation Frames: {total}");
            readout.AddToClassList("phase-readout");
            parent.Add(readout);

            var slider = new MinMaxSlider(su, su + act, 0, total);
            
            // Create fields first so we can reference them in the slider callback
            var fields = new VisualElement();
            fields.AddToClassList("phase-fields");

            var suField = new IntegerField("SU") { value = su };
            var actField = new IntegerField("ACT") { value = act };
            var recField = new IntegerField("REC") { value = rec };

            // Slider callback: Updates the data AND the integer fields
            slider.RegisterValueChangedCallback(evt =>
            {
                int newSU = Mathf.RoundToInt(evt.newValue.x);
                int newACT = Mathf.RoundToInt(evt.newValue.y) - newSU;
                int newREC = total - newSU - newACT;
                
                // Update integer fields without triggering their own change callbacks
                suField.SetValueWithoutNotify(newSU);
                actField.SetValueWithoutNotify(newACT);
                recField.SetValueWithoutNotify(newREC);

                onChanges?.Invoke(Mathf.Max(0, newSU), Mathf.Max(0, newACT), Mathf.Max(0, newREC));
            });
            parent.Add(slider);

            // Integer field callbacks: Update the data AND the slider
            suField.RegisterValueChangedCallback(evt => {
                int nSU = evt.newValue;
                slider.SetValueWithoutNotify(new Vector2(nSU, nSU + actField.value));
                onChanges?.Invoke(nSU, actField.value, recField.value);
            });
            fields.Add(suField);

            actField.RegisterValueChangedCallback(evt => {
                int nACT = evt.newValue;
                slider.SetValueWithoutNotify(new Vector2(suField.value, suField.value + nACT));
                onChanges?.Invoke(suField.value, nACT, recField.value);
            });
            fields.Add(actField);

            recField.RegisterValueChangedCallback(evt => {
                int nREC = evt.newValue;
                // REC is derived, but letting user edit it directly:
                // We'll keep SU and ACT and let REC be what it is (total - su - act)
                // Actually, let's make it intuitive: editing REC updates ACT
                int nACT = total - suField.value - nREC;
                actField.SetValueWithoutNotify(nACT);
                slider.SetValueWithoutNotify(new Vector2(suField.value, suField.value + nACT));
                onChanges?.Invoke(suField.value, nACT, nREC);
            });
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
