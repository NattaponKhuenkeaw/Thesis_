using System;
using System.Collections;
using CombatCore;
using UnityEngine;
using Project.Core;

/// <summary>
/// Centralized manager for game-wide freezes (hitstop/hitfreeze).
/// Uses Time.timeScale to ensure all systems (physics, coroutines, UI, etc.) 
/// are synchronized during a freeze.
/// </summary>
public class GameFreezeManager : MonoBehaviour
{
    public readonly struct FreezeToken
    {
        public FreezeToken(int id)
        {
            Id = id;
        }

        public int Id { get; }
        public bool IsValid => Id != 0;
    }

    public static GameFreezeManager Instance { get; private set; }
    private static int fallbackPauseHoldCount;
    private static float fallbackResumeScale;

    private Coroutine freezeCoroutine;
    private Coroutine slowMotionCoroutine;
    private int currentFreezeFramesRemaining;
    private int nextFreezeTokenId = 1;
    private int activeScopedFreezeTokenId;
    private int pauseHoldCount;
    private float defaultFixedDeltaTime;
    private float activeSlowMotionScale = 1f;

    public bool IsTimeFrozen => Time.timeScale <= 0f;
    public bool HasActiveScopedFreeze => activeScopedFreezeTokenId != 0;
    public bool IsPauseHeld => pauseHoldCount > 0 || fallbackPauseHoldCount > 0;
    public static bool IsGameplayPaused => fallbackPauseHoldCount > 0 || (Instance != null && Instance.IsPauseHeld);
    public static float GameplayUnscaledDeltaTime => IsGameplayPaused ? 0f : Time.unscaledDeltaTime;

    public static Action AcquirePauseHold()
    {
        GameFreezeManager manager = Instance;
        if (manager != null)
        {
            manager.BeginPauseHold();
            return () => { if (manager != null) manager.EndPauseHold(); };
        }

        if (fallbackPauseHoldCount++ == 0) fallbackResumeScale = Time.timeScale;
        Time.timeScale = 0f;
        return ReleaseFallbackPauseHold;
    }

    private static void ReleaseFallbackPauseHold()
    {
        if (--fallbackPauseHoldCount != 0) return;
        if (Instance != null) Instance.ApplyTimeScale();
        else Time.timeScale = fallbackResumeScale;
    }

    public static IEnumerator WaitForGameplaySeconds(float seconds)
    {
        do
        {
            yield return null;
            seconds -= GameplayUnscaledDeltaTime;
        } while (seconds > 0f || IsGameplayPaused);
    }

    private void Awake()
    {
        string sourceSceneName = gameObject.scene.name;
        RootPersistentManager rootManager = GetComponentInParent<RootPersistentManager>();
        if (rootManager != null && rootManager.IsDuplicate)
        {
            enabled = false;
            return;
        }

        if (Instance != null && Instance != this)
        {
            Debug.Log($"[GameFreezeManager] Duplicate freeze manager '{gameObject.name}' loaded from scene '{sourceSceneName}'. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        defaultFixedDeltaTime = Time.fixedDeltaTime;

        if (rootManager == null)
        {
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[GameFreezeManager] Standalone freeze manager initialized from scene '{sourceSceneName}'.");
        }
        else
        {
            Debug.Log($"[GameFreezeManager] Freeze manager registered under persistent root '{rootManager.gameObject.name}'.");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // Ensure timeScale is restored if the manager is destroyed
            Time.timeScale = fallbackPauseHoldCount > 0 ? 0f : 1f;
            Time.fixedDeltaTime = defaultFixedDeltaTime > 0f ? defaultFixedDeltaTime : 0.02f;
            Instance = null;
        }
    }

    /// <summary>
    /// Freezes the game for a specified number of frames.
    /// Frames are translated to real-time seconds based on LOGIC_FPS.
    /// </summary>
    public void FreezeFrame(int frames)
    {
        if (frames <= 0) return;
        if (HasActiveScopedFreeze) return;

        StopSlowMotion();

        // If a new freeze is requested, we take the maximum duration
        if (frames > currentFreezeFramesRemaining)
        {
            currentFreezeFramesRemaining = frames;
            
            if (freezeCoroutine != null)
            {
                StopCoroutine(freezeCoroutine);
            }
            
            float durationSeconds = frames / FrameDataConstants.LOGIC_FPS;
            freezeCoroutine = StartCoroutine(FreezeRoutine(durationSeconds));
        }
    }

    public FreezeToken BeginScopedFreeze()
    {
        currentFreezeFramesRemaining = 0;
        StopSlowMotion();

        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
            freezeCoroutine = null;
        }

        activeScopedFreezeTokenId = nextFreezeTokenId++;
        Time.timeScale = 0f;
        return new FreezeToken(activeScopedFreezeTokenId);
    }

    public bool EndScopedFreeze(FreezeToken token)
    {
        if (!OwnsScopedFreeze(token))
        {
            return false;
        }

        RestoreTime();
        return true;
    }

    public bool OwnsScopedFreeze(FreezeToken token)
    {
        return token.IsValid && activeScopedFreezeTokenId == token.Id;
    }

    /// <summary>
    /// Special freeze for Ultimate activations with a fixed duration.
    /// Typically longer and might trigger a callback when finished.
    /// </summary>
    public void UltimateFreeze(int frames, Action onFreezeEnd = null)
    {
        if (HasActiveScopedFreeze)
        {
            return;
        }

        StopSlowMotion();

        currentFreezeFramesRemaining = frames;

        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
        }

        float durationSeconds = frames / FrameDataConstants.LOGIC_FPS;
        freezeCoroutine = StartCoroutine(FreezeRoutine(durationSeconds, onFreezeEnd));
    }

