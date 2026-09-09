using System.Collections;
using System.Collections.Generic;
using Project.ShopSystem;
using Project.UI;
using UnityEngine;

namespace Project.Progression.LevelRoom
{
    /// <summary>
    /// Owns the GDD Level-room presentation sequence and applies Run Global Level once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelRoomController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private PlayerRunInventory runInventory;
        [SerializeField] private LevelRoomDisplay display;
        [SerializeField] private LevelRoomInteractable interactable;
        [SerializeField] private Transform cameraFocusTarget;
        [SerializeField] private Canvas sequenceCanvas;

        [Header("Claim Policy")]
        [Tooltip("If true, a completed reveal cannot be claimed again until the room is reloaded.")]
        [SerializeField] private bool oneClaimPerVisit = true;

        [Header("Timing (feel pass later)")]
        [SerializeField, Min(0.05f)] private float cameraBlendSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float cameraReturnBlendSeconds = 0.65f;
        [SerializeField, Min(0.1f)] private float cycleDurationSeconds = 1.4f;
        [SerializeField, Min(0.05f)] private float cycleStepSeconds = 0.12f;
        [SerializeField, Min(0.1f)] private float revealHoldSeconds = 0.85f;
        [SerializeField, Min(0f)] private float restoreDelaySeconds = 0.45f;

        [Header("Audio (optional)")]
        [SerializeField] private AudioClip confirmClip;
        [SerializeField] private AudioClip cycleClip;
        [SerializeField] private AudioClip levelUpClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        private readonly List<Canvas> disabledCanvases = new List<Canvas>(16);
        private Coroutine sequenceRoutine;
        private bool ownsInputLock;
        private bool ownsCameraFocus;
        private bool hasClaimedThisVisit;
        private bool hasAppliedReward;

        public bool IsSequenceActive => sequenceRoutine != null;
        public bool HasClaimedThisVisit => hasClaimedThisVisit;

        private void Awake()
        {
            if (runInventory == null)
            {
                runInventory = Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory");
            }

            if (display == null)
            {
                display = GetComponentInChildren<LevelRoomDisplay>(true);
            }

            if (interactable == null)
            {
                interactable = GetComponentInChildren<LevelRoomInteractable>(true);
            }

            if (cameraFocusTarget == null)
            {
                cameraFocusTarget = display != null ? display.transform : transform;
            }

            display?.ShowIdle();
        }

        private void OnDisable()
        {
            AbortSequenceAndRestoreOwnership();
        }

        private void OnDestroy()
        {
            AbortSequenceAndRestoreOwnership();
        }

        public bool TryBeginSequence()
        {
            if (IsSequenceActive)
            {
                return false;
            }

            if (oneClaimPerVisit && hasClaimedThisVisit)
            {
                display?.ShowClaimed();
                return false;
            }

            if (runInventory == null)
            {
                Debug.LogWarning("[LevelRoomController] Missing PlayerRunInventory.");
                return false;
            }

            sequenceRoutine = StartCoroutine(RunSequence());
            return true;
        }

        private IEnumerator RunSequence()
        {
            hasAppliedReward = false;
            AcquirePresentationOwnership();

            try
            {
                PlaySfx(confirmClip);

                int rolledLevels = RunGlobalLevelReward.Roll();
                yield return StartCoroutine(PlayNumberCycle(rolledLevels));

                int previousLevel = runInventory.GlobalLevel;
                int appliedLevels = 0;
                if (!hasAppliedReward)
                {
                    appliedLevels = runInventory.AddGlobalLevels(rolledLevels);
                    hasAppliedReward = true;
                    if (oneClaimPerVisit)
                    {
                        // Claim is consumed at apply time so a mid-sequence unload cannot re-roll.
                        hasClaimedThisVisit = true;
                    }
                }

                display?.ShowReveal(
                    rolledLevels,
                    appliedLevels,
                    runInventory.GlobalLevel,
                    runInventory.GlobalMaxLevel);

                if (appliedLevels > 0)
                {
                    PlaySfx(levelUpClip);
                }

                Debug.Log(
                    $"[LevelRoomController] Rolled +{rolledLevels}, applied +{appliedLevels}, "
                    + $"Global Lv.{previousLevel} → Lv.{runInventory.GlobalLevel}.");

                yield return new WaitForSecondsRealtime(revealHoldSeconds);

                if (restoreDelaySeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(restoreDelaySeconds);
                }
            }
            finally
            {
                ReleasePresentationOwnership();
                sequenceRoutine = null;

                if (hasClaimedThisVisit)
                {
                    display?.ShowClaimed();
                    interactable?.ForceIdleVisual();
                }
                else
                {
                    display?.ShowIdle();
                }
            }
        }

