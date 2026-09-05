using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Project.Core;

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Pool Settings")]
    public int initialPoolSize = 10;

    [Header("Volume Hierarchy")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1.0f;
    [Tooltip("Master SFX volume. Default is 0.5 (-6 dB) to give headroom and prevent clipping during multi-hits.")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.6f;

    [Header("Mixer Routing (Optional)")]
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup sfxMixerGroup;
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup voiceMixerGroup;
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup bgmMixerGroup;

    [Header("Concurrency & Polyphony Limiter")]
    [Tooltip("Max concurrent instances of the exact same AudioClip allowed in a single frame.")]
    [SerializeField] private int maxSameClipPerFrame = 2;
    [Tooltip("Volume multiplier applied to subsequent instances of the same sound played in the same frame.")]
    [SerializeField, Range(0f, 1f)] private float duplicateClipDampenFactor = 0.6f;

    private const string PrefKeyMaster = "Audio_MasterVolume";
    private const string PrefKeySFX = "Audio_SFXVolume";
    private const string PrefKeyVoice = "Audio_VoiceVolume";
    private const string PrefKeyBGM = "Audio_BGMVolume";

    private readonly Dictionary<AudioClip, int> frameClipCounts = new Dictionary<AudioClip, int>();
    private int lastProcessedFrame = -1;
    
    private Queue<AudioSource> sourcePool = new Queue<AudioSource>();
    private AudioSource voiceSource; // Single instance to mutually exclude lines

    public float MasterVolume
    {
        get => masterVolume;
        set
        {
            masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefKeyMaster, masterVolume);
        }
    }

    public float SFXVolume
    {
        get => sfxVolume;
        set
        {
            sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefKeySFX, sfxVolume);
        }
    }

    public float VoiceVolume
    {
        get => voiceVolume;
        set
        {
            voiceVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefKeyVoice, voiceVolume);
        }
    }

    public float BGMVolume
    {
        get => bgmVolume;
        set
        {
            bgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefKeyBGM, bgmVolume);
        }
    }

    public float GetEffectiveSFXVolume(float baseVolume = 1f)
    {
        return Mathf.Clamp01(baseVolume) * sfxVolume * masterVolume;
    }

    public float GetEffectiveVoiceVolume(float baseVolume = 1f)
    {
        return Mathf.Clamp01(baseVolume) * voiceVolume * masterVolume;
    }

    private float CalculateConcurrencyVolumeModifier(AudioClip clip)
    {
        if (clip == null) return 1f;

        int currentFrame = Time.frameCount;
        if (currentFrame != lastProcessedFrame)
        {
            frameClipCounts.Clear();
            lastProcessedFrame = currentFrame;
        }

        if (!frameClipCounts.TryGetValue(clip, out int count))
        {
            count = 0;
        }

        if (count >= maxSameClipPerFrame)
        {
            return 0f; // Drop duplicate sound exceeding frame limit to prevent ear-damaging volume spikes
        }

        frameClipCounts[clip] = count + 1;
        return count == 0 ? 1f : Mathf.Pow(duplicateClipDampenFactor, count);
    }

    private void LoadSavedVolumes()
    {
        if (PlayerPrefs.HasKey(PrefKeyMaster)) masterVolume = PlayerPrefs.GetFloat(PrefKeyMaster);
        if (PlayerPrefs.HasKey(PrefKeySFX)) sfxVolume = PlayerPrefs.GetFloat(PrefKeySFX);
        if (PlayerPrefs.HasKey(PrefKeyVoice)) voiceVolume = PlayerPrefs.GetFloat(PrefKeyVoice);
        if (PlayerPrefs.HasKey(PrefKeyBGM)) bgmVolume = PlayerPrefs.GetFloat(PrefKeyBGM);
    }

    private void Awake()
    {
        LoadSavedVolumes();
        string sourceSceneName = gameObject.scene.name;
        RootPersistentManager rootManager = GetComponentInParent<RootPersistentManager>();
        if (rootManager != null && rootManager.IsDuplicate)
        {
            enabled = false;
            return;
        }

        if (Instance != null && Instance != this)
        {
            Debug.Log($"[GameAudioManager] Duplicate audio manager '{gameObject.name}' loaded from scene '{sourceSceneName}'. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (rootManager == null)
        {
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[GameAudioManager] Standalone audio manager initialized from scene '{sourceSceneName}'.");
        }
        else
        {
            Debug.Log($"[GameAudioManager] Audio manager registered under persistent root '{rootManager.gameObject.name}'.");
        }

        InitializePool();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            sourcePool.Enqueue(CreateNewSource("PooledAudioSource", false));
        }

        voiceSource = CreateNewSource("VoiceAudioSource", true);
    }

    private AudioSource CreateNewSource(string namePrefix, bool isVoice = false)
    {
        GameObject go = new GameObject($"{namePrefix}_{System.Guid.NewGuid().ToString().Substring(0, 4)}");
        go.transform.SetParent(transform);
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        if (isVoice && voiceMixerGroup != null)
        {
            src.outputAudioMixerGroup = voiceMixerGroup;
        }
        else if (!isVoice && sfxMixerGroup != null)
        {
            src.outputAudioMixerGroup = sfxMixerGroup;
        }
        return src;
    }

    private AudioSource GetSource()
    {
        if (sourcePool.Count > 0)
        {
            return sourcePool.Dequeue();
        }
        return CreateNewSource("PooledAudioSource", false);
    }

    private void ReturnSource(AudioSource src)
    {
        src.Stop();
        src.clip = null;
        src.ignoreListenerPause = false; // Reset to default
        sourcePool.Enqueue(src);
    }

    public void PlayTimelineAudio(AudioEntry entry)
    {
        if (entry.clip == null)
        {
            Debug.LogWarning($"GameAudioManager: AudioEntry '{entry.label}' has no AudioClip assigned!", this);
            return;
        }

        if (entry.channel == AudioChannel.Voice)
        {
            // Override previous voice
            voiceSource.Stop();
            voiceSource.clip = entry.clip;
            voiceSource.volume = GetEffectiveVoiceVolume(entry.volume);
            voiceSource.pitch = entry.pitchVariation; // If we want to vary pitch
            voiceSource.ignoreListenerPause = false;
            voiceSource.Play();
        }
        else
        {
            float concurrencyMod = CalculateConcurrencyVolumeModifier(entry.clip);
            if (concurrencyMod <= 0.001f) return;

            // Action, Hit, etc.
            AudioSource src = GetSource();
            src.clip = entry.clip;
            src.volume = GetEffectiveSFXVolume(entry.volume) * concurrencyMod;
            src.pitch = entry.pitchVariation;
            src.ignoreListenerPause = false;
            src.Play();
            StartCoroutine(ReturnSourceRoutine(src, entry.clip.length / Mathf.Max(0.01f, src.pitch)));
        }
    }

    public void PlayHitSFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        float concurrencyMod = CalculateConcurrencyVolumeModifier(clip);
        if (concurrencyMod <= 0.001f) return;

        AudioSource src = GetSource();
        src.clip = clip;
        src.volume = GetEffectiveSFXVolume(volume) * concurrencyMod;
        src.pitch = pitch;
        src.ignoreListenerPause = false;
        src.Play();
        StartCoroutine(ReturnSourceRoutine(src, clip.length / Mathf.Max(0.01f, src.pitch)));
    }

    public void PlayUltimateActivation(UltimateAction ultimate)
    {
        if (ultimate == null) return;
        PlayUltimateActivation(ultimate.activationVoice, ultimate.activationSFX, ultimate.voiceDelay);
    }

    public void PlayUltimateActivation(AudioClip activationVoice, AudioClip activationSFX, float voiceDelay)
    {
        if (activationVoice != null)
        {
            StartCoroutine(PlayUnscaledDelayed(activationVoice, voiceDelay, true));
        }

        if (activationSFX != null)
        {
            StartCoroutine(PlayUnscaledDelayed(activationSFX, 0f, false));
        }
    }

    private IEnumerator PlayUnscaledDelayed(AudioClip clip, float delayRealtime, bool isVoice)
    {
        if (delayRealtime > 0f)
        {
            yield return new WaitForSecondsRealtime(delayRealtime);
        }

        if (isVoice)
        {
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.volume = GetEffectiveVoiceVolume(1f);
            voiceSource.pitch = 1f;
            voiceSource.ignoreListenerPause = true; // PLAY THROUGH FREEZE
            voiceSource.Play();
            
            // Note: voiceSource doesn't need to be returned to pool
        }
        else
        {
            AudioSource src = GetSource();
            src.clip = clip;
            src.volume = GetEffectiveSFXVolume(1f);
            src.pitch = 1f;
            src.ignoreListenerPause = true; // PLAY THROUGH FREEZE
            src.Play();

            // Can't use normal ReturnSourceRoutine since time scale is 0. 
            // Return after unscaled realtime.
            StartCoroutine(ReturnSourceUnscaledRoutine(src, clip.length));
        }
    }

    private IEnumerator ReturnSourceRoutine(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnSource(src);
    }

    private IEnumerator ReturnSourceUnscaledRoutine(AudioSource src, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        ReturnSource(src);
    }
}
