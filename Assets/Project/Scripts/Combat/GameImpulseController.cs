using System.Collections;
using System.Collections.Generic;
using CombatCore;
using Unity.Cinemachine;
using UnityEngine;

[System.Serializable]
public class SlowMotionSettings
{
    public bool enabled;

    [Range(0.01f, 1f)]
    public float timeScale = 0.35f;

    public float duration = 0.08f;
    public float delaySeconds;

    public float GetTotalDurationSeconds()
    {
        if (!enabled)
        {
            return 0f;
        }

        return Mathf.Max(0f, delaySeconds) + Mathf.Max(0f, duration);
    }
}

[System.Serializable]
public class GameImpulseRequest
{
    public float startStrength = 0.4f;
    public float endStrength = 0.1f;
    public float duration = 0.16f;
    public float pulseInterval = 0.03f;
    [Tooltip("Scale pulse timing and the shake waveform with nonzero gameplay slow motion.")]
    public bool followSlowMotion;
    [Tooltip("Freeze pulse timing and the shake waveform during hitstop/full freeze. Explicit pause always holds playback.")]
    public bool followHitstop;
    public SlowMotionSettings slowMotion = new SlowMotionSettings();

    public static float PlaybackDelta(bool followSlowMotion, bool followHitstop,
        float scaledDelta, float unscaledDelta, float timeScale, bool paused)
    {
        if (paused || (followHitstop && timeScale <= 0f)) return 0f;
        return Mathf.Max(0f, followSlowMotion && timeScale > 0f ? scaledDelta : unscaledDelta);
    }

    public float GetTotalDurationSeconds()
    {
        return Mathf.Max(0f, duration) + (slowMotion != null ? slowMotion.GetTotalDurationSeconds() : 0f);
    }

    public static GameImpulseRequest CreateImpact(float startStrength, float endStrength, float duration, float pulseInterval)
    {
        return new GameImpulseRequest
        {
            startStrength = startStrength,
            endStrength = endStrength,
            duration = duration,
            pulseInterval = pulseInterval,
            slowMotion = new SlowMotionSettings()
        };
    }

    public static GameImpulseRequest CreateActionEventDefault()
    {
        return new GameImpulseRequest
        {
            startStrength = 0.4f,
            endStrength = 0.08f,
            duration = 0.14f,
            pulseInterval = 0.025f,
            slowMotion = new SlowMotionSettings
            {
                enabled = false,
                timeScale = 0.35f,
                duration = 0.08f,
                delaySeconds = 0f
            }
        };
    }
}

[DefaultExecutionOrder(-100)]
public class GameImpulseController : MonoBehaviour
{
    private struct ClockedImpulse
    {
        public CinemachineImpulseManager.ImpulseEvent impulse;
        public ISignalSource6D signal;
        public bool followSlowMotion;
        public bool followHitstop;
        public float elapsed;
    }

    private readonly List<ClockedImpulse> _activeImpulses = new List<ClockedImpulse>();
    private CinemachineBrain _impulseBrain;
    private CinemachineBrain.UpdateMethods _previousCameraUpdate;
    private CinemachineBrain.BrainUpdateMethods _previousBlendUpdate;
    private bool _ownsCameraUpdate;

    [System.Serializable]
    private class ImpactImpulseProfile
    {
        public DamageImpactTier tier = DamageImpactTier.Low;
        public GameImpulseRequest request = GameImpulseRequest.CreateImpact(0.2f, 0.05f, 0.1f, 0.025f);
    }

    private static GameImpulseController instance;

