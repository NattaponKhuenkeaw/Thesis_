using System;
using System.Collections;
using CombatCore;
using UnityEngine;

/// <summary>
/// Centralized manager for game-wide freezes (hitstop/hitfreeze).
/// Uses Time.timeScale to ensure all systems (physics, coroutines, UI, etc.) 
/// are synchronized during a freeze.
/// </summary>
public class GameFreezeManager : MonoBehaviour
{
    public static GameFreezeManager Instance { get; private set; }

    private Coroutine freezeCoroutine;
    private int currentFreezeFramesRemaining;

    public bool IsTimeFrozen => Time.timeScale <= 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // Ensure timeScale is restored if the manager is destroyed
            Time.timeScale = 1f;
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

    /// <summary>
    /// Special freeze for Ultimate activations.
    /// Freezes indefinitely until RestoreTime() is called.
    /// Useful for synchronizing with external sequences like the Flash Overlay.
    /// </summary>
    public void UltimateFreezeManual()
    {
        currentFreezeFramesRemaining = 0; // Disable frame-based restoration
        
        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
            freezeCoroutine = null;
        }
        
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Special freeze for Ultimate activations with a fixed duration.
    /// Typically longer and might trigger a callback when finished.
    /// </summary>
    public void UltimateFreeze(int frames, Action onFreezeEnd = null)
    {
        // For Ultimate, we always override the current freeze
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

        // Use WaitForSecondsRealtime because Time.timeScale is 0
        yield return new WaitForSecondsRealtime(seconds);

        Time.timeScale = 1f;
        currentFreezeFramesRemaining = 0;
        freezeCoroutine = null;
        
        onComplete?.Invoke();
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
        Time.timeScale = 1f;
        currentFreezeFramesRemaining = 0;
    }
}
