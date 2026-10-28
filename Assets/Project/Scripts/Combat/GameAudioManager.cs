using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Project.Core;
using Project.Core.ZoneState;
using UnityEngine.SceneManagement;

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }
    public const float MaxSFXGain = 2f;

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

    [Header("Background Music")]
    [SerializeField] private ZoneStateProvider musicZoneState;
    [SerializeField, Min(0f)] private float musicFadeDuration = 0.65f;

    [Header("Room Ambience")]
    [SerializeField, Min(0f)] private float ambienceFadeDuration = 0.65f;

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
    private AudioSource musicSource;
    private AudioClip requestedMusic;
    private Coroutine musicRoutine;
    private float musicBaseVolume = 1f;
    private float musicFade = 1f;
    private AudioSource ambienceSource;
    private AudioClip requestedAmbience;
    private Coroutine ambienceRoutine;
    private float ambienceBaseVolume = 1f;
    private float requestedAmbienceVolume = 1f;
    private float ambienceFade = 1f;

    public AudioClip CurrentMusic => musicSource != null ? musicSource.clip : null;
    public AudioClip CurrentAmbience => ambienceSource != null ? ambienceSource.clip : null;

    public static GameAudioManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var existing = FindFirstObjectByType<GameAudioManager>();
        return existing != null ? existing : new GameObject("GameAudioManager").AddComponent<GameAudioManager>();
    }

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
        return CalculateSFXGain(baseVolume, sfxVolume, masterVolume, GameSettings.SfxVolume);
    }

    public static float ClampSFXGain(float gain)
    {
        return float.IsNaN(gain) ? 0f : Mathf.Clamp(gain, 0f, MaxSFXGain);
    }

    public static float CalculateSFXGain(float baseGain, float sfx, float master, float settings)
    {
        return ClampSFXGain(baseGain) * sfx * master * settings;
    }

    private static void PlaySFXSource(AudioSource source, AudioClip clip, float gain)
    {
        // AudioSource.volume stops at 1; the one-shot scale preserves authored boosts above 1.
        source.volume = Mathf.Min(1f, gain);
        if (gain > 1f) source.PlayOneShot(clip, gain);
        else source.Play();
    }

    public float GetEffectiveVoiceVolume(float baseVolume = 1f)
    {
        return Mathf.Clamp01(baseVolume) * voiceVolume * masterVolume * GameSettings.SfxVolume;
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
        EnsureMusicSource();
        if (musicZoneState == null)
            musicZoneState = Resources.Load<ZoneStateProvider>("CombatSO/ZoneStateProvider");
        if (musicZoneState != null)
            musicZoneState.OnRoomChanged += HandleMusicRoomChanged;
        SceneManager.sceneLoaded += HandleMusicSceneLoaded;
    }

    private void Start()
    {
        if (Instance == this)
            RefreshRoomMusic();
    }

    private void Update()
    {
        RefreshMusicVolume();
        RefreshAmbienceVolume();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleMusicSceneLoaded;
        if (musicZoneState != null)
            musicZoneState.OnRoomChanged -= HandleMusicRoomChanged;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureMusicSource()
    {
        if (musicSource != null) return;
        var host = new GameObject("MusicAudioSource");
        host.transform.SetParent(transform, false);
        musicSource = host.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.ignoreListenerPause = true;
        musicSource.outputAudioMixerGroup = bgmMixerGroup;
    }

    private void HandleMusicSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshRoomMusic();
    }

    private void RefreshRoomMusic()
    {
        // The menu owns its authored track; it is not a MapSO room.
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            PlayAmbience(null);
            return;
        }
        PlayRoomMusic(musicZoneState != null ? musicZoneState.CurrentRoom : null);
    }

    private void HandleMusicRoomChanged(RoomData room)
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
            PlayRoomMusic(room);
        else
            PlayAmbience(null);
    }

    public void PlayRoomMusic(RoomData room)
    {
        PlayMusic(room != null ? room.BackgroundMusic : null, room != null ? room.MusicVolume : 1f);
        PlayAmbience(room != null ? room.Ambience : null, room != null ? room.AmbienceVolume : 1f);
    }

    public void PlayMusic(AudioClip clip, float volume = 1f)
    {
        EnsureMusicSource();
        musicBaseVolume = Mathf.Clamp01(volume);
        if (requestedMusic == clip && (musicRoutine != null || musicSource.clip == clip))
        {
            RefreshMusicVolume();
            return;
        }

        requestedMusic = clip;
        if (musicRoutine != null)
        {
            StopCoroutine(musicRoutine);
            musicRoutine = null;
        }

        if (!Application.isPlaying || musicFadeDuration <= 0f)
        {
            SetMusicClip(clip);
            musicFade = 1f;
            RefreshMusicVolume();
            return;
        }

        musicRoutine = StartCoroutine(ChangeMusic(clip));
    }

    public void RefreshMusicVolume()
    {
        if (musicSource != null)
            musicSource.volume = musicBaseVolume * bgmVolume * masterVolume * GameSettings.BgmVolume * musicFade;
    }

    private void SetMusicClip(AudioClip clip)
    {
        musicSource.Stop();
        musicSource.clip = clip;
        if (clip != null && Application.isPlaying)
            musicSource.Play();
    }

    private IEnumerator ChangeMusic(AudioClip clip)
    {
        // Returning during fade-out keeps the same playback position instead of restarting the song.
        if (musicSource.clip != clip)
        {
            if (musicSource.clip != null)
                yield return FadeMusicTo(0f);
            SetMusicClip(clip);
            musicFade = 0f;
        }
        if (clip != null)
            yield return FadeMusicTo(1f);
        musicRoutine = null;
    }

    private IEnumerator FadeMusicTo(float target)
    {
        float start = musicFade;
        float elapsed = 0f;
        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            musicFade = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / musicFadeDuration));
            RefreshMusicVolume();
            yield return null;
        }
        musicFade = target;
        RefreshMusicVolume();
    }

    public void PlayAmbience(AudioClip clip, float volume = 1f)
    {
        requestedAmbienceVolume = Mathf.Clamp01(volume);
        if (clip != null && ambienceSource != null && ambienceSource.clip == clip)
            ambienceBaseVolume = requestedAmbienceVolume;
        if (ambienceSource == null)
        {
            if (clip == null) return;
            var host = new GameObject("AmbienceAudioSource");
            host.transform.SetParent(transform, false);
            ambienceSource = host.AddComponent<AudioSource>();
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.ignoreListenerPause = true;
            ambienceSource.outputAudioMixerGroup = sfxMixerGroup;
        }
        if (requestedAmbience == clip && (ambienceRoutine != null || ambienceSource.clip == clip))
        {
            RefreshAmbienceVolume();
            return;
        }

        requestedAmbience = clip;
        if (ambienceRoutine != null)
        {
            StopCoroutine(ambienceRoutine);
            ambienceRoutine = null;
        }
        if (!Application.isPlaying || ambienceFadeDuration <= 0f)
        {
            SetAmbienceClip(clip);
            ambienceFade = 1f;
            RefreshAmbienceVolume();
            return;
        }
        ambienceRoutine = StartCoroutine(ChangeAmbience(clip));
    }

    public void RefreshAmbienceVolume()
    {
        if (ambienceSource != null)
            ambienceSource.volume = GetEffectiveSFXVolume(ambienceBaseVolume) * ambienceFade;
    }

    private void SetAmbienceClip(AudioClip clip)
    {
        ambienceSource.Stop();
        ambienceSource.clip = clip;
        ambienceSource.volume = 0f;
        if (clip != null)
            ambienceBaseVolume = requestedAmbienceVolume;
        if (clip != null && Application.isPlaying)
            ambienceSource.Play();
    }

    private IEnumerator ChangeAmbience(AudioClip clip)
    {
        // Re-entering a room with the same atmosphere resumes the fade without restarting the loop.
        if (ambienceSource.clip != clip)
        {
            if (ambienceSource.clip != null)
                yield return FadeAmbienceTo(0f);
            SetAmbienceClip(clip);
            ambienceFade = 0f;
        }
        if (clip != null)
            yield return FadeAmbienceTo(1f);
        ambienceRoutine = null;
    }

    private IEnumerator FadeAmbienceTo(float target)
    {
        float start = ambienceFade;
        float elapsed = 0f;
        while (elapsed < ambienceFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            ambienceFade = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / ambienceFadeDuration));
            RefreshAmbienceVolume();
            yield return null;
        }
        ambienceFade = target;
        RefreshAmbienceVolume();
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
            src.pitch = entry.pitchVariation;
            src.ignoreListenerPause = false;
            PlaySFXSource(src, entry.clip, GetEffectiveSFXVolume(entry.volume) * concurrencyMod);
            StartCoroutine(ReturnSourceRoutine(src, entry.clip.length / Mathf.Max(0.01f, src.pitch)));
        }
    }

    // The caller owns this source and must stop it when its gameplay phase ends.
    public void PlayControlledSFX(AudioSource source, AudioClip clip, float volume = 1f)
    {
        if (source == null || clip == null) return;
        source.Stop();
        float concurrencyMod = CalculateConcurrencyVolumeModifier(clip);
        if (concurrencyMod <= 0.001f) return;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = false;
        source.outputAudioMixerGroup = sfxMixerGroup;
        source.clip = clip;
        source.pitch = 1f;
        float gain = GetEffectiveSFXVolume(volume) * concurrencyMod;
        source.volume = Mathf.Min(1f, gain);
        if (Application.isPlaying) PlaySFXSource(source, clip, gain);
    }

    public void PlayHitSFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        float concurrencyMod = CalculateConcurrencyVolumeModifier(clip);
        if (concurrencyMod <= 0.001f) return;

        AudioSource src = GetSource();
        src.clip = clip;
        src.pitch = pitch;
        src.ignoreListenerPause = false;
        PlaySFXSource(src, clip, GetEffectiveSFXVolume(volume) * concurrencyMod);
        StartCoroutine(ReturnSourceRoutine(src, clip.length / Mathf.Max(0.01f, src.pitch)));
    }

    public void PlayUltimateActivation(UltimateAction ultimate)
    {
        if (ultimate == null) return;
        PlayUltimateActivation(ultimate.activationVoice, ultimate.activationSFX, ultimate.voiceDelay, ultimate.activationSFXVolume);
    }

    public void PlayUltimateActivation(AudioClip activationVoice, AudioClip activationSFX, float voiceDelay, float activationSFXVolume = 1f)
    {
        if (activationVoice != null)
        {
            StartCoroutine(PlayUnscaledDelayed(activationVoice, voiceDelay, true));
        }

        if (activationSFX != null)
        {
            StartCoroutine(PlayUnscaledDelayed(activationSFX, 0f, false, Mathf.Clamp01(activationSFXVolume)));
        }
    }

    private IEnumerator PlayUnscaledDelayed(AudioClip clip, float delayRealtime, bool isVoice, float volume = 1f)
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
            src.volume = GetEffectiveSFXVolume(Mathf.Clamp01(volume));
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
