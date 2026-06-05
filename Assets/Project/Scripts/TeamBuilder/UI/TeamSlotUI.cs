using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Individual team slot that supports safe-zone editing and combat-zone inspection.
    /// </summary>
    public class TeamSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
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

        [Header("Combat Lock State")]
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private GameObject padlockOverlay;
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private AudioClip lockedActionSfx;
        [SerializeField] private float inspectHoldDuration = 0.45f;
        [SerializeField] private float lockedWiggleDuration = 0.25f;
        [SerializeField] private float lockedWiggleDistance = 8f;
        [SerializeField] private float padlockPulseScale = 1.15f;

        [Header("Stat Feedback")]
        [SerializeField] private Color statNormalColor = Color.white;
        [SerializeField] private Color statBoostColor = new Color(0.45f, 1f, 0.45f, 1f);
        [SerializeField] private Color placementBurstColor = new Color(0.75f, 1f, 0.9f, 0.9f);
        [SerializeField] private float statFeedbackDuration = 0.3f;
        [SerializeField] private float statFeedbackScale = 1.2f;

        [Header("Element Sprites")]
        [Tooltip("Assign sprites in order: None, Earth, Water, Fire, Wind (matching ElementType enum order)")]
        [SerializeField] private Sprite[] elementSprites = new Sprite[5];

        private CharacterItem currentCharacter;
        private TeamBuilderUI parentBuilder;
        private int slotIndex;
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Transform dragGhost;
        private Vector2 baseAnchoredPosition;
        private Coroutine lockFeedbackCoroutine;
        private Coroutine inspectHoldCoroutine;
        private Coroutine atkFeedbackCoroutine;
        private Coroutine defFeedbackCoroutine;
        private Coroutine hpFeedbackCoroutine;
        private UIFeedbackDriver feedbackDriver;
        private float lastAttackBase;
        private float lastAttackBonus;
        private float lastDefenseBase;
        private float lastDefenseBonus;
        private float lastHpBase;
        private float lastHpBonus;
        private bool hasStatSnapshot;

        public CharacterItem CurrentCharacter => currentCharacter;
        public int SlotIndex => slotIndex;

        public void Init(TeamBuilderUI builder, int index)
        {
            parentBuilder = builder;
            slotIndex = index;
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
            feedbackDriver = UIFeedbackDriver.GetOrAdd(gameObject);

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (rectTransform != null)
            {
                baseAnchoredPosition = rectTransform.anchoredPosition;
            }

            for (int i = 0; i < artifactSlots.Length; i++)
            {
                if (artifactSlots[i] != null)
                {
                    artifactSlots[i].Init(this);
                }
            }

            ClearSlot();
        }

        public void SetCharacter(CharacterItem character)
        {
            ResetDragState();
            currentCharacter = character;

            if (character == null)
            {
                ClearSlot();
                return;
            }

            character.SyncFromCharacterObjectData();
            CharacterObject charObj = character.characterObjectData;

            if (profileImage != null)
            {
                if (charObj != null && charObj.metaData.portrait != null)
                {
                    profileImage.sprite = charObj.metaData.portrait;
                }
                else if (character.icon != null)
                {
                    profileImage.sprite = character.icon;
                }

                profileImage.color = Color.white;
                profileImage.enabled = true;
            }

            if (characterNameText != null)
            {
                characterNameText.text = charObj != null ? charObj.metaData.displayName : character.itemName;
            }

            if (elementIcon != null && charObj != null)
            {
                int elementIndex = (int)charObj.elementType;
                bool hasElementSprite = elementIndex >= 0 && elementIndex < elementSprites.Length && elementSprites[elementIndex] != null;
                elementIcon.enabled = hasElementSprite;
                if (hasElementSprite)
                {
                    elementIcon.sprite = elementSprites[elementIndex];
                }
            }

            if (emptyStateOverlay != null)
            {
                emptyStateOverlay.SetActive(false);
            }

            RecalculateStats();
        }

        public void ClearSlot()
        {
            ResetDragState();
            currentCharacter = null;
            hasStatSnapshot = false;

            if (profileImage != null)
            {
                profileImage.sprite = emptyPortraitSprite;
                profileImage.color = new Color(1f, 1f, 1f, 0.3f);
            }

            if (characterNameText != null)
            {
                characterNameText.text = "Empty";
            }

            if (elementIcon != null)
            {
                elementIcon.enabled = false;
            }

            if (emptyStateOverlay != null)
            {
                emptyStateOverlay.SetActive(true);
            }

            for (int i = 0; i < artifactSlots.Length; i++)
            {
                if (artifactSlots[i] != null)
                {
                    artifactSlots[i].ClearArtifact();
                }
            }

            if (atkText != null) atkText.text = string.Empty;
            if (defText != null) defText.text = string.Empty;
            if (hpText != null) hpText.text = string.Empty;

            SetActiveState(false);
        }

        public void SetActiveState(bool isActive)
        {
            if (activeIndicator != null)
            {
                activeIndicator.SetActive(isActive);
            }
        }

        public void SetLockedVisualState(bool isLocked)
        {
            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(isLocked);
            }

            if (padlockOverlay != null)
            {
                padlockOverlay.SetActive(isLocked);
            }
        }

        public void SwapWith(TeamSlotUI other)
        {
            if (other == null)
            {
                return;
            }

            CharacterItem tempChar = currentCharacter;
            Artifact[] tempArtifacts = GetEquippedArtifacts();

            SetCharacter(other.currentCharacter);
            SetArtifacts(other.GetEquippedArtifacts());

            other.SetCharacter(tempChar);
            other.SetArtifacts(tempArtifacts);
        }

        public void RecalculateStats()
        {
            if (currentCharacter == null || currentCharacter.characterObjectData == null)
            {
                if (atkText != null) atkText.text = string.Empty;
                if (defText != null) defText.text = string.Empty;
                if (hpText != null) hpText.text = string.Empty;
                return;
            }

            CharacterStats baseStats = currentCharacter.characterObjectData.stats;
            float bonusAttack = 0f;
            float bonusDefense = 0f;
            float bonusHp = 0f;

            for (int i = 0; i < artifactSlots.Length; i++)
            {
                ArtifactSlotUI slot = artifactSlots[i];
                if (slot == null || slot.CurrentArtifact == null)
                {
                    continue;
                }

                bonusAttack += slot.CurrentArtifact.bonusATK;
                bonusDefense += slot.CurrentArtifact.bonusDEF;
                bonusHp += slot.CurrentArtifact.bonusHP;
            }

            UpdateStatFeedback(atkText, ref atkFeedbackCoroutine, ref lastAttackBase, ref lastAttackBonus, baseStats.attack, bonusAttack);
            UpdateStatFeedback(defText, ref defFeedbackCoroutine, ref lastDefenseBase, ref lastDefenseBonus, baseStats.defense, bonusDefense);
            UpdateStatFeedback(hpText, ref hpFeedbackCoroutine, ref lastHpBase, ref lastHpBonus, baseStats.maxHP, bonusHp);
            hasStatSnapshot = true;
        }

        public CharacterItem GetCharacterItem()
        {
            return currentCharacter;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (parentBuilder == null || parentBuilder.CurrentMode == TeamBuilderMode.CombatZone)
            {
                return;
            }

            TeamSlotUI draggedSlot = eventData.pointerDrag?.GetComponent<TeamSlotUI>();
            if (draggedSlot != null)
            {
                if (draggedSlot == this || draggedSlot.CurrentCharacter == null)
                {
                    return;
                }

                SwapWith(draggedSlot);
                parentBuilder.OnSlotChanged();
                PlayPlacementFeedback();
                draggedSlot.PlayPlacementFeedback();
                return;
            }

            CharacterGridItemUI draggedItem = eventData.pointerDrag?.GetComponent<CharacterGridItemUI>();
            if (draggedItem == null || draggedItem.CharacterData == null)
            {
                return;
            }

            CharacterItem incomingChar = draggedItem.CharacterData;
            TeamSlotUI existingSlot = parentBuilder.FindSlotWithCharacter(incomingChar);

            if (existingSlot != null && existingSlot != this)
            {
                existingSlot.SetCharacter(currentCharacter);
                SetCharacter(incomingChar);
            }
            else if (existingSlot == this)
            {
                return;
            }
            else
            {
                SetCharacter(incomingChar);
            }

            parentBuilder.OnSlotChanged();
            PlayPlacementFeedback();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (currentCharacter == null || parentBuilder == null)
            {
                return;
            }

            if (parentBuilder.CurrentMode == TeamBuilderMode.CombatZone)
            {
                return;
            }
            else
            {
                parentBuilder.SetActiveIndex(slotIndex);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (currentCharacter == null || parentBuilder == null)
            {
                return;
            }

            if (parentBuilder.CurrentMode != TeamBuilderMode.CombatZone)
            {
                return;
            }

            CancelInspectHold();
            inspectHoldCoroutine = StartCoroutine(InspectHoldRoutine());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            CancelInspectHold();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelInspectHold();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            CancelInspectHold();

            if (parentBuilder != null && parentBuilder.CurrentMode == TeamBuilderMode.CombatZone)
            {
                PlayLockedFeedback();
                return;
            }

            if (currentCharacter == null || profileImage == null || profileImage.sprite == null)
            {
                return;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0.5f;
                canvasGroup.blocksRaycasts = false;
            }

            CreateDragGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateDragGhostPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            bool shouldRemoveFromSlot = parentBuilder != null && parentBuilder.IsSlotRemovalDropTarget(eventData);

            ResetDragState();

            if (currentCharacter == null || parentBuilder == null)
            {
                return;
            }

            if (shouldRemoveFromSlot)
            {
                parentBuilder.TryRemoveCharacterFromSlot(slotIndex);
            }
        }

        public void HandleArtifactSlotClicked(ArtifactSlotUI slot, int direction)
        {
            if (slot == null || parentBuilder == null)
            {
                return;
            }

            if (parentBuilder.CurrentMode == TeamBuilderMode.CombatZone)
            {
                PlayLockedFeedback();
                return;
            }

            if (currentCharacter == null)
            {
                slot.ClearArtifact();
                return;
            }

            if (parentBuilder.TryResolveArtifactSelection(slot.GetArtifact(), direction, out Artifact nextArtifact))
            {
                slot.SetArtifact(nextArtifact);
                parentBuilder.OnSlotChanged();
            }
        }

        public Artifact[] GetEquippedArtifactsSnapshot()
        {
            return GetEquippedArtifacts();
        }

        public void SetEquippedArtifacts(Artifact[] artifacts)
        {
            SetArtifacts(artifacts);
            RecalculateStats();
        }

        private Artifact[] GetEquippedArtifacts()
        {
            Artifact[] artifacts = new Artifact[artifactSlots.Length];
            for (int i = 0; i < artifactSlots.Length; i++)
            {
                artifacts[i] = artifactSlots[i] != null ? artifactSlots[i].GetArtifact() : null;
            }

            return artifacts;
        }

        private void SetArtifacts(Artifact[] artifacts)
        {
            if (artifacts == null)
            {
                return;
            }

            for (int i = 0; i < artifactSlots.Length && i < artifacts.Length; i++)
            {
                if (artifactSlots[i] != null)
                {
                    artifactSlots[i].SetArtifact(artifacts[i]);
                }
            }
        }

        private void OnDisable()
        {
            CancelInspectHold();
            ResetDragState();
        }

        private void OnDestroy()
        {
            CancelInspectHold();
            ResetDragState();
        }

        private void ResetDragState()
        {
            DestroyDragGhost();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = baseAnchoredPosition;
            }
        }

        private void CreateDragGhost(PointerEventData eventData)
        {
            if (parentBuilder == null || parentBuilder.RootCanvas == null || profileImage == null || profileImage.sprite == null)
            {
                return;
            }

            GameObject ghostObject = new GameObject("TeamSlotDragGhost", typeof(RectTransform), typeof(Image));
            dragGhost = ghostObject.transform;
            dragGhost.SetParent(parentBuilder.RootCanvas.transform, false);

            Image ghostImage = ghostObject.GetComponent<Image>();
            ghostImage.sprite = profileImage.sprite;
            ghostImage.color = new Color(1f, 1f, 1f, 0.78f);
            ghostImage.raycastTarget = false;

            RectTransform sourceRect = profileImage.rectTransform != null ? profileImage.rectTransform : rectTransform;
            RectTransform ghostRect = ghostObject.GetComponent<RectTransform>();
            ghostRect.anchorMin = Vector2.zero;
            ghostRect.anchorMax = Vector2.zero;
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.sizeDelta = sourceRect != null ? sourceRect.rect.size : new Vector2(80f, 80f);

            UpdateDragGhostPosition(eventData);
        }

        private void UpdateDragGhostPosition(PointerEventData eventData)
        {
            if (dragGhost == null || parentBuilder == null || parentBuilder.RootCanvas == null || eventData == null)
            {
                return;
            }

            RectTransform ghostRect = dragGhost as RectTransform;
            RectTransform canvasRect = parentBuilder.RootCanvas.GetComponent<RectTransform>();
            Camera eventCamera = parentBuilder.RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : parentBuilder.RootCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventCamera, out Vector2 localPoint))
            {
                Vector2 canvasSize = canvasRect.rect.size;
                Vector2 pivotOffset = canvasRect.pivot * canvasSize;
                ghostRect.anchoredPosition = localPoint + pivotOffset;
            }
        }

        private void DestroyDragGhost()
        {
            if (dragGhost == null)
            {
                return;
            }

            Destroy(dragGhost.gameObject);
            dragGhost = null;
        }

        private void PlayLockedFeedback()
        {
            if (lockFeedbackCoroutine != null)
            {
                StopCoroutine(lockFeedbackCoroutine);
            }

            if (uiAudioSource != null && lockedActionSfx != null)
            {
                uiAudioSource.PlayOneShot(lockedActionSfx);
            }

            feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);
            if (feedbackDriver != null && rectTransform != null)
            {
                feedbackDriver.PlayWiggle(rectTransform, baseAnchoredPosition, lockedWiggleDistance, lockedWiggleDuration);
                if (padlockOverlay != null)
                {
                    feedbackDriver.PlayPulse(padlockOverlay.transform, padlockPulseScale, lockedWiggleDuration);
                }

                if (lockedOverlay != null && lockedOverlay.TryGetComponent(out Graphic lockGraphic))
                {
                    feedbackDriver.PlayFlash(lockGraphic, new Color(1f, 0.2f, 0.2f, lockGraphic.color.a), lockedWiggleDuration);
                }

                return;
            }

            lockFeedbackCoroutine = StartCoroutine(PlayLockedFeedbackRoutine());
        }

        private void PlayPlacementFeedback()
        {
            feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);
            if (feedbackDriver == null || rectTransform == null)
            {
                return;
            }

            feedbackDriver.PlayPulse(rectTransform, 1.08f, 0.12f);
            feedbackDriver.PlayRadialBurst(rectTransform, placementBurstColor, 8, 34f, 0.12f);
        }

        private IEnumerator InspectHoldRoutine()
        {
            float elapsed = 0f;
            float requiredDuration = Mathf.Max(0.05f, inspectHoldDuration);

            while (elapsed < requiredDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            inspectHoldCoroutine = null;
            parentBuilder.OpenInspectScreen(currentCharacter, GetEquippedArtifactsSnapshot());
        }

        private void CancelInspectHold()
        {
            if (inspectHoldCoroutine == null)
            {
                return;
            }

            StopCoroutine(inspectHoldCoroutine);
            inspectHoldCoroutine = null;
        }

        private IEnumerator PlayLockedFeedbackRoutine()
        {
            if (rectTransform == null)
            {
                yield break;
            }

            Vector3 originalPadlockScale = padlockOverlay != null ? padlockOverlay.transform.localScale : Vector3.one;
            float elapsed = 0f;

            while (elapsed < lockedWiggleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / lockedWiggleDuration);
                float damping = 1f - normalized;
                float wiggle = Mathf.Sin(normalized * Mathf.PI * 6f) * lockedWiggleDistance * damping;
                rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(wiggle, 0f);

                if (padlockOverlay != null)
                {
                    float pulse = Mathf.Lerp(1f, padlockPulseScale, Mathf.Sin(normalized * Mathf.PI));
                    padlockOverlay.transform.localScale = Vector3.one * pulse;
                }

                yield return null;
            }

            rectTransform.anchoredPosition = baseAnchoredPosition;
            if (padlockOverlay != null)
            {
                padlockOverlay.transform.localScale = originalPadlockScale;
            }

            lockFeedbackCoroutine = null;
        }

        private void UpdateStatFeedback(
            TMP_Text targetText,
            ref Coroutine activeCoroutine,
            ref float previousBaseValue,
            ref float previousBonusValue,
            float nextBaseValue,
            float nextBonusValue)
        {
            if (targetText == null)
            {
                previousBaseValue = nextBaseValue;
                previousBonusValue = nextBonusValue;
                return;
            }

            bool shouldAnimate = hasStatSnapshot
                && (Mathf.Abs(previousBaseValue - nextBaseValue) > Mathf.Epsilon
                    || Mathf.Abs(previousBonusValue - nextBonusValue) > Mathf.Epsilon);

            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }

            if (!shouldAnimate)
            {
                targetText.text = FormatStatValue(nextBaseValue, nextBonusValue);
                targetText.color = statNormalColor;
                targetText.transform.localScale = Vector3.one;
            }
            else
            {
                activeCoroutine = StartCoroutine(AnimateStatFeedback(
                    targetText,
                    previousBaseValue,
                    previousBonusValue,
                    nextBaseValue,
                    nextBonusValue));
            }

            previousBaseValue = nextBaseValue;
            previousBonusValue = nextBonusValue;
        }

        private IEnumerator AnimateStatFeedback(
            TMP_Text targetText,
            float startBaseValue,
            float startBonusValue,
            float endBaseValue,
            float endBonusValue)
        {
            float elapsed = 0f;

            while (elapsed < statFeedbackDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / statFeedbackDuration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                float currentBase = Mathf.Lerp(startBaseValue, endBaseValue, eased);
                float currentBonus = Mathf.Lerp(startBonusValue, endBonusValue, eased);

                targetText.text = FormatStatValue(currentBase, currentBonus);
                targetText.color = Color.Lerp(statBoostColor, statNormalColor, t);
                targetText.transform.localScale = Vector3.one * Mathf.Lerp(statFeedbackScale, 1f, t);
                yield return null;
            }

            targetText.text = FormatStatValue(endBaseValue, endBonusValue);
            targetText.color = statNormalColor;
            targetText.transform.localScale = Vector3.one;
        }

        private static string FormatStatValue(float baseValue, float bonusValue)
        {
            return $"{Mathf.RoundToInt(baseValue)}(+{Mathf.RoundToInt(bonusValue)})";
        }
    }
}
