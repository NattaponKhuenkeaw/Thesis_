using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Project.InventorySystem.Items;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Individual team slot — displays character portrait, element icon,
    /// 3 artifact sub-slots, and ATK/DEF/HP stats.
    /// Implements IDropHandler to receive character drags from the grid.
    /// Implements pointer + drag handlers to select active character and drag equipped
    /// characters back out to the inventory grid.
    /// </summary>
    public class TeamSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // =========================================================
        // UI References
        // =========================================================
        [Header("Character Display")]
        [SerializeField] private Image profileImage;
        [SerializeField] private Image elementIcon;
        [SerializeField] private TMP_Text characterNameText;

        [Header("Stat Texts")]
        [SerializeField] private TMP_Text atkText;
        [SerializeField] private TMP_Text defText;
        [SerializeField] private TMP_Text hpText;

        [Header("Artifact Slots")]
        [SerializeField] private ArtifactSlotUI[] artifactSlots = new ArtifactSlotUI[3];

        [Header("Empty State")]
        [SerializeField] private GameObject emptyStateOverlay;
        [SerializeField] private Sprite emptyPortraitSprite;

        [Header("Active State")]
        [SerializeField] private GameObject activeIndicator;

        [Header("Element Sprites")]
        [Tooltip("Assign sprites in order: None, Earth, Water, Fire, Wind (matching ElementType enum order)")]
        [SerializeField] private Sprite[] elementSprites = new Sprite[5];

        // =========================================================
        // Runtime State
        // =========================================================
        private CharacterItem currentCharacter;
        private TeamBuilderUI parentBuilder;
        private int slotIndex;
        private CanvasGroup canvasGroup;

        // =========================================================
        // Properties
        // =========================================================
        public CharacterItem CurrentCharacter => currentCharacter;
        public int SlotIndex => slotIndex;

        // =========================================================
        // Initialization
        // =========================================================
        public void Init(TeamBuilderUI builder, int index)
        {
            parentBuilder = builder;
            slotIndex = index;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Initialize artifact sub-slots
            foreach (var artifactSlot in artifactSlots)
            {
                if (artifactSlot != null)
                    artifactSlot.Init(this);
            }

            ClearSlot();
        }

        // =========================================================
        // Public Methods
        // =========================================================

        /// <summary>
        /// Places a character into this slot and updates all visuals.
        /// </summary>
        public void SetCharacter(CharacterItem character)
        {
            ResetDragState();
            currentCharacter = character;

            if (character == null)
            {
                ClearSlot();
                return;
            }

            // Sync BaseItem fields from the CharacterObject reference
            character.SyncFromCharacterObjectData();

            CharacterObject charObj = character.characterObjectData;

            // Portrait
            if (profileImage != null)
            {
                if (charObj != null && charObj.metaData.portrait != null)
                    profileImage.sprite = charObj.metaData.portrait;
                else if (character.icon != null)
                    profileImage.sprite = character.icon;

                profileImage.color = Color.white;
                profileImage.enabled = true;
            }

            // Character Name
            if (characterNameText != null)
            {
                characterNameText.text = (charObj != null)
                    ? charObj.metaData.displayName
                    : character.itemName;
            }

            // Element Icon
            if (elementIcon != null && charObj != null)
            {
                int elementIndex = (int)charObj.elementType;
                if (elementIndex >= 0 && elementIndex < elementSprites.Length && elementSprites[elementIndex] != null)
                {
                    elementIcon.sprite = elementSprites[elementIndex];
                    elementIcon.enabled = true;
                }
                else
                {
                    elementIcon.enabled = false;
                }
            }

            // Show/hide empty overlay
            if (emptyStateOverlay != null)
                emptyStateOverlay.SetActive(false);

            RecalculateStats();
        }

        /// <summary>
        /// Empties this slot completely.
        /// </summary>
        public void ClearSlot()
        {
            ResetDragState();
            currentCharacter = null;

            if (profileImage != null)
            {
                profileImage.sprite = emptyPortraitSprite;
                profileImage.color = new Color(1f, 1f, 1f, 0.3f);
            }

            if (characterNameText != null)
                characterNameText.text = "Empty";

            if (elementIcon != null)
                elementIcon.enabled = false;

            if (emptyStateOverlay != null)
                emptyStateOverlay.SetActive(true);

            // Clear artifact slots
            foreach (var artifactSlot in artifactSlots)
            {
                if (artifactSlot != null)
                    artifactSlot.ClearArtifact();
            }

            // Clear stats
            if (atkText != null) atkText.text = "";
            if (defText != null) defText.text = "";
            if (hpText != null) hpText.text = "";

            SetActiveState(false);
        }

        /// <summary>
        /// Highlights this slot if it's the currently active character in TeamLoader.
        /// </summary>
        public void SetActiveState(bool isActive)
        {
            if (activeIndicator != null)
                activeIndicator.SetActive(isActive);
        }

        /// <summary>
        /// Swaps the content of this slot with another TeamSlotUI.
        /// </summary>
        public void SwapWith(TeamSlotUI other)
        {
            if (other == null) return;

            CharacterItem tempChar = this.currentCharacter;
            Artifact[] tempArtifacts = GetEquippedArtifacts();

            // Set this slot to other's data
            this.SetCharacter(other.currentCharacter);
            Artifact[] otherArtifacts = other.GetEquippedArtifacts();
            SetArtifacts(otherArtifacts);

            // Set other slot to our original data
            other.SetCharacter(tempChar);
            other.SetArtifacts(tempArtifacts);
        }

        /// <summary>
        /// Recalculates and displays base stats + artifact bonuses.
        /// Called whenever a character or artifact changes.
        /// </summary>
        public void RecalculateStats()
        {
            if (currentCharacter == null || currentCharacter.characterObjectData == null)
            {
                if (atkText != null) atkText.text = "";
                if (defText != null) defText.text = "";
                if (hpText != null) hpText.text = "";
                return;
            }

            CharacterStats baseStats = currentCharacter.characterObjectData.stats;
            float totalATK = baseStats.attack;
            float totalDEF = baseStats.defense;
            float totalHP = baseStats.maxHP;

            // Add artifact bonuses
            foreach (var slot in artifactSlots)
            {
                if (slot != null && slot.CurrentArtifact != null)
                {
                    totalATK += slot.CurrentArtifact.bonusATK;
                    totalDEF += slot.CurrentArtifact.bonusDEF;
                    totalHP += slot.CurrentArtifact.bonusHP;
                }
            }

            if (atkText != null) atkText.text = $"{totalATK:F0}";
            if (defText != null) defText.text = $"{totalDEF:F0}";
            UpdateHealthDisplay(totalHP);
        }

        /// <summary>
        /// Returns the CharacterItem in this slot, or null if empty.
        /// </summary>
        public CharacterItem GetCharacterItem()
        {
            return currentCharacter;
        }

        // =========================================================
        // Drop Handler — receives drags from CharacterGridItemUI
        // =========================================================
        public void OnDrop(PointerEventData eventData)
        {
            // Get the dragged character
            CharacterGridItemUI draggedItem = eventData.pointerDrag?.GetComponent<CharacterGridItemUI>();
            if (draggedItem == null || draggedItem.CharacterData == null) return;

            CharacterItem incomingChar = draggedItem.CharacterData;

            if (parentBuilder == null) return;

            // Check if this character is already in ANOTHER slot → swap
            TeamSlotUI existingSlot = parentBuilder.FindSlotWithCharacter(incomingChar);

            if (existingSlot != null && existingSlot != this)
            {
                // Swap: the existing slot gets our current character
                existingSlot.SetCharacter(this.currentCharacter);
                this.SetCharacter(incomingChar);
            }
            else if (existingSlot == this)
            {
                // Already in this slot, do nothing
                return;
            }
            else
            {
                // Simple placement into an empty (or occupied) slot
                // If this slot had a character, it goes back to the grid
                this.SetCharacter(incomingChar);
            }

            // Notify the builder to refresh the grid (dim placed characters)
            parentBuilder.OnSlotChanged();
        }

        // =========================================================
        // Click Handler — selects active character
        // =========================================================
        public void OnPointerClick(PointerEventData eventData)
        {
            if (currentCharacter == null || parentBuilder == null) return;

            if (parentBuilder.currentMode == TeamBuilderMode.CombatZone)
            {
                parentBuilder.OpenInspectScreen(currentCharacter);
            }
            else
            {
                // Set this slot as active in the TeamLoader
                parentBuilder.SetActiveIndex(slotIndex);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (parentBuilder != null && parentBuilder.currentMode == TeamBuilderMode.CombatZone) return;

            if (currentCharacter == null || profileImage == null || profileImage.sprite == null)
                return;

            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            GameObject dropTarget = eventData.pointerEnter;
            bool droppedOnInventoryPanel = dropTarget != null
                && dropTarget.GetComponentInParent<CharacterGridInventoryUI>() != null;

            ResetDragState();

            if (currentCharacter == null || parentBuilder == null)
                return;

            if (droppedOnInventoryPanel)
                parentBuilder.TryRemoveCharacterFromSlot(slotIndex);
        }

        // =========================================================
        // Helpers
        // =========================================================
        private Artifact[] GetEquippedArtifacts()
        {
            Artifact[] artifacts = new Artifact[artifactSlots.Length];
            for (int i = 0; i < artifactSlots.Length; i++)
            {
                artifacts[i] = (artifactSlots[i] != null) ? artifactSlots[i].GetArtifact() : null;
            }
            return artifacts;
        }

        private void SetArtifacts(Artifact[] artifacts)
        {
            if (artifacts == null) return;
            for (int i = 0; i < artifactSlots.Length && i < artifacts.Length; i++)
            {
                if (artifactSlots[i] != null)
                    artifactSlots[i].SetArtifact(artifacts[i]);
            }
        }

        private void Update()
        {
            if (currentCharacter == null || currentCharacter.characterObjectData == null || hpText == null)
                return;

            UpdateHealthDisplay(currentCharacter.characterObjectData.stats != null
                ? currentCharacter.characterObjectData.stats.maxHP
                : 0f);
        }

        private void UpdateHealthDisplay(float fallbackMaxHp)
        {
            if (hpText == null)
                return;

            if (currentCharacter == null || currentCharacter.characterObjectData == null)
            {
                hpText.text = string.Empty;
                return;
            }

            if (parentBuilder != null
                && parentBuilder.TryGetCharacterHealth(currentCharacter.characterObjectData, out float currentHp, out float maxHp)
                && maxHp > 0f)
            {
                hpText.text = $"{Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)}";
                return;
            }

            hpText.text = $"{fallbackMaxHp:F0}";
        }

        private void OnDisable()
        {
            ResetDragState();
        }

        private void OnDestroy()
        {
            ResetDragState();
        }

        private void ResetDragState()
        {
            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = true;
        }
    }
}
