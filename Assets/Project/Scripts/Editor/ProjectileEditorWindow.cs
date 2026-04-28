using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CombatEditor.UI;

namespace CombatEditor
{
    public class ProjectileEditorWindow : EditorWindow
    {
        private ProjectileData selectedProjectile;
        private PropertyCardBuilder cardBuilder;
        private VisualElement propertyScrollContent;
        private VisualElement timelineContainer;

        [MenuItem("Window/Projectile Editor")]
        public static void ShowWindow()
        {
            ProjectileEditorWindow window = GetWindow<ProjectileEditorWindow>("笘・Projectile Editor");
            window.minSize = new Vector2(400f, 600f);
        }

        public static void OpenWindow(ProjectileData projectile)
        {
            ProjectileEditorWindow window = GetWindow<ProjectileEditorWindow>("笘・Projectile Editor");
            window.selectedProjectile = projectile;
            window.Show();
            window.RefreshUI();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        public void CreateGUI()
        {
            StyleSheet uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Project/Scripts/Editor/AnimationEditor.uss");
            if (uss != null)
            {
                rootVisualElement.styleSheets.Add(uss);
            }

            var root = new VisualElement { name = "root" };
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1f;
            rootVisualElement.Add(root);

            var selectorBar = new VisualElement { name = "selector-bar" };
            selectorBar.style.flexDirection = FlexDirection.Row;
            selectorBar.style.paddingBottom = 10;
            selectorBar.style.paddingTop = 10;
            selectorBar.style.paddingLeft = 10;
            selectorBar.style.paddingRight = 10;
            selectorBar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);

            var projectileField = new ObjectField("Projectile Data")
            {
                objectType = typeof(ProjectileData),
                value = selectedProjectile,
                style = { flexGrow = 1f }
            };
            projectileField.RegisterValueChangedCallback(evt =>
            {
                selectedProjectile = evt.newValue as ProjectileData;
                RefreshUI();
            });
            selectorBar.Add(projectileField);
            root.Add(selectorBar);

            var body = new VisualElement { name = "body" };
            body.style.flexGrow = 1f;
            body.style.flexDirection = FlexDirection.Row;
            root.Add(body);

            propertyScrollContent = new ScrollView { name = "property-scroll-content" };
            propertyScrollContent.style.flexGrow = 1f;
            propertyScrollContent.style.paddingLeft = 10;
            propertyScrollContent.style.paddingRight = 10;
            body.Add(propertyScrollContent);

            timelineContainer = new VisualElement { name = "timeline-container" };
            timelineContainer.style.height = 120;
            timelineContainer.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            timelineContainer.style.borderTopWidth = 1;
            timelineContainer.style.borderTopColor = Color.black;
            timelineContainer.style.paddingLeft = 5;
            timelineContainer.style.paddingRight = 5;
            root.Add(timelineContainer);

            cardBuilder = new PropertyCardBuilder(propertyScrollContent, null, RefreshUI, RefreshUI);

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
            rootVisualElement.focusable = true;

            RefreshUI();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.ctrlKey && evt.keyCode == KeyCode.Z)
            {
                Undo.PerformUndo();
                evt.StopPropagation();
            }
            else if (evt.ctrlKey && evt.shiftKey && evt.keyCode == KeyCode.Z)
            {
                Undo.PerformRedo();
                evt.StopPropagation();
            }
            else if (evt.ctrlKey && evt.keyCode == KeyCode.Y)
            {
                Undo.PerformRedo();
                evt.StopPropagation();
            }
        }

        private void OnUndoRedo()
        {
            RefreshUI();
            Repaint();
        }

