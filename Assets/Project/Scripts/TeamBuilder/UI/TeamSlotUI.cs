using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.UI;
using Project.Core.ZoneState;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// Individual team slot that supports safe-zone editing and combat-zone inspection.
    /// </summary>
    public class TeamSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        [Header("Character Display")]
        [SerializeField] private Image profileImage;
        [SerializeField] private Image elementIcon;
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text levelText;

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
        [SerializeField] private float lockedWiggleDuration = 0.25f;
        [SerializeField] private float lockedWiggleDistance = 8f;
        [SerializeField] private float padlockPulseScale = 1.15f;

        [Header("Stat Feedback")]
        [SerializeField] private Color statNormalColor = Color.white;
        [SerializeField] private Color statBoostColor = new Color(0.45f, 1f, 0.45f, 1f);
        [SerializeField] private Color statDowngradeColor = new Color(1f, 0.4f, 0.4f, 1f);
        [SerializeField] private Color placementBurstColor = new Color(0.75f, 1f, 0.9f, 0.9f);
        [SerializeField] private float statFeedbackDuration = 0.3f;
        [SerializeField] private float statFeedbackScale = 1.2f;



        private CharacterItem currentCharacter;
        private TeamBuilderUI parentBuilder;
        private int slotIndex;
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Transform dragGhost;
        private Vector2 baseAnchoredPosition;
        private UIFeedbackDriver feedbackDriver;

        private Coroutine atkFeedbackCoroutine;
        private Coroutine defFeedbackCoroutine;
        private Coroutine hpFeedbackCoroutine;
        private Coroutine rmbHoldCoroutine;
        private float lastAttackBase;
        private float lastAttackBonus;
        private float lastDefenseBase;
        private float lastDefenseBonus;
        private float lastHpBase;
        private float lastHpBonus;
        private CharacterStats lastEffectiveStats;
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

            if (levelText != null)
            {
                CharacterUpgrade upgrade = charObj != null && parentBuilder != null
                    ? parentBuilder.ResolveCharacterUpgrade(charObj)
                    : (charObj != null ? charObj.upgrade : null);

                levelText.text = upgrade != null
                    ? upgrade.LevelLabel
                    : "Lv.1/30";
            }

            if (elementIcon != null && charObj != null)
            {
                var db = ElementDatabase.Load();
                Sprite sprite = db != null ? db.GetIcon(charObj.elementType) : null;
                elementIcon.sprite = sprite;
                elementIcon.enabled = sprite != null;
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

            if (levelText != null)
            {
                levelText.text = string.Empty;
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
            RecalculateStats();

            other.SetCharacter(tempChar);
            other.SetArtifacts(tempArtifacts);
            other.RecalculateStats();
        }

        public void RecalculateStats()
        {
            if (currentCharacter == null || currentCharacter.characterObjectData == null)
            {
                if (atkText != null) atkText.text = string.Empty;
                if (defText != null) defText.text = string.Empty;
                if (hpText != null) hpText.text = string.Empty;
                hasStatSnapshot = false;
                return;
            }

            CharacterObject charObj = currentCharacter.characterObjectData;
            CharacterStats baseStats = parentBuilder != null
                ? parentBuilder.ResolveCharacterPermanentStats(charObj)
                : charObj.GetLeveledStats();
            CharacterStats effectiveStats = parentBuilder != null
                ? parentBuilder.ResolveCharacterRuntimeStats(charObj, GetEquippedArtifacts())
                : baseStats.Clone();

            UpdateStatFeedback(atkText, ref atkFeedbackCoroutine, ref lastAttackBase, ref lastAttackBonus, baseStats.attack, effectiveStats.attack, "atk");
            UpdateStatFeedback(defText, ref defFeedbackCoroutine, ref lastDefenseBase, ref lastDefenseBonus, baseStats.defense, effectiveStats.defense, "def");
            UpdateStatFeedback(hpText, ref hpFeedbackCoroutine, ref lastHpBase, ref lastHpBonus, baseStats.maxHP, effectiveStats.maxHP, "hp");

            if (hasStatSnapshot && lastEffectiveStats != null)
            {
                float trend = CalculateStatTrend(lastEffectiveStats, effectiveStats);
                if (Mathf.Abs(trend) > 0.001f)
                {
                    PlayOverallStatFeedback(trend > 0f);
                }
            }

            lastEffectiveStats = effectiveStats.Clone();
            hasStatSnapshot = true;
        }



        public void OnDrop(PointerEventData eventData)
        {
            if (parentBuilder == null || parentBuilder.CurrentMode == ZoneMode.CombatZone)
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

            // Character inventory items are click-to-switch per GDD. Team slots remain draggable.
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (parentBuilder == null)
            {
                return;
            }

            if (parentBuilder.CurrentMode == ZoneMode.CombatZone)
            {
                return;
            }
            else
            {
                parentBuilder.BeginCharacterSwitch(slotIndex);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (currentCharacter == null || parentBuilder == null) return;
            if (parentBuilder.CurrentMode == ZoneMode.CombatZone)
            {
                PlayLockedFeedback();
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (rmbHoldCoroutine != null)
                {
                    StopCoroutine(rmbHoldCoroutine);
                }
                rmbHoldCoroutine = StartCoroutine(RmbHoldRoutine());
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (rmbHoldCoroutine != null)
                {
                    StopCoroutine(rmbHoldCoroutine);
                    rmbHoldCoroutine = null;
                }
            }
        }

        private IEnumerator RmbHoldRoutine()
        {
            yield return new WaitForSecondsRealtime(0.4f);

            if (currentCharacter != null && currentCharacter.characterObjectData != null)
            {
                string charName = currentCharacter.characterObjectData.name;
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.OpenInspectorWithCharacter(charName);
                }
                else if (parentBuilder != null && parentBuilder.InspectScreenUI != null)
                {
                    // Fallback if UIManager is not yet set up in the scene.
                    parentBuilder.InspectScreenUI.OpenInspectorWithCharacter(charName);
                }
            }
            rmbHoldCoroutine = null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (rmbHoldCoroutine != null)
            {
                StopCoroutine(rmbHoldCoroutine);
                rmbHoldCoroutine = null;
            }

            if (parentBuilder != null && parentBuilder.CurrentMode == ZoneMode.CombatZone)
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

            if (parentBuilder.CurrentMode == ZoneMode.CombatZone)
            {
                PlayLockedFeedback();
                return;
            }

            if (currentCharacter == null)
            {
                slot.ClearArtifact();
                return;
            }

            // Right-click = remove artifact
            if (direction < 0)
            {
                slot.ClearArtifact();
                RecalculateStats();
                parentBuilder.OnSlotChanged();
            }
        }

        public void HandleArtifactDrop(ArtifactSlotUI targetSlot, Artifact newArtifact)
        {
            if (parentBuilder == null || currentCharacter == null || targetSlot == null || newArtifact == null) return;

            if (parentBuilder.CurrentMode == ZoneMode.CombatZone)
            {
                PlayLockedFeedback();
                return;
            }

            // Check if this artifact is already equipped somewhere else
            TeamSlotUI existingSlot = parentBuilder.FindSlotWithArtifact(newArtifact);
            if (existingSlot != null)
            {
                ArtifactSlotUI oldSubSlot = existingSlot.FindArtifactSlotWith(newArtifact);
                if (oldSubSlot != null)
                {
                    oldSubSlot.ClearArtifact();
                    if (existingSlot != this)
                    {
                        existingSlot.RecalculateStats();
                    }
                }
            }

            targetSlot.SetArtifact(newArtifact);
            RecalculateStats();
            parentBuilder.OnSlotChanged();
        }

        public void HandleArtifactSwap(ArtifactSlotUI targetSlot, ArtifactSlotUI sourceSlot)
        {
            if (parentBuilder == null || currentCharacter == null || targetSlot == null || sourceSlot == null) return;

            if (parentBuilder.CurrentMode == ZoneMode.CombatZone)
            {
                PlayLockedFeedback();
                return;
            }

            Artifact temp = targetSlot.GetArtifact();
            TeamSlotUI sourceParent = sourceSlot.ParentSlot;
            targetSlot.SetArtifact(sourceSlot.GetArtifact());
            sourceSlot.SetArtifact(temp);

            RecalculateStats();
            if (sourceParent != null && sourceParent != this)
            {
                sourceParent.RecalculateStats();
            }
            parentBuilder.OnSlotChanged();
        }

        public ArtifactSlotUI FindArtifactSlotWith(Artifact artifact)
        {
            if (artifact == null) return null;

            for (int i = 0; i < artifactSlots.Length; i++)
            {
                if (artifactSlots[i] != null && artifactSlots[i].GetArtifact() == artifact)
                {
                    return artifactSlots[i];
                }
            }
            return null;
        }

        public Artifact[] GetEquippedArtifacts()
        {
            Artifact[] artifacts = new Artifact[artifactSlots.Length];
            for (int i = 0; i < artifactSlots.Length; i++)
            {
                artifacts[i] = artifactSlots[i] != null ? artifactSlots[i].GetArtifact() : null;
            }

            return artifacts;
        }

        public void SetEquippedArtifacts(Artifact[] artifacts)
        {
            SetArtifacts(artifacts);
            RecalculateStats();
        }

        private void SetArtifacts(Artifact[] artifacts)
        {
            for (int i = 0; i < artifactSlots.Length; i++)
            {
                if (artifactSlots[i] != null)
                {
                    Artifact artifact = artifacts != null && i < artifacts.Length
                        ? artifacts[i]
                        : null;
                    artifactSlots[i].SetArtifact(artifact);
                }
            }
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

            RectTransform sourceRect = profileImage.rectTransform != null ? profileImage.rectTransform : rectTransform;
            Vector2 size = sourceRect != null ? sourceRect.rect.size : new Vector2(80f, 80f);
            dragGhost = UIDragGhostHelper.CreateGhost(parentBuilder.RootCanvas, profileImage.sprite, size, 0.78f, "TeamSlotDragGhost");
        }

        private void UpdateDragGhostPosition(PointerEventData eventData)
        {
            if (dragGhost == null || parentBuilder == null || parentBuilder.RootCanvas == null || eventData == null)
            {
                return;
            }

            UIDragGhostHelper.UpdatePosition(dragGhost, parentBuilder.RootCanvas, eventData.position);
        }

        private void DestroyDragGhost()
        {
            UIDragGhostHelper.DestroyGhost(ref dragGhost);
        }

        private void PlayLockedFeedback()
        {
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
            }
        }

        private void PlayPlacementFeedback()
        {
            feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);
            if (feedbackDriver != null && rectTransform != null)
            {
                feedbackDriver.PlayPulse(rectTransform, 1.08f, 0.12f);
                feedbackDriver.PlayRadialBurst(rectTransform, placementBurstColor, 8, 34f, 0.12f);
            }
        }

        private static string FormatStatValue(float baseValue, float bonusValue, string statName)
        {
            return StatFormatHelper.FormatResolved(baseValue, bonusValue, statName, true);
        }

        private void UpdateStatFeedback(
            TMP_Text targetText,
            ref Coroutine activeCoroutine,
            ref float previousBaseValue,
            ref float previousBonusValue,
            float nextBaseValue,
            float nextBonusValue,
            string statName)
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
                targetText.text = FormatStatValue(nextBaseValue, nextBonusValue, statName);
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
                    nextBonusValue,
                    statName));
            }

            previousBaseValue = nextBaseValue;
            previousBonusValue = nextBonusValue;
        }

        private IEnumerator AnimateStatFeedback(
            TMP_Text targetText,
            float startBaseValue,
            float startBonusValue,
            float endBaseValue,
            float endBonusValue,
            string statName)
        {
            float elapsed = 0f;

            // Determine if upgrade or downgrade
            string statKey = statName != null ? statName.ToLowerInvariant() : "";
            bool isPercentMultiplier = statKey.Contains("hp") || statKey.Contains("atk") || statKey.Contains("def");

            float startTotal = isPercentMultiplier
                ? startBonusValue
                : startBonusValue;

            float endTotal = isPercentMultiplier
                ? endBonusValue
                : endBonusValue;

            bool isUpgrade = endTotal > startTotal;
            Color flashColor = isUpgrade ? statBoostColor : statDowngradeColor;

            while (elapsed < statFeedbackDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / statFeedbackDuration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                float currentBase = Mathf.Lerp(startBaseValue, endBaseValue, eased);
                float currentBonus = Mathf.Lerp(startBonusValue, endBonusValue, eased);

                targetText.text = FormatStatValue(currentBase, currentBonus, statName);
                targetText.color = Color.Lerp(flashColor, statNormalColor, t);
                targetText.transform.localScale = Vector3.one * Mathf.Lerp(statFeedbackScale, 1f, t);
                yield return null;
            }

            targetText.text = FormatStatValue(endBaseValue, endBonusValue, statName);
            targetText.color = statNormalColor;
            targetText.transform.localScale = Vector3.one;
        }

        private void PlayOverallStatFeedback(bool isUpgrade)
        {
            feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);
            if (feedbackDriver == null)
            {
                return;
            }

            Color feedbackColor = isUpgrade ? statBoostColor : statDowngradeColor;
            Graphic feedbackGraphic = profileImage != null ? (Graphic)profileImage : atkText;
            if (feedbackGraphic != null)
            {
                feedbackDriver.PlayFlash(feedbackGraphic, feedbackColor, statFeedbackDuration);
            }

            if (rectTransform != null)
            {
                float cardPulseScale = Mathf.Lerp(1f, statFeedbackScale, 0.45f);
                feedbackDriver.PlayPulse(rectTransform, cardPulseScale, statFeedbackDuration);
            }
        }

        private static float CalculateStatTrend(CharacterStats previous, CharacterStats next)
        {
            if (previous == null || next == null)
            {
                return 0f;
            }

            float trend = 0f;
            trend += NormalizeDelta(previous.maxHP, next.maxHP);
            trend += NormalizeDelta(previous.attack, next.attack);
            trend += NormalizeDelta(previous.defense, next.defense);
            trend += NormalizeDelta(previous.speed, next.speed);
            trend += NormalizeDelta(previous.elementalMastery, next.elementalMastery);
            trend += NormalizeDelta(previous.critRate, next.critRate);
            trend += NormalizeDelta(previous.critDamage, next.critDamage);
            trend += NormalizeDelta(previous.cooldownRate, next.cooldownRate);
            trend += NormalizeDelta(previous.impact, next.impact);
            trend += NormalizeDelta(previous.aeRecharge, next.aeRecharge);
            return trend;
        }

        private static float NormalizeDelta(float previous, float next)
        {
            float denominator = Mathf.Max(1f, Mathf.Abs(previous));
            return (next - previous) / denominator;
        }
    }
}
