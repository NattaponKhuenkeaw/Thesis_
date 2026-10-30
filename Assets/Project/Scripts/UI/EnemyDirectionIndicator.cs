using TMPro;
using UnityEngine;

namespace Project.UI
{
    [DisallowMultipleComponent]
    public sealed class EnemyDirectionIndicator : MonoBehaviour
    {
        [SerializeField] private RectTransform pointer;
        [SerializeField] private GameObject countRoot;
        [SerializeField] private TMP_Text countText;

        public void Show(Vector2 position, float angle, int count)
        {
            transform.localPosition = new Vector3(position.x, position.y, 0f);
            if (pointer != null) pointer.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (countRoot != null) countRoot.SetActive(count > 1);
            if (countText != null) countText.SetText("{0}", count);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
