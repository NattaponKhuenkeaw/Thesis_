using System;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// One save-slot card authored in the save panel prefab.
    /// Shows either the New Game label or the saved summary, and toggles the in-slot Play / Delete panel.
    /// </summary>
    public class SaveSlotCardView : MonoBehaviour
    {
        [SerializeField] private Text slotNameText;
        [SerializeField] private Text biomeText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text currencyText;
        [SerializeField] private Image[] portraits = Array.Empty<Image>();
        [SerializeField] private Color aliveTint = Color.white;
        [SerializeField] private Color deadTint = new Color(0.22f, 0.22f, 0.24f, 1f);
        [SerializeField] private GameObject[] filledObjects = Array.Empty<GameObject>();
        [SerializeField] private GameObject emptyLabel;
        [SerializeField] private GameObject actionPanel;
        [SerializeField] private Button playButton;
        [SerializeField] private Button deleteButton;

        public event Action SlotPressed;
        public event Action PlayPressed;
        public event Action DeletePressed;

        private Button cardButton;

        private void Awake()
        {
            cardButton = GetComponent<Button>();
            if (cardButton != null)
            {
                cardButton.onClick.AddListener(OnSlotClicked);
            }

            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayClicked);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.AddListener(OnDeleteClicked);
            }
        }

        private void OnDestroy()
        {
            if (cardButton != null)
            {
                cardButton.onClick.RemoveListener(OnSlotClicked);
            }

            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayClicked);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveListener(OnDeleteClicked);
            }
        }

        public void ShowEmpty(string slotName)
        {
            SetSlotName(slotName);
            SetFilledVisible(false);
            SetActive(emptyLabel, true);
            SetActionPanelVisible(false);
        }

        public void ShowFilled(string slotName, int biome, int runLevel, int currency, Sprite[] faces, bool[] dead)
        {
            SetSlotName(slotName);
            SetFilledVisible(true);
            SetActive(emptyLabel, false);
            SetActionPanelVisible(false);

            if (biomeText != null)
            {
                biomeText.text = biome.ToString();
            }

            if (levelText != null)
            {
                levelText.text = runLevel.ToString();
            }

            if (currencyText != null)
            {
                currencyText.text = currency.ToString("N0");
            }

            int count = portraits != null ? portraits.Length : 0;
            for (int i = 0; i < count; i++)
            {
                Image image = portraits[i];
                if (image == null)
                {
                    continue;
                }

                Sprite face = faces != null && i < faces.Length ? faces[i] : null;
                bool isDead = face != null && dead != null && i < dead.Length && dead[i];
                image.sprite = face;
                image.enabled = face != null;
                image.color = isDead ? deadTint : aliveTint;
            }
        }

        public bool IsActionPanelVisible => actionPanel != null && actionPanel.activeSelf;

        public void SetActionPanelVisible(bool visible)
        {
            SetActive(actionPanel, visible);
        }

        private void SetFilledVisible(bool visible)
        {
            if (filledObjects == null)
            {
                return;
            }

            for (int i = 0; i < filledObjects.Length; i++)
            {
                SetActive(filledObjects[i], visible);
            }
        }

        private void SetSlotName(string slotName)
        {
            if (slotNameText != null)
            {
                slotNameText.text = slotName;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void OnSlotClicked() => SlotPressed?.Invoke();
        private void OnPlayClicked() => PlayPressed?.Invoke();
        private void OnDeleteClicked() => DeletePressed?.Invoke();
    }
}
