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
        private int openedFrame = -1;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            panelRect = GetComponent<RectTransform>();
            ResolveMissingReferences();
            gameObject.SetActive(false);
            isOpen = false;
        }

        private void Update()
        {
            if (!isOpen) return;
            if (Time.frameCount == openedFrame) return;

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
            gameObject.SetActive(true);
            isOpen = true;
            openedFrame = Time.frameCount;
            ResolveMissingReferences();
            PopulateData();

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

        private void ResolveMissingReferences()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            Image[] images = GetComponentsInChildren<Image>(true);

            artifactNameText ??= FindTextByName(texts, "Name", "Title", "ArtifactName");
            descriptionText ??= FindTextByName(texts, "Description", "Passive", "Effect", "Info");
            loreText ??= FindTextByName(texts, "Lore", "Flavor");
            artifactIcon ??= FindImageByName(images, "Icon", "ArtifactIcon");

            if (statItemContainer == null)
            {
                Transform statsRoot = FindChildByName(transform, "Stats", "StatContainer", "Stat Item Container", "Content");
                statItemContainer = statsRoot != null ? statsRoot : transform;
            }
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
                if (statItemPrefab == null || statItemContainer == null)
                {
                    return;
                }

                GameObject obj = Instantiate(statItemPrefab, statItemContainer);
                StatItemUI ui = obj.GetComponent<StatItemUI>();
                if (ui != null)
                {
                    ui.Setup(statName, value, statKey);
                }
            }
        }

        private static TMP_Text FindTextByName(TMP_Text[] texts, params string[] names)
        {
            if (texts == null || names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                for (int j = 0; j < texts.Length; j++)
                {
                    if (texts[j] != null && texts[j].name.IndexOf(names[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return texts[j];
                    }
                }
            }

            return null;
        }

        private static Image FindImageByName(Image[] images, params string[] names)
        {
            if (images == null || names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                for (int j = 0; j < images.Length; j++)
                {
                    if (images[j] != null && images[j].name.IndexOf(names[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return images[j];
                    }
                }
            }

            return null;
        }

        private static Transform FindChildByName(Transform root, params string[] names)
        {
            if (root == null || names == null)
            {
                return null;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = 0; j < children.Length; j++)
                {
                    if (children[j] != null && children[j].name.IndexOf(names[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return children[j];
                    }
                }
            }

            return null;
        }
    }
}
