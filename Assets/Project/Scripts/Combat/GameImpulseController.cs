using System.Collections;
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
    public SlowMotionSettings slowMotion = new SlowMotionSettings();

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

public class GameImpulseController : MonoBehaviour
{
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

    public void Play(GameImpulseRequest request)
    {
        if (request == null)
        {
            return;
        }

        StartCoroutine(PlayImpulseRoutine(request));
    }

    public void PlayOneShot(float strength)
    {
        EmitImpulse(strength);
    }

    public void PlayImpact(DamageImpactTier tier)
    {
        Play(GetImpactRequest(tier));
    }

    private IEnumerator PlayImpulseRoutine(GameImpulseRequest request)
    {
        float pulseDuration = Mathf.Max(0f, request.duration);
        float pulseInterval = Mathf.Max(0.01f, request.pulseInterval);

        if (pulseDuration <= 0f)
        {
            EmitImpulse(Mathf.Max(request.startStrength, request.endStrength));
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < pulseDuration)
            {
                float normalized = pulseDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / pulseDuration);
                float strength = Mathf.Lerp(request.startStrength, request.endStrength, normalized);
                EmitImpulse(strength);

                yield return new WaitForSecondsRealtime(pulseInterval);
                elapsed += pulseInterval;
            }

            EmitImpulse(request.endStrength);
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

    private void EmitImpulse(float strength)
    {
        if (strength <= 0f)
        {
            return;
        }

        if (CameraManager.instance != null)
        {
            CameraManager.instance.EmitImpulse(strength);
            return;
        }

        if (fallbackImpulseSource == null)
        {
            fallbackImpulseSource = FindFirstObjectByType<CinemachineImpulseSource>();
        }

        if (fallbackImpulseSource != null)
        {
            fallbackImpulseSource.GenerateImpulse(strength);
        }
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
