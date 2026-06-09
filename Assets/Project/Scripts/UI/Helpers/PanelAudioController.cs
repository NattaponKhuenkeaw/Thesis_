using UnityEngine;

namespace Project.UI
{
    /// <summary>
    /// Helper class to handle audio ducking and low-pass filtering when panels are opened/closed.
    /// </summary>
    [System.Serializable]
    public class PanelAudioController
    {
        [Header("Audio Settings")]
        [Tooltip("Factor by which to duck the main audio listener volume (if low pass filter is not used).")]
        [SerializeField] [Range(0f, 1f)] private float panelAudioDuckVolume = 0.35f;
        
        [Tooltip("Optional low pass filter to apply when the panel is active.")]
        [SerializeField] private AudioLowPassFilter panelLowPassFilter;
        
        [Tooltip("Cutoff frequency for the low pass filter.")]
        [SerializeField] [Range(10f, 22000f)] private float panelLowPassCutoff = 350f;

        private float previousAudioListenerVolume = 1f;
        private float previousLowPassCutoff = 22000f;
        private bool previousLowPassEnabled;

        public PanelAudioController() { }

        public PanelAudioController(float duckVolume, AudioLowPassFilter lowPassFilter, float cutoff)
        {
            this.panelAudioDuckVolume = duckVolume;
            this.panelLowPassFilter = lowPassFilter;
            this.panelLowPassCutoff = cutoff;
        }

        public void ApplyPauseAudioState()
        {
            previousAudioListenerVolume = AudioListener.volume;

            if (panelLowPassFilter != null)
            {
                previousLowPassEnabled = panelLowPassFilter.enabled;
                previousLowPassCutoff = panelLowPassFilter.cutoffFrequency;
                panelLowPassFilter.enabled = true;
                panelLowPassFilter.cutoffFrequency = panelLowPassCutoff;
            }
            else
            {
                AudioListener.volume = Mathf.Clamp01(previousAudioListenerVolume * panelAudioDuckVolume);
            }
        }

        public void RestorePauseAudioState()
        {
            if (panelLowPassFilter != null)
            {
                panelLowPassFilter.cutoffFrequency = previousLowPassCutoff;
                panelLowPassFilter.enabled = previousLowPassEnabled;
            }

            AudioListener.volume = previousAudioListenerVolume;
        }
    }
}
