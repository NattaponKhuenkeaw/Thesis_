using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Pool Settings")]
    public int initialPoolSize = 10;
    
    private Queue<AudioSource> sourcePool = new Queue<AudioSource>();
    private AudioSource voiceSource; // Single instance to mutually exclude lines

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            sourcePool.Enqueue(CreateNewSource("PooledAudioSource"));
        }

        voiceSource = CreateNewSource("VoiceAudioSource");
    }

    private AudioSource CreateNewSource(string namePrefix)
    {
        GameObject go = new GameObject($"{namePrefix}_{System.Guid.NewGuid().ToString().Substring(0, 4)}");
        go.transform.SetParent(transform);
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        return src;
    }

    private AudioSource GetSource()
    {
        if (sourcePool.Count > 0)
        {
            return sourcePool.Dequeue();
        }
        return CreateNewSource("PooledAudioSource");
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
        if (entry.clip == null) return;

        if (entry.channel == AudioChannel.Voice)
        {
            // Override previous voice
            voiceSource.Stop();
            voiceSource.clip = entry.clip;
            voiceSource.volume = entry.volume;
            voiceSource.pitch = entry.pitchVariation; // If we want to vary pitch
            voiceSource.ignoreListenerPause = false;
            voiceSource.Play();
        }
        else
        {
            // Action, Hit, etc.
            AudioSource src = GetSource();
            src.clip = entry.clip;
            src.volume = entry.volume;
            src.pitch = entry.pitchVariation;
            src.ignoreListenerPause = false;
            src.Play();
            StartCoroutine(ReturnSourceRoutine(src, entry.clip.length / Mathf.Max(0.01f, src.pitch)));
        }
    }

    public void PlayHitSFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        AudioSource src = GetSource();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.ignoreListenerPause = false;
        src.Play();
        StartCoroutine(ReturnSourceRoutine(src, clip.length / Mathf.Max(0.01f, src.pitch)));
    }

    public void PlayUltimateActivation(UltimateAction ultimate)
    {
        if (ultimate.activationVoice != null)
        {
            StartCoroutine(PlayUnscaledDelayed(ultimate.activationVoice, ultimate.voiceDelay, true));
        }

        if (ultimate.activationSFX != null)
        {
            // Usually SFX plays immediately
            StartCoroutine(PlayUnscaledDelayed(ultimate.activationSFX, 0f, false));
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
            voiceSource.volume = 1f;
            voiceSource.pitch = 1f;
            voiceSource.ignoreListenerPause = true; // PLAY THROUGH FREEZE
            voiceSource.Play();
            
            // Note: voiceSource doesn't need to be returned to pool
        }
        else
        {
            AudioSource src = GetSource();
            src.clip = clip;
            src.volume = 1f;
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
