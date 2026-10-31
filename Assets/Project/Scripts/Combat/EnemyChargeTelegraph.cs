using UnityEngine;

/// <summary>Presentation only: reads the shield guard's existing windup, never advances it.</summary>
[DisallowMultipleComponent]
public sealed class EnemyChargeTelegraph : MonoBehaviour
{
    [SerializeField] private LineRenderer background;
    [SerializeField] private LineRenderer fill;
    [SerializeField] private LineRenderer outline;
    [SerializeField] private Color laneColor = new Color(1f, .8f, .12f, .10f);
    [SerializeField] private Color fillColor = new Color(1f, .8f, .12f, .30f);
    [SerializeField] private Color outlineColor = new Color(1f, .85f, .25f, .65f);
    [SerializeField, Min(.01f)] private float outlineWidth = .035f;

    private EnemyController owner;
    private EnemyShieldGuard guard;

    private void Awake()
    {
        owner = GetComponentInParent<EnemyController>();
        guard = GetComponentInParent<EnemyShieldGuard>();
        Hide();
    }

    private void OnDisable() => Hide();

    private void LateUpdate()
    {
        if (owner == null || guard == null || !guard.isActiveAndEnabled
            || !owner.IsCombatReady || owner.CurrentReactionState != EnemyReactionState.None
            || guard.Phase != EnemyShieldGuard.GuardPhase.Windup
            || owner.Kit == null || !owner.Kit.BodyCharge || owner.bodyCollider == null)
        {
            Hide();
            return;
        }

        Bounds body = owner.bodyCollider.bounds;
        float direction = EnemyFacingUtil.GetFacingSign(owner.transform);
        float length = Mathf.Max(0f, owner.ResolveLungeDistance(0f));
        if (length <= 0f || body.size.y <= 0f)
        {
            Hide();
            return;
        }

        Vector3 start = new Vector3(direction > 0f ? body.max.x : body.min.x,
            body.center.y, owner.transform.position.z);
        Vector3 end = start + Vector3.right * (direction * length);
        float progress = guard.WindupProgress;
        DrawStrip(background, start, end, body.size.y, laneColor);
        DrawStrip(fill, start, Vector3.Lerp(start, end, progress), body.size.y, fillColor);
        if (fill != null) fill.enabled = progress > 0f;
        if (outline != null)
        {
            Vector3 halfHeight = Vector3.up * body.extents.y;
            outline.positionCount = 4;
            outline.loop = true;
            outline.SetPosition(0, start - halfHeight);
            outline.SetPosition(1, end - halfHeight);
            outline.SetPosition(2, end + halfHeight);
            outline.SetPosition(3, start + halfHeight);
            outline.startWidth = outline.endWidth = outlineWidth;
            outline.startColor = outline.endColor = outlineColor;
            outline.enabled = true;
        }
    }

    private static void DrawStrip(LineRenderer line, Vector3 start, Vector3 end, float height, Color color)
    {
        if (line == null) return;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = line.endWidth = height;
        line.startColor = line.endColor = color;
        line.enabled = true;
    }

    private void Hide()
    {
        if (background != null) background.enabled = false;
        if (fill != null) fill.enabled = false;
        if (outline != null) outline.enabled = false;
    }
}