    private IEnumerator FreezeRoutine(float seconds, Action onComplete = null)
    {
        Time.timeScale = 0f;

        yield return WaitForGameplaySeconds(seconds);

        currentFreezeFramesRemaining = 0;
        freezeCoroutine = null;
        ApplyTimeScale();

        onComplete?.Invoke();
    }

    public void BeginPauseHold()
    {
        pauseHoldCount++;
        ApplyTimeScale();
    }

    public void EndPauseHold()
    {
        if (pauseHoldCount == 0) return;
        pauseHoldCount--;
        ApplyTimeScale();
    }

    public void PlaySlowMotion(float timeScale, float duration, float delaySeconds = 0f)
    {
        if (duration <= 0f || timeScale <= 0f || timeScale >= 1f)
        {
            return;
        }

        if (delaySeconds <= 0f && !IsPauseHeld && IsTimeFrozen) return;

        if (slowMotionCoroutine != null)
        {
            StopCoroutine(slowMotionCoroutine);
        }

        slowMotionCoroutine = StartCoroutine(SlowMotionRoutine(
            Mathf.Clamp(timeScale, 0.01f, 0.99f),
            duration,
            Mathf.Max(0f, delaySeconds)));
    }

    private IEnumerator SlowMotionRoutine(float timeScale, float duration, float delaySeconds)
    {
        if (delaySeconds > 0f)
        {
            yield return WaitForGameplaySeconds(delaySeconds);
        }
        else if (IsPauseHeld)
        {
            yield return WaitForGameplaySeconds(0f);
        }

        if (HasActiveScopedFreeze || IsTimeFrozen)
        {
            slowMotionCoroutine = null;
            yield break;
        }

        activeSlowMotionScale = timeScale;
        ApplyTimeScale();

        yield return WaitForGameplaySeconds(duration);

        slowMotionCoroutine = null;
        activeSlowMotionScale = 1f;
        ApplyTimeScale();
    }

    /// <summary>
    /// Forcefully restores time scale. Use with caution.
    /// </summary>
    public void RestoreTime()
    {
        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
            freezeCoroutine = null;
        }

        StopSlowMotion();
        currentFreezeFramesRemaining = 0;
        activeScopedFreezeTokenId = 0;
        ApplyTimeScale();
    }

    private void ApplyTimeScale()
    {
        if (IsPauseHeld || HasActiveScopedFreeze || freezeCoroutine != null)
        {
            Time.timeScale = 0f;
            return;
        }

        Time.timeScale = activeSlowMotionScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * activeSlowMotionScale;
    }

    private void StopSlowMotion()
    {
        if (slowMotionCoroutine != null)
        {
            StopCoroutine(slowMotionCoroutine);
            slowMotionCoroutine = null;
        }

        activeSlowMotionScale = 1f;
    }
}