        private void RefreshUI()
        {
            if (propertyScrollContent == null)
            {
                return;
            }

            Vector2 savedScroll = Vector2.zero;
            ScrollView oldTimelineBody = timelineContainer.Q<ScrollView>("timeline-body");
            if (oldTimelineBody != null)
            {
                savedScroll = oldTimelineBody.scrollOffset;
            }

            propertyScrollContent.Clear();

            if (selectedProjectile == null)
            {
                propertyScrollContent.Add(new Label("Select a ProjectileData asset to begin editing.")
                {
                    style =
                    {
                        flexGrow = 1f,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        color = Color.gray
                    }
                });
                return;
            }

            cardBuilder.SetTarget(selectedProjectile);
            cardBuilder.Build(selectedProjectile);
            BuildPhaseTimeline(savedScroll);
        }

        private void BuildPhaseTimeline(Vector2 savedScroll)
        {
            timelineContainer.Clear();

            var title = new Label("Projectile Phase Timeline")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 5,
                    marginTop = 5
                }
            };
            timelineContainer.Add(title);

            var timelineBody = new ScrollView(ScrollViewMode.Horizontal) { name = "timeline-body" };
            timelineBody.style.flexDirection = FlexDirection.Row;
            timelineBody.style.height = 80;
            timelineContainer.Add(timelineBody);
            timelineBody.schedule.Execute(() => timelineBody.scrollOffset = savedScroll);

            int maxFrame = 10;
            HitboxBase spawnHitbox = selectedProjectile.ResolveSpawningHitbox();
            HitboxBase routineHitbox = selectedProjectile.ResolveRoutineHitbox();

            if (spawnHitbox != null)
            {
                maxFrame = Mathf.Max(maxFrame, spawnHitbox.startFrameOffset + spawnHitbox.activeFrames);
            }

            if (routineHitbox != null)
            {
                maxFrame = Mathf.Max(maxFrame, routineHitbox.startFrameOffset + routineHitbox.activeFrames);
            }

            maxFrame = Mathf.Max(30, maxFrame + 5);
            float frameWidth = 10f;

            var frameNumbers = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 20 } };
            for (int i = 0; i <= maxFrame; i++)
            {
                if (i % 5 != 0)
                {
                    continue;
                }

                var num = new Label(i.ToString())
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = i * frameWidth,
                        fontSize = 9,
                        color = Color.gray
                    }
                };
                frameNumbers.Add(num);
            }

            timelineBody.Add(frameNumbers);

            if (spawnHitbox != null)
            {
                timelineBody.Add(BuildTrackBar("Spawn", spawnHitbox, frameWidth, new Color(0.18f, 0.72f, 0.38f)));
            }

            if (routineHitbox != null)
            {
                timelineBody.Add(BuildTrackBar("Routine", routineHitbox, frameWidth, new Color(0.2f, 0.6f, 1f)));
            }

            if (spawnHitbox == null && routineHitbox == null)
            {
                timelineBody.Add(new Label("No phase hitboxes configured.")
                {
                    style =
                    {
                        color = Color.gray,
                        unityFontStyleAndWeight = FontStyle.Italic,
                        marginTop = 10
                    }
                });
            }
        }

        private static VisualElement BuildTrackBar(string labelPrefix, HitboxBase hitbox, float frameWidth, Color trackColor)
        {
            var track = new VisualElement { name = "track" };
            track.style.height = 15;
            track.style.marginTop = 2;
            track.style.flexDirection = FlexDirection.Row;

            var bar = new VisualElement();
            bar.style.position = Position.Absolute;
            bar.style.left = hitbox.startFrameOffset * frameWidth;
            bar.style.width = hitbox.activeFrames * frameWidth;
            bar.style.height = 12;
            bar.style.backgroundColor = trackColor;
            bar.style.borderLeftWidth = 1;
            bar.style.borderRightWidth = 1;
            bar.style.borderLeftColor = Color.black;
            bar.style.borderRightColor = Color.black;
            bar.style.borderTopColor = Color.black;
            bar.style.borderBottomColor = Color.black;

            var label = new Label($"{labelPrefix} ({hitbox.HitboxTypeName})")
            {
                style =
                {
                    fontSize = 8,
                    color = Color.black,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            bar.Add(label);
            track.Add(bar);
            return track;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (selectedProjectile == null)
            {
                return;
            }

            Handles.Label(new Vector3(0f, 3.25f, 0f), "Projectile Local Space Preview");
            DrawFacingPreview("Facing Left", -1f, new Vector2(-4f, 0f));
            DrawFacingPreview("Facing Right", 1f, new Vector2(4f, 0f));

            Handles.color = new Color(1f, 1f, 1f, 0.2f);
            Handles.DrawDottedLine(new Vector3(0f, -3f, 0f), new Vector3(0f, 3f, 0f), 4f);
        }

        private void DrawFacingPreview(string label, float facingSign, Vector2 previewOrigin)
        {
            Handles.color = Color.white;
            Handles.DrawSolidDisc(previewOrigin, Vector3.forward, 0.04f);
            Handles.Label((Vector3)previewOrigin + new Vector3(-0.6f, 2.75f, 0f), label);

            float travelDistance = Mathf.Max(0.75f, selectedProjectile.speed * Mathf.Max(0.1f, selectedProjectile.lifetime));
            Vector2 pathEnd = previewOrigin + Vector2.right * facingSign * travelDistance;
            Handles.color = Color.cyan;
            Handles.DrawDottedLine(previewOrigin, pathEnd, 4f);
            Handles.Label((Vector3)previewOrigin + new Vector3(-0.45f, -0.35f, 0f), "Origin");

            DrawPassiveColliderGizmo(previewOrigin, facingSign);
            DrawPhaseHitboxGizmo(selectedProjectile.ResolveSpawningHitbox(), "Spawn", new Color(0.18f, 0.72f, 0.38f), previewOrigin, facingSign);
            DrawPhaseHitboxGizmo(selectedProjectile.ResolveRoutineHitbox(), "Routine", new Color(0.2f, 0.6f, 1f), previewOrigin, facingSign);
        }

        private void DrawPassiveColliderGizmo(Vector2 previewOrigin, float facingSign)
        {
            DrawRectGizmo(
                previewOrigin + selectedProjectile.ResolvePassiveColliderOffset(facingSign),
                selectedProjectile.travelColliderSize,
                new Color(1f, 0.45f, 0.12f, 0.15f),
                new Color(1f, 0.45f, 0.12f, 1f),
                "Passive");
        }

        private void DrawPhaseHitboxGizmo(
            HitboxBase hitbox,
            string phaseLabel,
            Color fallbackColor,
            Vector2 previewOrigin,
            float facingSign)
        {
            if (!ProjectileSpatialUtility.TryGetHitboxGeometry(hitbox, facingSign, out Vector2 offset, out Vector2 size))
            {
                return;
            }

            Color resolvedColor = hitbox.debugColor != default ? hitbox.debugColor : fallbackColor;
            DrawRectGizmo(
                previewOrigin + offset,
                size,
                new Color(resolvedColor.r, resolvedColor.g, resolvedColor.b, 0.15f),
                resolvedColor,
                $"{phaseLabel}: {hitbox.HitboxTypeName} [F:{hitbox.startFrameOffset}-{hitbox.startFrameOffset + hitbox.activeFrames}]");
        }

        private static void DrawRectGizmo(Vector2 center, Vector2 size, Color fillColor, Color outlineColor, string label)
        {
            Vector3 p1 = (Vector3)center + new Vector3(-size.x, -size.y, 0f) * 0.5f;
            Vector3 p2 = (Vector3)center + new Vector3(size.x, -size.y, 0f) * 0.5f;
            Vector3 p3 = (Vector3)center + new Vector3(size.x, size.y, 0f) * 0.5f;
            Vector3 p4 = (Vector3)center + new Vector3(-size.x, size.y, 0f) * 0.5f;

            Handles.color = fillColor;
            Handles.DrawSolidRectangleWithOutline(new[] { p1, p2, p3, p4 }, fillColor, outlineColor);
            Handles.color = outlineColor;
            Handles.Label((Vector3)center + new Vector3(0f, size.y * 0.65f, 0f), label);
        }
    }
}