        private IEnumerator PlayNumberCycle(int finalValue)
        {
            float elapsed = 0f;
            int shown = 1;
            while (elapsed < cycleDurationSeconds)
            {
                shown = (shown % 3) + 1;
                display?.ShowCycling(shown);
                if (cycleClip != null && shown == 1)
                {
                    PlaySfx(cycleClip);
                }

                yield return new WaitForSecondsRealtime(cycleStepSeconds);
                elapsed += cycleStepSeconds;
            }

            display?.ShowCycling(finalValue);
            yield return new WaitForSecondsRealtime(cycleStepSeconds);
        }

        private void AcquirePresentationOwnership()
        {
            if (!ownsInputLock)
            {
                InputLockManager.Lock();
                ownsInputLock = true;
            }

            HideGameplayCanvases();

            if (!ownsCameraFocus && CameraManager.instance != null && cameraFocusTarget != null)
            {
                CameraManager.instance.BeginTemporaryFocus(cameraFocusTarget, cameraBlendSeconds);
                ownsCameraFocus = true;
            }
        }

        private void ReleasePresentationOwnership()
        {
            if (ownsCameraFocus && CameraManager.instance != null)
            {
                CameraManager.instance.EndTemporaryFocus(cameraReturnBlendSeconds);
                ownsCameraFocus = false;
            }

            RestoreGameplayCanvases();

            if (ownsInputLock)
            {
                InputLockManager.Unlock();
                ownsInputLock = false;
            }
        }

        private void AbortSequenceAndRestoreOwnership()
        {
            if (sequenceRoutine != null)
            {
                StopCoroutine(sequenceRoutine);
                sequenceRoutine = null;
            }

            ReleasePresentationOwnership();
        }

        private void HideGameplayCanvases()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || !canvas.enabled)
                {
                    continue;
                }

                if (sequenceCanvas != null && (canvas == sequenceCanvas || canvas.transform.IsChildOf(sequenceCanvas.transform)))
                {
                    continue;
                }

                // Keep world-space Level display canvas visible.
                if (display != null && canvas.transform.IsChildOf(display.transform))
                {
                    continue;
                }

                // Room choice stays armed at the exit; do not bury it under the claim sequence.
                if (canvas.GetComponentInParent<Project.UI.RoomChoiceUI>() != null
                    || canvas.GetComponent<Project.UI.RoomChoiceUI>() != null)
                {
                    continue;
                }

                canvas.enabled = false;
                if (!disabledCanvases.Contains(canvas))
                {
                    disabledCanvases.Add(canvas);
                }
            }
        }

        private void RestoreGameplayCanvases()
        {
            for (int i = 0; i < disabledCanvases.Count; i++)
            {
                Canvas canvas = disabledCanvases[i];
                if (canvas != null)
                {
                    canvas.enabled = true;
                }
            }

            disabledCanvases.Clear();
        }

        private void PlaySfx(AudioClip clip)
        {
            if (clip == null || GameAudioManager.Instance == null)
            {
                return;
            }

            GameAudioManager.Instance.PlayHitSFX(clip, sfxVolume);
        }
    }
}
