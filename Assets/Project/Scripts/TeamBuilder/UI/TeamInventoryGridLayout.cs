using UnityEngine;
using UnityEngine.UI;

namespace Project.TeamBuilder.UI
{
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class TeamInventoryGridLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        private GridLayoutGroup grid;
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            grid = grid != null ? grid : GetComponent<GridLayoutGroup>();
            if (viewport == null) return;
            float width = Mathf.Max(1f, (viewport.rect.width - grid.padding.horizontal - grid.spacing.x * 2f) / 3f);
            var cell = new Vector2(width, width * 1.22f);
            if ((grid.cellSize - cell).sqrMagnitude > .01f) grid.cellSize = cell;
        }
    }
}
