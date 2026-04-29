using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven mapping of PlayerStateId to Audio entries.
/// </summary>
[CreateAssetMenu(fileName = "PlayerAudioData_", menuName = "Game/Character/Player Audio Data")]
public class PlayerAudioData : ScriptableObject
{
    [Serializable]
    public class StateAudioMapping
    {
        public PlayerStateId stateId;
        
        [Header("SFX")]
        public List<AudioClip> sfxVariants = new List<AudioClip>();
        public bool loopSFX = false;
        public float volumeSFX = 1f;
        public float pitchSFX = 1f;

        [Header("Voice")]
        public List<AudioClip> voiceVariants = new List<AudioClip>();
        public bool loopVoice = false;
        public float volumeVoice = 1f;
        public float pitchVoice = 1f;
    }

    public List<StateAudioMapping> mappings = new List<StateAudioMapping>();

    public StateAudioMapping GetMapping(PlayerStateId stateId)
    {
        return mappings.Find(m => m.stateId == stateId);
    }
}
