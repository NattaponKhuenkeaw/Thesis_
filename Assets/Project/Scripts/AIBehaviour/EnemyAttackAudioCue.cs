using UnityEngine;

// Attack audio may anticipate the strike, but only confirmed strikes may delay it.
public sealed class EnemyAttackAudioCue
{
    private bool armed;
    private bool pending;
    private bool played;
    private float remaining;
    public bool HasPlayed => played;

    public void Begin(float windup, float offset)
    {
        armed = true;
        played = false;
        pending = offset < 0f;
        remaining = Mathf.Max(0f, windup + offset);
    }

    public bool Confirm(float offset)
    {
        if (!armed || played) return false;
        pending = true;
        remaining = Mathf.Max(0f, offset);
        return Tick(0f);
    }

    public bool Tick(float deltaTime)
    {
        if (!armed || !pending || played) return false;
        remaining -= Mathf.Max(0f, deltaTime);
        if (remaining > 0f) return false;
        pending = false;
        played = true;
        return true;
    }

    public void Cancel() { armed = false; pending = false; }
}
