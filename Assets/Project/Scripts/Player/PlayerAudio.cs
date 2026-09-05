using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Managed component that plays audio based on PlayerStateMachine state changes.
/// Supports SFX and Voice, including looping sounds.
/// </summary>
public class PlayerAudio : MonoBehaviour
{
    private PlayerCharacter player;
    private PlayerAudioData audioData;
    
    private AudioSource loopSFXSource;
    private AudioSource loopVoiceSource;

    private PlayerStateId currentStateId;

    public void Initialize(PlayerCharacter player, PlayerAudioData data)
    {
        // Unsubscribe from previous (if any)
        if (this.player != null && this.player.StateMachine != null)
        {
            this.player.StateMachine.OnStateChanged -= HandleStateChanged;
            this.player.StateMachine.OnJumpLaunched -= HandleJumpLaunched;
        }

        this.player = player;
        this.audioData = data;

        if (data == null)
        {
            //Debug.LogWarning($"[PlayerAudio] Initializing for {player.name} but audioData is NULL! Check if the CharacterObject has an AudioData assigned.");
        }
        else
        {
            //Debug.Log($"[PlayerAudio] Initializing for {player.name} with audioData: {data.name}");
        }

        if (player.StateMachine != null)
        {
            player.StateMachine.OnStateChanged += HandleStateChanged;
            player.StateMachine.OnJumpLaunched += HandleJumpLaunched;
        }

        // Initialize local sources once
        if (loopSFXSource == null) loopSFXSource = CreateLocalSource("LoopSFXSource");
        if (loopVoiceSource == null) loopVoiceSource = CreateLocalSource("LoopVoiceSource");
    }

    private void OnDestroy()
    {
        if (player != null && player.StateMachine != null)
        {
            player.StateMachine.OnStateChanged -= HandleStateChanged;
            player.StateMachine.OnJumpLaunched -= HandleJumpLaunched;
        }
    }

    private AudioSource CreateLocalSource(string namePrefix)
    {
        GameObject go = new GameObject(namePrefix);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        return src;
    }

    private void HandleStateChanged(PlayerStateId prevState, PlayerStateId newState)
    {
        if (audioData == null)
        {
            //Debug.LogWarning($"[PlayerAudio] HandleStateChanged ignored: audioData is NULL for {player.name}");
            return;
        }

        // Skip Airborne state in state change handler because OnJumpLaunched handles
        // playing jump sounds on exact physical launch (including double jumps).
        if (newState == PlayerStateId.Airborne) return;

        PlayAudioForState(newState);
    }

    private void HandleJumpLaunched()
    {
        if (audioData == null) return;
        PlayAudioForState(PlayerStateId.Airborne);
    }

    private void PlayAudioForState(PlayerStateId stateId)
    {
        currentStateId = stateId;
        var mapping = audioData.GetMapping(stateId);
        
        // Stop previous looping sounds if they shouldn't continue
        StopLoopingSounds();

        if (mapping == null) return;

        // Play SFX
        PlayStateSound(mapping.sfxVariants, mapping.loopSFX, mapping.volumeSFX, mapping.pitchSFX, false);

        // Play Voice
        PlayStateSound(mapping.voiceVariants, mapping.loopVoice, mapping.volumeVoice, mapping.pitchVoice, true);
    }

    private void PlayStateSound(List<AudioClip> clips, bool loop, float volume, float pitch, bool isVoice)
    {
        if (clips == null || clips.Count == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Count)];
        if (clip == null) return;

        if (loop)
        {
            AudioSource source = isVoice ? loopVoiceSource : loopSFXSource;
            source.clip = clip;
            source.loop = true;
            source.volume = GameAudioManager.Instance != null
                ? (isVoice ? GameAudioManager.Instance.GetEffectiveVoiceVolume(volume) : GameAudioManager.Instance.GetEffectiveSFXVolume(volume))
                : volume;
            source.pitch = pitch;
            source.spatialBlend = 0f; // Force 2D
            source.Play();
            //Debug.Log($"[PlayerAudio] Playing LOOPING sound: {clip.name} (isVoice: {isVoice}, Volume: {volume}, Pitch: {pitch}) Status: {source.isPlaying}");
        }
        else
        {
            // Use GameAudioManager for one-shots to benefit from pooling
            if (GameAudioManager.Instance != null)
            {
                //Debug.Log($"[PlayerAudio] Playing ONESHOT sound: {clip.name} (isVoice: {isVoice}, Volume: {volume}, Pitch: {pitch})");
                if (isVoice)
                {
                    // Voice channel typically overrides previous voice in GameAudioManager
                    GameAudioManager.Instance.PlayTimelineAudio(new AudioEntry
                    {
                        clip = clip,
                        volume = volume,
                        pitchVariation = pitch,
                        channel = AudioChannel.Voice
                    });
                }
                else
                {
                    GameAudioManager.Instance.PlayHitSFX(clip, volume, pitch);
                }
            }
            else
            {
                //Debug.LogWarning("[PlayerAudio] GameAudioManager.Instance is NULL!");
            }
        }
    }

    private void StopLoopingSounds()
    {
        if (loopSFXSource != null && loopSFXSource.isPlaying)
        {
            loopSFXSource.Stop();
            loopSFXSource.clip = null;
        }
        if (loopVoiceSource != null && loopVoiceSource.isPlaying)
        {
            loopVoiceSource.Stop();
            loopVoiceSource.clip = null;
        }
    }
}
