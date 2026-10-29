#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class AnimationSlowMotionNativeTests
{
    private static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    [Test]
    public void IndependentMotionRequestsFullDisplacementOverNormalSpeedTime()
    {
        var host = new GameObject("Independent Motion Requests"); host.SetActive(false);
        float savedScale = Time.timeScale, savedStep = Time.fixedDeltaTime;
        try
        {
            var movement = host.AddComponent<PlayerMovement>();
            var body = host.GetComponent<Rigidbody2D>();
            typeof(PlayerMovement).GetField("rb", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, body);
            Time.timeScale = 0.25f; Time.fixedDeltaTime = 0.005f;
            var key = new MotionKeyframe { taskType = MotionTaskType.Absolute, startFrame = 0, endFrame = 60,
                position = new Vector3(6f, 0f, 0f), easing = LeanTweenType.linear };
            var routine = (IEnumerator)Call(movement, "AttackMovementCoroutine", key, false);
            float travel = 0f, realElapsed = 0f;
            int ticks = 0;
            var velocity = typeof(PlayerMovement).GetField("attackMotionVelocityX", BindingFlags.Instance | BindingFlags.NonPublic);
            while (routine.MoveNext() && ticks++ < 5000)
            {
                travel += (float)velocity.GetValue(movement) * Time.fixedDeltaTime;
                realElapsed += Time.fixedUnscaledDeltaTime;
            }
            Assert.That(ticks, Is.LessThan(5000));
            Assert.That(travel, Is.EqualTo(6f).Within(0.001f));
            Assert.That(realElapsed, Is.EqualTo(1f).Within(Time.fixedUnscaledDeltaTime + 0.001f));
        }
        finally { Time.timeScale = savedScale; Time.fixedDeltaTime = savedStep; Object.DestroyImmediate(host); }
    }

    [Test]
    public void TimelineAudioUpdatesPitchAndDoesNotReturnPausedSourcesToPool()
    {
        var host = new GameObject("Slow Motion Audio Test"); host.SetActive(false);
        var clip = AudioClip.Create("Slow Motion Audio", 48000, 1, 48000, false);
        float savedScale = Time.timeScale;
        bool savedPause = AudioListener.pause;
        try
        {
            var manager = host.AddComponent<GameAudioManager>(); manager.initialPoolSize = 1;
            Call(manager, "InitializePool");
            var source = (AudioSource)Call(manager, "GetSource"); source.clip = clip;
            Call(manager, "TrackTimelineAudio", source, new AudioEntry { clip = clip, slowMotionAware = true, pitchVariation = 1.2f }, false);
            AudioListener.pause = true; Time.timeScale = 0.25f;
            Call(manager, "UpdateTimelineAudio");
            Assert.That(source.pitch, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(manager.ActiveTimelineAudioCount, Is.EqualTo(1));
            Time.timeScale = 0f; Call(manager, "UpdateTimelineAudio");
            Assert.That(manager.ActiveTimelineAudioCount, Is.EqualTo(1));
            Time.timeScale = 1f; Call(manager, "UpdateTimelineAudio");
            Assert.That(source.pitch, Is.EqualTo(1.2f).Within(0.001f));
            AudioListener.pause = false; source.Stop(); Call(manager, "UpdateTimelineAudio");
            Assert.That(manager.ActiveTimelineAudioCount, Is.Zero);
            Assert.That(source.pitch, Is.EqualTo(1f));
            Assert.That(source.clip, Is.Null);
            Assert.That(Call(manager, "GetSource"), Is.SameAs(source));
        }
        finally { Time.timeScale = savedScale; AudioListener.pause = savedPause; Object.DestroyImmediate(host); Object.DestroyImmediate(clip); }
    }

    [Test]
    public void CapturedWorldMotionIgnoresSlowMotionButStillFreezesAndCancels()
    {
        var host = new GameObject("Captured Clock Test"); host.SetActive(false);
        var floorHost = new GameObject("Captured Clock Support", typeof(BoxCollider2D));
        float savedScale = Time.timeScale, savedStep = Time.fixedDeltaTime;
        try
        {
            var movement = host.AddComponent<PlayerMovement>();
            var body = host.GetComponent<Rigidbody2D>(); body.position = new Vector2(0f, 10f);
            typeof(PlayerMovement).GetField("rb", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, body);
            typeof(PlayerMovement).GetField("solidColliders", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, new Collider2D[0]);
            var placement = new QteEntryPlacement(body.position, Vector2.zero, Vector2.zero, Vector2.up,
                Vector2.zero, floorHost.GetComponent<BoxCollider2D>(), 1, 1);
            movement.PrepareCapturedWorldMotion(placement);
            var motion = new MotionKeyframe { taskType = MotionTaskType.CapturedWorldPoint,
                startFrame = 10, endFrame = 70, easing = LeanTweenType.linear };
            Time.timeScale = 0.25f; Time.fixedDeltaTime = 0.005f;
            Assert.That(movement.SampleCapturedWorldMotion(motion, 10, false), Is.True);
            var lastTick = typeof(PlayerMovement).GetField("_capturedLastFixedTime", BindingFlags.Instance | BindingFlags.NonPublic);
            int ticks = Mathf.CeilToInt(0.5f / Time.fixedUnscaledDeltaTime);
            for (int i = 0; i < ticks; i++)
            {
                lastTick.SetValue(movement, float.NegativeInfinity); Call(movement, "TickIndependentCapturedMotion");
            }
            Assert.That(body.position.y, Is.LessThan(6f));
            Assert.That(body.position.y, Is.GreaterThan(3f));
            Vector2 frozen = body.position; Time.timeScale = 0f;
            lastTick.SetValue(movement, float.NegativeInfinity); Call(movement, "TickIndependentCapturedMotion");
            Assert.That(body.position, Is.EqualTo(frozen));
            Time.timeScale = 0.25f;
            for (int i = 0; i <= ticks; i++)
            {
                lastTick.SetValue(movement, float.NegativeInfinity); Call(movement, "TickIndependentCapturedMotion");
            }
            Assert.That(body.position.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(movement.SampleCapturedWorldMotion(motion, 10, false), Is.True);
            Assert.That(body.position.y, Is.EqualTo(0f).Within(0.001f), "An unchanged action frame must not rewind independent movement.");
            movement.ClearCapturedWorldMotion();
            lastTick.SetValue(movement, float.NegativeInfinity); Call(movement, "TickIndependentCapturedMotion");
            Assert.That(movement.HasCapturedWorldMotion, Is.False);
        }
        finally { Time.timeScale = savedScale; Time.fixedDeltaTime = savedStep; Object.DestroyImmediate(host); Object.DestroyImmediate(floorHost); }
    }
}
#endif
