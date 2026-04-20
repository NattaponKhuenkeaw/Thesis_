using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CombatCore;
using CombatEditor.UI;

namespace CombatEditor
{
    public class ProjectileEditorWindow : EditorWindow
    {
        private ProjectileData selectedProjectile;
        private PropertyCardBuilder cardBuilder;
        private VisualElement propertyScrollContent;
        private VisualElement timelineContainer;
        private float lastUpdateTime;
        private bool isPreviewingInScene;

        [MenuItem("Window/Projectile Editor")]
        public static void ShowWindow()
        {
            var w = GetWindow<ProjectileEditorWindow>("☄ Projectile Editor");
            w.minSize = new Vector2(400, 600);
        }

        public static void OpenWindow(ProjectileData projectile)
        {
            var w = GetWindow<ProjectileEditorWindow>("☄ Projectile Editor");
            w.selectedProjectile = projectile;
            w.Show();
            w.RefreshUI();
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
            // Load stylesheets (reuse AnimationEditor styles where possible)
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Project/Scripts/Editor/AnimationEditor.uss");
            if (uss != null)
                rootVisualElement.styleSheets.Add(uss);

            var root = new VisualElement { name = "root" };
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;
            rootVisualElement.Add(root);

            // 1. Selector Bar
            var selectorBar = new VisualElement { name = "selector-bar" };
            selectorBar.style.flexDirection = FlexDirection.Row;
            selectorBar.style.paddingBottom = 10;
            selectorBar.style.paddingTop = 10;
            selectorBar.style.paddingLeft = 10;
            selectorBar.style.paddingRight = 10;
            selectorBar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);

            var projField = new ObjectField("Projectile Data") 
            { 
                objectType = typeof(ProjectileData),
                value = selectedProjectile,
                style = { flexGrow = 1 }
            };
            projField.RegisterValueChangedCallback(evt => {
                selectedProjectile = evt.newValue as ProjectileData;
                RefreshUI();
            });
            selectorBar.Add(projField);
            root.Add(selectorBar);

            // 2. Main Body (Property Sidebar primarily)
            var body = new VisualElement { name = "body" };
            body.style.flexGrow = 1;
            body.style.flexDirection = FlexDirection.Row;
            root.Add(body);

            propertyScrollContent = new ScrollView { name = "property-scroll-content" };
            propertyScrollContent.style.flexGrow = 1;
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
            if (propertyScrollContent == null) return;

            // Save scroll if possible
            Vector2 savedScroll = Vector2.zero;
            var oldTimelineBody = timelineContainer.Q<ScrollView>("timeline-body");
            if (oldTimelineBody != null) savedScroll = oldTimelineBody.scrollOffset;

            propertyScrollContent.Clear();

            if (selectedProjectile == null)
            {
                propertyScrollContent.Add(new Label("Select a ProjectileData asset to begin editing.") {
                    style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleCenter, color = Color.gray }
                });
                return;
            }

            cardBuilder.SetTarget(selectedProjectile);
            cardBuilder.Build(selectedProjectile);
            
            BuildTimeline(savedScroll);
        }

        private void BuildTimeline(Vector2 savedScroll)
        {
            timelineContainer.Clear();
            if (selectedProjectile.impactHitData == null) return;

            var title = new Label("Impact Timeline (Logic Frames)") {
                 style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5, marginTop = 5 }
            };
            timelineContainer.Add(title);

            var timelineBody = new ScrollView(ScrollViewMode.Horizontal) { name = "timeline-body" };
            timelineBody.style.flexDirection = FlexDirection.Row;
            timelineBody.style.height = 80;
            timelineContainer.Add(timelineBody);

            // Restore scroll
            timelineBody.schedule.Execute(() => {
                timelineBody.scrollOffset = savedScroll;
            });


            int maxFrame = 0;
            foreach(var hb in selectedProjectile.impactHitData.hitboxes) {
                if (hb != null) maxFrame = Mathf.Max(maxFrame, hb.startFrameOffset + hb.activeFrames);
            }
            maxFrame = Mathf.Max(30, maxFrame + 5);

            float frameWidth = 10f;

            // Header for frame numbers
            var frameNumbers = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 20 } };
            for(int i = 0; i <= maxFrame; i++) {
                if (i % 5 == 0) {
                    var num = new Label(i.ToString()) { style = { position = Position.Absolute, left = i * frameWidth, fontSize = 9, color = Color.gray } };
                    frameNumbers.Add(num);
                }
            }
            timelineBody.Add(frameNumbers);

            // Tracks
            foreach(var hitbox in selectedProjectile.impactHitData.hitboxes) {
                if (hitbox == null) continue;
                
                var track = new VisualElement { name = "track" };
                track.style.height = 15;
                track.style.marginTop = 2;
                track.style.flexDirection = FlexDirection.Row;
                
                var bar = new VisualElement();
                bar.style.position = Position.Absolute;
                bar.style.left = hitbox.startFrameOffset * frameWidth;
                bar.style.width = hitbox.activeFrames * frameWidth;
                bar.style.height = 12;
                bar.style.backgroundColor = hitbox.TimelineTrackColor;
                bar.style.borderLeftWidth = 1;
                bar.style.borderRightWidth = 1;
                bar.style.borderLeftColor = Color.black;
                bar.style.borderRightColor = Color.black;
                bar.style.borderTopColor = Color.black;
                bar.style.borderBottomColor = Color.black;
                
                var label = new Label(hitbox.HitboxTypeName) { style = { fontSize = 8, color = Color.black, unityTextAlign = TextAnchor.MiddleCenter } };
                bar.Add(label);
                
                track.Add(bar);
                timelineBody.Add(track);
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (selectedProjectile == null || selectedProjectile.impactHitData == null) return;

            Handles.color = Color.cyan;
            Vector2 spawnPos = Vector2.zero; // Local origin for preview
            
            // Draw Travel Path (simplified)
            Handles.DrawDottedLine(spawnPos, spawnPos + Vector2.right * (selectedProjectile.speed * selectedProjectile.lifetime), 4f);
            Handles.Label(spawnPos, "Spawn Point (Projectile Path Preview)");

            // Draw Hitboxes in sequence
            foreach (var hitbox in selectedProjectile.impactHitData.hitboxes)
            {
                if (hitbox == null) continue;
                
                Vector2 offset = Vector2.zero;
                Vector2 size = Vector2.one;

                if (hitbox is NormalHitbox n) { offset = n.offset; size = n.size; }
                else if (hitbox is GrabHitbox g) { offset = g.offset; size = g.size; }
                else continue;

                Handles.color = hitbox.debugColor;
                Vector3 center = (Vector3)offset;
                
                // Draw wireframe box
                Vector3 p1 = center + new Vector3(-size.x, -size.y, 0) * 0.5f;
                Vector3 p2 = center + new Vector3(size.x, -size.y, 0) * 0.5f;
                Vector3 p3 = center + new Vector3(size.x, size.y, 0) * 0.5f;
                Vector3 p4 = center + new Vector3(-size.x, size.y, 0) * 0.5f;
                
                Handles.DrawPolyLine(p1, p2, p3, p4, p1);
                Handles.Label(center, $"{hitbox.HitboxTypeName} [F:{hitbox.startFrameOffset}-{hitbox.startFrameOffset + hitbox.activeFrames}]");

                // Draw solid cap for start frame
                Handles.color = new Color(hitbox.debugColor.r, hitbox.debugColor.g, hitbox.debugColor.b, 0.2f);
                Handles.DrawSolidRectangleWithOutline(new[] { p1, p2, p3, p4 }, hitbox.debugColor, Color.clear);
            }
        }
    }
}