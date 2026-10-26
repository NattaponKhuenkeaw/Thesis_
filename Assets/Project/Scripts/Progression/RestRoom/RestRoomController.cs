using System.Collections;
using System.Collections.Generic;
using Project.UI;
using UnityEngine;

namespace Project.Progression.RestRoom
{
    /// <summary>
    /// Owns the Rest-room heal presentation. Heals remaining teammates once per visit
    /// by a tunable percent of Max HP (Inspector).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RestRoomController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private RestRoomDisplay display;
        [SerializeField] private RestRoomInteractable interactable;
        [SerializeField] private Transform cameraFocusTarget;
        [SerializeField] private Canvas sequenceCanvas;

        [Header("Heal")]
        [Tooltip("Fraction of Max HP restored to each teammate (additive, clamped). 1 = full, 0.3 ≈ BBEE Rest.")]
        [SerializeField, Range(0f, 1f)] private float healPercentOfMaxHp = 1f;

        [Header("Claim Policy")]
        [Tooltip("If true, a completed heal cannot be claimed again until the room is reloaded.")]
        [SerializeField] private bool oneClaimPerVisit = true;

        [Header("Timing (feel pass later)")]
        [SerializeField, Min(0.05f)] private float cameraBlendSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float cameraReturnBlendSeconds = 0.65f;
        [SerializeField, Min(0.1f)] private float healHoldSeconds = 0.9f;
        [SerializeField, Min(0f)] private float restoreDelaySeconds = 0.45f;

        [Header("Audio (optional)")]
        [SerializeField] private AudioClip confirmClip;
        [SerializeField] private AudioClip healClip;
        [Tooltip("Delay after showing the heal result, within the existing hold time.")]
        [SerializeField, Min(0f)] private float healSfxDelaySeconds = 0.45f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        private readonly List<Canvas> disabledCanvases = new List<Canvas>(16);
        private Coroutine sequenceRoutine;
        private bool ownsInputLock;
        private bool ownsCameraFocus;
        private bool hasClaimedThisVisit;
        private bool hasAppliedHeal;

        public bool IsSequenceActive => sequenceRoutine != null;
        public bool HasClaimedThisVisit => hasClaimedThisVisit;

        private void Awake()
        {
            if (display == null)
            {
                display = GetComponentInChildren<RestRoomDisplay>(true);
            }

            if (interactable == null)
            {
                interactable = GetComponentInChildren<RestRoomInteractable>(true);
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

            sequenceRoutine = StartCoroutine(RunSequence());
            return true;
        }

        private IEnumerator RunSequence()
        {
            hasAppliedHeal = false;
            AcquirePresentationOwnership();

            try
            {
                PlaySfx(confirmClip);
                display?.ShowHealing();

                if (!hasAppliedHeal)
                {
                    float percent = ApplyTeamHeal();
                    hasAppliedHeal = true;
                    if (oneClaimPerVisit)
                    {
                        hasClaimedThisVisit = true;
                    }

                    display?.ShowHealed(percent);
                }
                else
                {
                    display?.ShowHealed(healPercentOfMaxHp);
                }

                float audioDelay = healClip != null
                    ? Mathf.Clamp(healSfxDelaySeconds, 0f, healHoldSeconds)
                    : 0f;
                if (audioDelay > 0f)
                {
                    yield return new WaitForSecondsRealtime(audioDelay);
                }

                PlaySfx(healClip);
                if (healHoldSeconds > audioDelay)
                {
                    yield return new WaitForSecondsRealtime(healHoldSeconds - audioDelay);
                }

                if (restoreDelaySeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(restoreDelaySeconds);
                }

                yield return StartCoroutine(ReturnCameraFocus());
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

        private float ApplyTeamHeal()
        {
            float percent = Mathf.Clamp01(healPercentOfMaxHp);
            TeamLoader teamLoader = TeamLoader.Instance ?? Object.FindFirstObjectByType<TeamLoader>();
            if (teamLoader == null)
            {
                Debug.LogWarning("[RestRoomController] Team heal skipped; TeamLoader missing.");
                return percent;
            }

            teamLoader.HealTeamByMaxHpPercent(percent);
            Debug.Log($"[RestRoomController] Applied team heal ({percent * 100f:0.#}% Max HP).");
            return percent;
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

        private IEnumerator ReturnCameraFocus()
        {
            if (!ownsCameraFocus || CameraManager.instance == null)
            {
                yield break;
            }

            float returnBlend = cameraReturnBlendSeconds;
            CameraManager.instance.EndTemporaryFocus(returnBlend);
            ownsCameraFocus = false;
            if (returnBlend > 0f)
            {
                yield return new WaitForSecondsRealtime(returnBlend);
            }
        }

        private void ReleasePresentationOwnership()
        {
            if (ownsCameraFocus && CameraManager.instance != null)
            {
                CameraManager.instance.EndTemporaryFocus(0f);
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

                if (display != null && canvas.transform.IsChildOf(display.transform))
                {
                    continue;
                }

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
