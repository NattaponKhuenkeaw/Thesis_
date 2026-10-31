using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.InventorySystem.Artifacts;
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
        [SerializeField] private Button changeCharacterButton;

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
        private TMP_Text emptySlotLabel;
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
            EnsureEmptySlotLabel();
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
            feedbackDriver = UIFeedbackDriver.GetOrAdd(gameObject);
            if (changeCharacterButton != null)
            {
                changeCharacterButton.onClick.RemoveListener(ChangeCharacter);
                changeCharacterButton.onClick.AddListener(ChangeCharacter);
            }

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
            EnsureEmptySlotLabel();
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
                int globalLevel = parentBuilder != null ? parentBuilder.ResolveRunGlobalLevel() : 1;
                int globalMaxLevel = parentBuilder != null ? parentBuilder.ResolveRunGlobalMaxLevel() : 30;
                levelText.text = $"Lv.{globalLevel}/{globalMaxLevel}";
            }

            if (elementIcon != null && charObj != null)
            {
                elementIcon.enabled = false;
            }

            if (emptyStateOverlay != null)
            {
                emptyStateOverlay.SetActive(false);
            }
            if (emptySlotLabel != null) emptySlotLabel.gameObject.SetActive(false);

            RecalculateStats();
        }

        public void ClearSlot()
        {
            ResetDragState();
            EnsureEmptySlotLabel();
            currentCharacter = null;
            hasStatSnapshot = false;

            if (profileImage != null)
            {
                profileImage.sprite = null;
                profileImage.color = Color.white;
                profileImage.enabled = false;
            }

            if (characterNameText != null)
            {
                characterNameText.text = string.Empty;
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
                emptyStateOverlay.SetActive(false);
            }
            if (emptySlotLabel != null) emptySlotLabel.gameObject.SetActive(true);

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

        private void EnsureEmptySlotLabel()
        {
            if (emptySlotLabel != null) return;
            Transform existing = transform.Find("Empty Slot Plus");
            if (existing != null) emptySlotLabel = existing.GetComponent<TMP_Text>();
            if (emptySlotLabel == null)
            {
                var labelObject = new GameObject("Empty Slot Plus", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(transform, false);
                var rect = (RectTransform)labelObject.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(96f, 96f);
                emptySlotLabel = labelObject.GetComponent<TMP_Text>();
                emptySlotLabel.font = characterNameText != null ? characterNameText.font : TMP_Settings.defaultFontAsset;
                emptySlotLabel.fontSize = 64f;
                emptySlotLabel.fontStyle = FontStyles.Bold;
                emptySlotLabel.color = new Color(1f, 1f, 1f, .85f);
                emptySlotLabel.alignment = TextAlignmentOptions.Center;
                emptySlotLabel.textWrappingMode = TextWrappingModes.NoWrap;
                emptySlotLabel.raycastTarget = false;
            }
            emptySlotLabel.text = "+";
            emptySlotLabel.transform.SetAsLastSibling();
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
            if (changeCharacterButton != null) changeCharacterButton.gameObject.SetActive(!isLocked);
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
            ArtifactInstance[] tempArtifacts = GetEquippedArtifacts();

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
            // Character selection has its own button; portrait input retains drag and inspect behavior.
        }

        public void ChangeCharacter() => parentBuilder?.BeginCharacterSwitch(slotIndex);

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

                if (parentBuilder != null && parentBuilder.InspectScreenUI != null)
                {
                    parentBuilder.InspectScreenUI.RefreshCurrentArtifactInstances(
                        GetEquippedArtifacts(),
                        parentBuilder.PlayerInventory);
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
                parentBuilder.CancelArtifactSwitch();
                slot.ClearArtifact();
                return;
            }

            // Right-click = remove artifact
            if (direction < 0)
            {
                slot.ClearArtifact();
                RecalculateStats();
                parentBuilder.OnSlotChanged();
                return;
            }

            parentBuilder.BeginArtifactSwitch(slot);
        }

        public void HandleArtifactSlotDragStarted()
        {
            parentBuilder?.CancelArtifactSwitch();
        }

        public void HandleArtifactDrop(ArtifactSlotUI targetSlot, ArtifactInstance newArtifact)
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

            targetSlot.SetArtifact(newArtifact, ResolveDefinition(newArtifact));
            parentBuilder.PlayerInventory?.TryMarkArtifactSeen(newArtifact.instanceId);
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

            ArtifactInstance temp = targetSlot.GetArtifact();
            ArtifactDefinition tempDefinition = targetSlot.CurrentDefinition;
            TeamSlotUI sourceParent = sourceSlot.ParentSlot;
            targetSlot.SetArtifact(sourceSlot.GetArtifact(), sourceSlot.CurrentDefinition);
            sourceSlot.SetArtifact(temp, tempDefinition);

            RecalculateStats();
            if (sourceParent != null && sourceParent != this)
            {
                sourceParent.RecalculateStats();
            }
            parentBuilder.OnSlotChanged();
        }

        public ArtifactSlotUI FindArtifactSlotWith(ArtifactInstance artifact)
        {
            if (artifact == null) return null;

            for (int i = 0; i < artifactSlots.Length; i++)
            {
                ArtifactInstance equipped = artifactSlots[i] != null ? artifactSlots[i].GetArtifact() : null;
                if (equipped != null && equipped.instanceId == artifact.instanceId)
                {
                    return artifactSlots[i];
                }
            }
            return null;
        }

        public ArtifactSlotUI GetFirstEmptyOrFirstArtifactSlot()
        {
            if (artifactSlots == null || artifactSlots.Length == 0) return null;
            for (int i = 0; i < artifactSlots.Length; i++)
            {
                if (artifactSlots[i] != null && artifactSlots[i].IsEmpty)
                    return artifactSlots[i];
            }
            return artifactSlots[0];
        }

        public ArtifactInstance[] GetEquippedArtifacts()
        {
            ArtifactInstance[] artifacts = new ArtifactInstance[artifactSlots.Length];
            for (int i = 0; i < artifactSlots.Length; i++)
            {
                artifacts[i] = artifactSlots[i] != null ? artifactSlots[i].GetArtifact() : null;
            }

            return artifacts;
        }

        public ArtifactInstanceRef[] GetEquippedArtifactRefs()
        {
            ArtifactInstanceRef[] refs = new ArtifactInstanceRef[artifactSlots.Length];
            for (int i = 0; i < artifactSlots.Length; i++)
            {
                ArtifactInstance artifact = artifactSlots[i] != null ? artifactSlots[i].GetArtifact() : null;
                refs[i] = new ArtifactInstanceRef(artifact != null ? artifact.instanceId : string.Empty);
            }

            return refs;
        }

        public void SetEquippedArtifacts(ArtifactInstance[] artifacts)
        {
            SetArtifacts(artifacts);
            RecalculateStats();
        }

        private void SetArtifacts(ArtifactInstance[] artifacts)
        {
            for (int i = 0; i < artifactSlots.Length; i++)
            {
                if (artifactSlots[i] != null)
                {
                    ArtifactInstance artifact = artifacts != null && i < artifacts.Length
                        ? artifacts[i]
                        : null;
                    artifactSlots[i].SetArtifact(artifact, ResolveDefinition(artifact));
                }
            }
        }

        private ArtifactDefinition ResolveDefinition(ArtifactInstance artifact)
        {
            return artifact != null && parentBuilder != null && parentBuilder.PlayerInventory != null
                ? parentBuilder.PlayerInventory.ResolveArtifactDefinition(artifact.definitionId)
                : null;
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
                feedbackDriver.PlayPulse(rectTransform, 1.12f, lockedWiggleDuration);
            }
        }

        private void PlayPlacementFeedback()
        {
            feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);
            if (feedbackDriver != null && rectTransform != null)
            {
                feedbackDriver.PlayPulse(rectTransform, 1.08f, 0.12f);
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

            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }
            targetText.text = FormatStatValue(nextBaseValue, nextBonusValue, statName);
            targetText.color = statNormalColor;
            previousBaseValue = nextBaseValue;
            previousBonusValue = nextBonusValue;
        }

        private void PlayOverallStatFeedback(bool isUpgrade)
        {
            feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);
            if (feedbackDriver == null)
            {
                return;
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
            trend += NormalizeDelta(previous.dotMastery, next.dotMastery);
            trend += NormalizeDelta(previous.critRate, next.critRate);
            trend += NormalizeDelta(previous.critDamage, next.critDamage);
            trend += NormalizeDelta(previous.cooldownMastery, next.cooldownMastery);
            trend += NormalizeDelta(previous.damageIncrease, next.damageIncrease);
            trend += NormalizeDelta(previous.additionalDamageIncrease, next.additionalDamageIncrease);
            trend += NormalizeDelta(previous.auraEnergyRecharge, next.auraEnergyRecharge);
            return trend;
        }

        private static float NormalizeDelta(float previous, float next)
        {
            float denominator = Mathf.Max(1f, Mathf.Abs(previous));
            return (next - previous) / denominator;
        }
    }
}
