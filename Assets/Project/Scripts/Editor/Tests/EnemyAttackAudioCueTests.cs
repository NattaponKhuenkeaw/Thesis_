#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public class EnemyAttackAudioCueTests
{
    [Test]
    public void ZeroOffsetWaitsForStrikeAndPlaysOnce()
    {
        var cue = new EnemyAttackAudioCue();
        cue.Begin(1f, 0f);
        Assert.That(cue.Tick(2f), Is.False);
        Assert.That(cue.Confirm(0f), Is.True);
        Assert.That(cue.Confirm(0f), Is.False);
        Assert.That(cue.Tick(2f), Is.False);
    }

    [Test]
    public void NegativeOffsetAnticipatesStrikeWithoutPlayingTwice()
    {
        var cue = new EnemyAttackAudioCue();
        cue.Begin(1f, -.25f);
        Assert.That(cue.Tick(.5f), Is.False);
        Assert.That(cue.Tick(.25f), Is.True);
        Assert.That(cue.HasPlayed, Is.True);
        Assert.That(cue.Confirm(-.25f), Is.False);
        Assert.That(cue.Tick(1f), Is.False);
    }

    [Test]
    public void EarlyOffsetCannotStartBeforeWindup()
    {
        var cue = new EnemyAttackAudioCue();
        cue.Begin(.5f, -2f);
        Assert.That(cue.Tick(0f), Is.True);
        Assert.That(cue.Confirm(-2f), Is.False);
    }

    [Test]
    public void PositiveOffsetCountsFromActualStrikeNotPredictedWindup()
    {
        var cue = new EnemyAttackAudioCue();
        cue.Begin(1f, .5f);
        Assert.That(cue.Tick(10f), Is.False);
        Assert.That(cue.Confirm(.5f), Is.False);
        Assert.That(cue.Tick(.25f), Is.False);
        Assert.That(cue.Tick(.25f), Is.True);
        Assert.That(cue.Tick(1f), Is.False);
    }

    [Test]
    public void CancellingWindupDisarmsItsSoundAndConfirmation()
    {
        var cue = new EnemyAttackAudioCue();
        cue.Begin(1f, -.25f);
        cue.Tick(.25f);
        cue.Cancel();
        Assert.That(cue.Tick(2f), Is.False);
        Assert.That(cue.Confirm(-.25f), Is.False);
    }

    [Test]
    public void CancellingAfterStrikeDiscardsDelayedSound()
    {
        var cue = new EnemyAttackAudioCue();
        cue.Begin(1f, .5f);
        cue.Confirm(.5f);
        cue.Cancel();
        Assert.That(cue.Tick(2f), Is.False);
    }

    [Test]
    public void NewAttackAfterCancellationCanPlayNormally()
    {
        var cue = new EnemyAttackAudioCue();
        cue.Begin(1f, -.5f);
        cue.Cancel();
        cue.Begin(.5f, -.25f);
        Assert.That(cue.Tick(.25f), Is.True);
        Assert.That(cue.Confirm(-.25f), Is.False);
    }

    [Test]
    public void LateConfirmationCatchesMissedEarlyCueOnce()
    {
        var cue = new EnemyAttackAudioCue();
        cue.Begin(1f, -.25f);
        Assert.That(cue.Confirm(-.25f), Is.True);
        Assert.That(cue.Tick(5f), Is.False);
    }
}
#endif
