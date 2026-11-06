using System.Collections.Generic;
using CombatCore;
using UnityEngine;

public partial class GameAudioManager
{
    private sealed class TimelineAudioPlayback
    {
        public AudioSource source;
        public AudioClip clip;
        public float basePitch;
        public bool slowMotionAware;
        public bool voice;
        public bool pausedByClock;
    }

    private readonly List<TimelineAudioPlayback> timelineAudio = new List<TimelineAudioPlayback>();
    public int ActiveTimelineAudioCount => timelineAudio.Count;

    private void TrackTimelineAudio(AudioSource source, AudioEntry entry, bool voice)
    {
        TrackActionAudio(source, entry.pitchVariation, entry.slowMotionAware, voice);
    }

    private void TrackActionAudio(AudioSource source, float basePitch, bool slowMotionAware, bool voice)
    {
        var playback = new TimelineAudioPlayback { source = source, clip = source.clip, basePitch = basePitch,
            slowMotionAware = slowMotionAware, voice = voice };
        if (ShouldPauseTimelineAudio(playback))
        {
            source.Pause();
            playback.pausedByClock = true;
        }
        timelineAudio.Add(playback);
    }

    private static bool ShouldPauseTimelineAudio(TimelineAudioPlayback playback) => GameFreezeManager.IsGameplayPaused
        || (playback.slowMotionAware && Time.timeScale <= 0f);

    private void ForgetTimelineAudio(AudioSource source)
    {
        for (int i = timelineAudio.Count - 1; i >= 0; i--)
            if (timelineAudio[i].source == source) timelineAudio.RemoveAt(i);
    }

    private void UpdateTimelineAudio()
    {
        for (int i = timelineAudio.Count - 1; i >= 0; i--)
        {
            var playback = timelineAudio[i];
            AudioSource source = playback.source;
            if (source == null || source.clip != playback.clip)
            {
                timelineAudio.RemoveAt(i);
                continue;
            }
            source.pitch = AnimationTimePolicy.AudioPitch(playback.basePitch, Time.timeScale, playback.slowMotionAware);
            bool frozen = ShouldPauseTimelineAudio(playback);
            if (frozen && !playback.pausedByClock) { source.Pause(); playback.pausedByClock = true; }
            else if (!frozen && playback.pausedByClock) { source.UnPause(); playback.pausedByClock = false; }
            // isPlaying is false while paused too; a pause must never return an unfinished clip to the pool.
            if (!playback.pausedByClock && !AudioListener.pause && !source.isPlaying)
            {
                if (playback.voice) { source.pitch = 1f; timelineAudio.RemoveAt(i); }
                else ReturnSource(source);
            }
        }
    }

    private void OnDisable()
    {
        for (int i = timelineAudio.Count - 1; i >= 0; i--)
        {
            var playback = timelineAudio[i];
            if (playback.source == null) { timelineAudio.RemoveAt(i); continue; }
            if (playback.voice) { playback.source.Stop(); playback.source.pitch = 1f; timelineAudio.RemoveAt(i); }
            else ReturnSource(playback.source);
        }
    }
}