    public static GameImpulseController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GameImpulseController>();
                if (instance == null)
                {
                    GameObject go = new GameObject(nameof(GameImpulseController));
                    instance = go.AddComponent<GameImpulseController>();
                }
            }

            return instance;
        }
    }

    [Header("Fallback")]
    [SerializeField] private CinemachineImpulseSource fallbackImpulseSource;
    [SerializeField, Range(0f, 1f)] private float outputStrengthMultiplier = 0.2f;

    [Header("Impact Profiles")]
    [SerializeField] private ImpactImpulseProfile lowImpact = new ImpactImpulseProfile
    {
        tier = DamageImpactTier.Low,
        request = new GameImpulseRequest
        {
            startStrength = 0.15f,
            endStrength = 0.03f,
            duration = 0.08f,
            pulseInterval = 0.025f,
            slowMotion = new SlowMotionSettings()
        }
    };

    [SerializeField] private ImpactImpulseProfile mediumImpact = new ImpactImpulseProfile
    {
        tier = DamageImpactTier.Medium,
        request = new GameImpulseRequest
        {
            startStrength = 0.4f,
            endStrength = 0.08f,
            duration = 0.12f,
            pulseInterval = 0.025f,
            slowMotion = new SlowMotionSettings()
        }
    };

    [SerializeField] private ImpactImpulseProfile heavyImpact = new ImpactImpulseProfile
    {
        tier = DamageImpactTier.Heavy,
        request = new GameImpulseRequest
        {
            startStrength = 0.65f,
            endStrength = 0.12f,
            duration = 0.16f,
            pulseInterval = 0.02f,
            slowMotion = new SlowMotionSettings()
        }
    };

    [SerializeField] private ImpactImpulseProfile ultimateImpact = new ImpactImpulseProfile
    {
        tier = DamageImpactTier.Ultimate,
        request = new GameImpulseRequest
        {
            startStrength = 1f,
            endStrength = 0.2f,
            duration = 0.22f,
            pulseInterval = 0.02f,
            slowMotion = new SlowMotionSettings()
        }
    };

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (fallbackImpulseSource == null)
        {
            fallbackImpulseSource = FindFirstObjectByType<CinemachineImpulseSource>();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        AdvanceImpulses(Time.deltaTime, Time.unscaledDeltaTime, Time.timeScale,
            GameFreezeManager.Instance != null && GameFreezeManager.Instance.IsPauseHeld);
    }

    public void AdvanceImpulses(float scaledDelta, float unscaledDelta, float timeScale, bool paused)
    {
        for (int i = _activeImpulses.Count - 1; i >= 0; i--)
        {
            ClockedImpulse active = _activeImpulses[i];
            // Cinemachine pools expired events; a retained handle must never retime a reused signal.
            if (!ReferenceEquals(active.impulse.SignalSource, active.signal))
            {
                _activeImpulses.RemoveAt(i);
                continue;
            }
            active.elapsed += GameImpulseRequest.PlaybackDelta(active.followSlowMotion, active.followHitstop,
                scaledDelta, unscaledDelta, timeScale, paused);
            active.impulse.StartTime = CinemachineImpulseManager.Instance.CurrentTime - active.elapsed;
            if (active.impulse.Expired) _activeImpulses.RemoveAt(i);
            else _activeImpulses[i] = active;
        }
        UpdateCameraClock(timeScale, paused);
    }

    private void UpdateCameraClock(float timeScale, bool paused)
    {
        bool needsRenderUpdate = false;
        if (timeScale <= 0f && !paused)
            for (int i = 0; i < _activeImpulses.Count; i++)
                if (!_activeImpulses[i].followHitstop) { needsRenderUpdate = true; break; }

        if (!needsRenderUpdate) { RestoreCameraClock(); return; }
        if (_impulseBrain == null || _ownsCameraUpdate
            || _impulseBrain.UpdateMethod == CinemachineBrain.UpdateMethods.ManualUpdate) return;
        _previousCameraUpdate = _impulseBrain.UpdateMethod;
        _previousBlendUpdate = _impulseBrain.BlendUpdateMethod;
        // Smart/Fixed cameras can otherwise stop sampling noise when physics stops in hitstop.
        // Delta time remains zero, so base follow damping and camera blends do not advance.
        _impulseBrain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
        _impulseBrain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
        _ownsCameraUpdate = true;
    }

    private void RestoreCameraClock()
    {
        if (_ownsCameraUpdate && _impulseBrain != null)
        {
            if (_impulseBrain.UpdateMethod == CinemachineBrain.UpdateMethods.LateUpdate)
                _impulseBrain.UpdateMethod = _previousCameraUpdate;
            if (_impulseBrain.BlendUpdateMethod == CinemachineBrain.BrainUpdateMethods.LateUpdate)
                _impulseBrain.BlendUpdateMethod = _previousBlendUpdate;
        }
        _ownsCameraUpdate = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        for (int i = 0; i < _activeImpulses.Count; i++)
        {
            ClockedImpulse active = _activeImpulses[i];
            if (!ReferenceEquals(active.impulse.SignalSource, active.signal)) continue;
            active.impulse.Cancel(CinemachineImpulseManager.Instance.CurrentTime, true);
            active.impulse.Envelope.SustainTime = Mathf.Max(float.Epsilon, active.impulse.Envelope.SustainTime);
            active.impulse.SignalSource = null;
        }
        _activeImpulses.Clear();
        RestoreCameraClock();
    }

    public void Play(GameImpulseRequest request)
    {
        if (request == null || !isActiveAndEnabled)
        {
            return;
        }

        StartCoroutine(PlayImpulseRoutine(request));
    }

    public void PlayOneShot(float strength)
    {
        if (!isActiveAndEnabled) return;
        EmitImpulse(strength * outputStrengthMultiplier, false, false);
    }

    public void PlayImpact(DamageImpactTier tier)
    {
        Play(GetImpactRequest(tier));
    }

    private IEnumerator PlayImpulseRoutine(GameImpulseRequest request)
    {
        float pulseDuration = Mathf.Max(0f, request.duration);
        float pulseInterval = Mathf.Max(0.01f, request.pulseInterval);
        float strengthMultiplier = Mathf.Max(0f, outputStrengthMultiplier);
        bool followSlowMotion = request.followSlowMotion;
        bool followHitstop = request.followHitstop;

        if (pulseDuration <= 0f)
        {
            EmitImpulse(Mathf.Max(request.startStrength, request.endStrength) * strengthMultiplier, followSlowMotion, followHitstop);
        }
        else
        {
            float elapsed = 0f;
            float nextPulse = 0f;
            while (elapsed < pulseDuration)
            {
                if (elapsed >= nextPulse)
                {
                    float strength = Mathf.Lerp(request.startStrength, request.endStrength, elapsed / pulseDuration) * strengthMultiplier;
                    EmitImpulse(strength, followSlowMotion, followHitstop);
                    nextPulse = elapsed + pulseInterval;
                }
                yield return null;
                elapsed += GameImpulseRequest.PlaybackDelta(followSlowMotion, followHitstop,
                    Time.deltaTime, Time.unscaledDeltaTime, Time.timeScale,
                    GameFreezeManager.Instance != null && GameFreezeManager.Instance.IsPauseHeld);
            }

            EmitImpulse(request.endStrength * strengthMultiplier, followSlowMotion, followHitstop);
        }

        if (request.slowMotion != null
            && request.slowMotion.enabled
            && GameFreezeManager.Instance != null)
        {
            GameFreezeManager.Instance.PlaySlowMotion(
                request.slowMotion.timeScale,
                request.slowMotion.duration,
                request.slowMotion.delaySeconds);
        }
    }

    private CinemachineImpulseManager.ImpulseEvent EmitImpulse(float strength, bool followSlowMotion, bool followHitstop)
    {
        if (strength <= 0f)
        {
            return null;
        }

        CinemachineImpulseManager.ImpulseEvent impulse = null;
        if (CameraManager.instance != null)
        {
            impulse = CameraManager.instance.CreateImpulse(strength);
        }
        else
        {
            if (fallbackImpulseSource == null)
                fallbackImpulseSource = FindFirstObjectByType<CinemachineImpulseSource>();
            if (fallbackImpulseSource != null)
                impulse = fallbackImpulseSource.ImpulseDefinition.CreateAndReturnEvent(
                    fallbackImpulseSource.transform.position, fallbackImpulseSource.DefaultVelocity * strength);
        }

        if (impulse != null)
        {
            _activeImpulses.Add(new ClockedImpulse { impulse = impulse, signal = impulse.SignalSource,
                followSlowMotion = followSlowMotion, followHitstop = followHitstop });
            if (_impulseBrain == null) _impulseBrain = FindFirstObjectByType<CinemachineBrain>();
            UpdateCameraClock(Time.timeScale, GameFreezeManager.Instance != null && GameFreezeManager.Instance.IsPauseHeld);
        }
        return impulse;
    }

    private GameImpulseRequest GetImpactRequest(DamageImpactTier tier)
    {
        return tier switch
        {
            DamageImpactTier.Medium => mediumImpact.request,
            DamageImpactTier.Heavy => heavyImpact.request,
            DamageImpactTier.Ultimate => ultimateImpact.request,
            _ => lowImpact.request
        };
    }
}
