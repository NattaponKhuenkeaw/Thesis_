using System.Collections.Generic;
using Project.Core.ZoneState;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.UI
{
    [DefaultExecutionOrder(11000)]
    [DisallowMultipleComponent]
    public sealed class OffscreenEnemyIndicators : MonoBehaviour
    {
        [Header("Authored Prefab")]
        [SerializeField] private RectTransform markersRoot;
        [SerializeField] private EnemyDirectionIndicator markerPrefab;

        [Header("Screen Insets (Canvas Units)")]
        [Tooltip("Left, bottom, right, top. Reserves space for the health bar and navigation.")]
        [SerializeField] private Vector4 screenInsets = new Vector4(46f, 108f, 74f, 112f);
        [SerializeField, Min(1f)] private float groupDistance = 58f;

        private struct Group
        {
            public Vector2 screenPosition;
            public float angle;
            public int count;
        }

        private readonly List<EnemyController> enemies = new List<EnemyController>(32);
        private readonly List<Group> groups = new List<Group>(16);
        private readonly List<EnemyDirectionIndicator> markers = new List<EnemyDirectionIndicator>(16);
        private Canvas canvas;
        private Camera gameplayCamera;

        public int VisibleIndicatorCount { get; private set; }

        private void Awake() => CacheReferences();
        private void OnDisable() => HideAll();

        private void CacheReferences()
        {
            if (markersRoot == null) markersRoot = transform as RectTransform;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
        }

        private void LateUpdate()
        {
            if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled)
                gameplayCamera = Camera.main;
            Refresh(gameplayCamera, Screen.safeArea);
        }

        public void Refresh(Camera camera, Rect safeArea)
        {
            CacheReferences();
            groups.Clear();
            if (camera == null || canvas == null || markerPrefab == null || markersRoot == null
                || PauseMenuController.IsOpen || (RoomChoiceUI.Instance != null && RoomChoiceUI.Instance.IsShowing)
                || (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)
                || EnemyController.IsRoomTraversalBlockingCombat
                || (PlayerCharacter.Instance != null && PlayerCharacter.Instance.IsDead))
            {
                HideAll();
                return;
            }

            Rect area = Intersect(camera.pixelRect, safeArea);
            if (area.width <= 0f || area.height <= 0f)
            {
                HideAll();
                return;
            }

            float scale = canvas.scaleFactor;
            float mergeDistanceSquared = groupDistance * scale * groupDistance * scale;
            RoomEnemyRegistry.FillTargetable(enemies);
            var activeScene = SceneManager.GetActiveScene();
            foreach (var enemy in enemies)
            {
                if (enemy.gameObject.scene != activeScene) continue;
                Bounds bounds = enemy.bodyCollider != null ? enemy.bodyCollider.bounds
                    : new Bounds(enemy.transform.position, Vector3.one);
                if (!TryProject(camera, bounds, area, screenInsets * scale, out var point, out float angle))
                    continue;

                int index = -1;
                for (int i = 0; i < groups.Count; i++)
                    if ((groups[i].screenPosition - point).sqrMagnitude < mergeDistanceSquared
                        && Mathf.Abs(Mathf.DeltaAngle(groups[i].angle, angle)) < 30f)
                    {
                        index = i;
                        break;
                    }
                if (index < 0) groups.Add(new Group { screenPosition = point, angle = angle, count = 1 });
                else
                {
                    var group = groups[index];
                    group.count++;
                    groups[index] = group;
                }
            }

            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            VisibleIndicatorCount = groups.Count;
            for (int i = 0; i < groups.Count; i++)
            {
                // Only authored marker prefabs are cloned; no UI hierarchy is built at runtime.
                if (i == markers.Count) markers.Add(Instantiate(markerPrefab, markersRoot));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(markersRoot, groups[i].screenPosition,
                    uiCamera, out var position);
                markers[i].Show(position, groups[i].angle, groups[i].count);
            }
            for (int i = groups.Count; i < markers.Count; i++) markers[i].Hide();
        }

        private void HideAll()
        {
            VisibleIndicatorCount = 0;
            foreach (var marker in markers)
                if (marker != null) marker.Hide();
        }

        private static Rect Intersect(Rect a, Rect b)
        {
            return Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin),
                Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
        }

        public static bool TryProject(Camera camera, Bounds bounds, Rect safeArea, Vector4 insets,
            out Vector2 position, out float angle)
        {
            position = default;
            angle = 0f;
            if (camera == null || safeArea.width <= 0f || safeArea.height <= 0f) return false;

            // Keep the arrow hidden while any part of the enemy's body is actually in view.
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            bool inFront = false;
            for (int corner = 0; corner < 8; corner++)
            {
                var world = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                var projected = camera.WorldToScreenPoint(world);
                if (projected.z <= 0f) continue;
                inFront = true;
                min = Vector2.Min(min, projected);
                max = Vector2.Max(max, projected);
            }
            if (inFront && Rect.MinMaxRect(min.x, min.y, max.x, max.y).Overlaps(camera.pixelRect)) return false;

            var center = camera.WorldToScreenPoint(bounds.center);
            Vector2 direction = (Vector2)center - camera.pixelRect.center;
            if (center.z <= 0f) direction = -direction;
            if (direction.sqrMagnitude < .001f) direction = Vector2.down;

            float widthLimit = safeArea.width * .4f, heightLimit = safeArea.height * .4f;
            var inner = Rect.MinMaxRect(
                safeArea.xMin + Mathf.Clamp(insets.x, 0f, widthLimit),
                safeArea.yMin + Mathf.Clamp(insets.y, 0f, heightLimit),
                safeArea.xMax - Mathf.Clamp(insets.z, 0f, widthLimit),
                safeArea.yMax - Mathf.Clamp(insets.w, 0f, heightLimit));
            // Aim from the viewport center, not the asymmetrically padded HUD rectangle.
            float t = float.PositiveInfinity;
            Vector2 origin = inner.center;
            if (Mathf.Abs(direction.x) > .0001f) t = Mathf.Min(t, inner.width * .5f / Mathf.Abs(direction.x));
            if (Mathf.Abs(direction.y) > .0001f) t = Mathf.Min(t, inner.height * .5f / Mathf.Abs(direction.y));
            position = origin + direction * t;
            angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return true;
        }
    }
}
