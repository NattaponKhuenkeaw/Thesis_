using UnityEngine;

/// <summary>
/// Red aim line from a ranged enemy muzzle. Telegraph only — not a damage volume.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyRangedAimLaser : MonoBehaviour
{
    [SerializeField] private float width = 0.06f;
    [SerializeField] private Color laserColor = new Color(1f, 0.12f, 0.08f, 0.92f);

    private LineRenderer line;
    private EnemyController owner;
    private float length = 7f;
    private bool visible;
    private Material runtimeMaterial;

    private void OnDisable()
    {
        Hide();
    }

    private void LateUpdate()
    {
        if (!visible)
        {
            return;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }

    public void Show(EnemyController source, float shotLength)
    {
        owner = source;
        length = Mathf.Max(0.25f, shotLength);
        visible = owner != null;
        EnsureLine();
        if (line == null)
        {
            visible = false;
            return;
        }

        line.enabled = visible;
        Refresh();
    }

    public void Hide()
    {
        visible = false;
        if (line != null)
        {
            line.enabled = false;
        }
    }

    private void Refresh()
    {
        if (line == null)
        {
            return;
        }

        Vector3 start = owner != null ? owner.GetRangedMuzzleWorldPosition() : transform.position;
        Vector2 direction = owner != null ? owner.GetRangedShotDirection() : Vector2.right;
        Vector3 end = start + (Vector3)(direction * length);
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = width;
        line.endWidth = width * 0.65f;
        line.startColor = laserColor;
        line.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, 0.15f);
    }

    private void EnsureLine()
    {
        if (line != null)
        {
            return;
        }

        Transform holder = transform.Find("RangedAimLaser");
        if (holder == null)
        {
            var go = new GameObject("RangedAimLaser");
            go.transform.SetParent(transform, false);
            holder = go.transform;
        }

        line = holder.GetComponent<LineRenderer>();
        if (line == null)
        {
            line = holder.gameObject.AddComponent<LineRenderer>();
        }

        if (line == null)
        {
            Debug.LogWarning("[EnemyRangedAimLaser] LineRenderer could not be created.", this);
            return;
        }

        line.positionCount = 2;
        line.useWorldSpace = true;
        line.numCapVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 540;
        line.alignment = LineAlignment.View;
        AssignMaterial(line);
        line.enabled = false;
    }

    private void AssignMaterial(LineRenderer renderer)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Hidden/InternalErrorShader");
        }

        if (shader == null)
        {
            return;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }

        runtimeMaterial = new Material(shader);
        runtimeMaterial.color = laserColor;
        renderer.sharedMaterial = runtimeMaterial;
    }
}
