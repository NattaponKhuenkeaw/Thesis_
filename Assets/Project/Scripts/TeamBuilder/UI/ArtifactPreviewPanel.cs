using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Project.InventorySystem.Items;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Detail panel for inspecting an artifact. Overlays the screen and can be dismissed
    /// by clicking outside its bounds.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ArtifactPreviewPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image artifactIcon;
        [SerializeField] private TMP_Text artifactNameText;
        [SerializeField] private GameObject statItemPrefab;
        [SerializeField] private Transform statItemContainer;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text loreText; // Optional lore/flavor text

        // =========================================================
        // Runtime State
        // =========================================================
        private RectTransform panelRect;
        private Artifact currentArtifact;
        private bool isOpen;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            panelRect = GetComponent<RectTransform>();
            gameObject.SetActive(false);
            isOpen = false;
        }

        private void Update()
        {
            if (!isOpen) return;

            // Dismiss when clicking outside the panel
            if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, UnityEngine.InputSystem.Pointer.current.position.ReadValue(), null))
                {
                    Close();
                }
            }
        }

        public void Open(Artifact artifact)
        {
            if (artifact == null) return;

            currentArtifact = artifact;
            PopulateData();

            gameObject.SetActive(true);
            isOpen = true;

            // Move to front of hierarchy so it renders on top
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (!isOpen) return;

            gameObject.SetActive(false);
            isOpen = false;
            currentArtifact = null;
        }

        private void PopulateData()
        {
            if (currentArtifact == null) return;

            if (artifactIcon != null && currentArtifact.icon != null)
            {
                artifactIcon.sprite = currentArtifact.icon;
                artifactIcon.enabled = true;
            }

            if (artifactNameText != null)
            {
                artifactNameText.text = currentArtifact.itemName;
            }

            // Clear existing stat items
            if (statItemContainer != null)
            {
                foreach (Transform child in statItemContainer)
                {
                    Destroy(child.gameObject);
                }

                if (statItemPrefab != null && currentArtifact.bonusStats != null)
                {
                    AddStatItemIfNonZero("HP", currentArtifact.bonusStats.maxHP, "hp");
                    AddStatItemIfNonZero("ATK", currentArtifact.bonusStats.attack, "atk");
                    AddStatItemIfNonZero("DEF", currentArtifact.bonusStats.defense, "def");
                    AddStatItemIfNonZero("SPD", currentArtifact.bonusStats.speed, "spd");
                    AddStatItemIfNonZero("Crit Rate", currentArtifact.bonusStats.critRate, "crit");
                    AddStatItemIfNonZero("Crit DMG", currentArtifact.bonusStats.critDamage, "crit");
                    AddStatItemIfNonZero("Cooldown Rate", currentArtifact.bonusStats.cooldownRate, "cdr");
                    AddStatItemIfNonZero("Elemental Mastery", currentArtifact.bonusStats.elementalMastery, "elemental");
                    AddStatItemIfNonZero("Impact", currentArtifact.bonusStats.impact, "impact");
                    AddStatItemIfNonZero("AE Recharge", currentArtifact.bonusStats.aeRecharge * 100f, "ae");
                }
            }

            if (descriptionText != null)
            {
                descriptionText.text = currentArtifact.passiveDescription;
            }

            if (loreText != null)
            {
                loreText.text = currentArtifact.description;
            }
        }

        private void AddStatItemIfNonZero(string statName, float value, string statKey = "")
        {
            if (Mathf.Abs(value) > 0.001f)
            {
                GameObject obj = Instantiate(statItemPrefab, statItemContainer);
                StatItemUI ui = obj.GetComponent<StatItemUI>();
                if (ui != null)
                {
                    ui.Setup(statName, value, statKey);
                }
            }
        }
    }
}
