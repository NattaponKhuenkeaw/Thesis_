using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    public sealed class RoomRouteProgressNode : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text number;
        [SerializeField] private GameObject bossLabel;
        [SerializeField] private GameObject nextMarker;
        [SerializeField] private Sprite checkSprite;
        [SerializeField] private Color completedColor = new Color(.27f, .76f, .6f);
        [SerializeField] private Color nextColor = new Color(1f, .91f, .05f);
        [SerializeField] private Color futureColor = new Color(.58f, .61f, .68f);

        public void Bind(int index, bool completed, bool next, bool boss, Sprite sprite)
        {
            number.text = (index + 1).ToString();
            border.color = completed ? completedColor : next ? nextColor : futureColor;
            background.color = completed ? new Color(.07f, .23f, .2f, 1f) : new Color(.045f, .055f, .09f, 1f);
            nextMarker.SetActive(next);
            bossLabel.SetActive(boss && !completed);
            SetIcon(completed ? checkSprite : sprite, !completed && boss, completed || next);
        }

        public void SetIcon(Sprite sprite, bool boss, bool highlighted = true)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.color = highlighted ? Color.white : futureColor;
            icon.rectTransform.anchoredPosition = new Vector2(0f, boss ? 9f : 0f);
            icon.rectTransform.sizeDelta = boss ? new Vector2(32f, 32f) : new Vector2(38f, 38f);
            bossLabel.SetActive(boss);
        }
    }
}
