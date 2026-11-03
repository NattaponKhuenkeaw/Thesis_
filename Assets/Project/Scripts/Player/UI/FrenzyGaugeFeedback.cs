using UnityEngine;
using UnityEngine.UI;

public sealed class FrenzyGaugeFeedback
{
    private static Material _whiteFillMaterial;
    private Image _gauge;
    private Image _flash;

    public static float Pulse => .25f - .25f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI);

    public void Tick(Image gauge, CharacterObject owner)
    {
        bool active = TeamLoader.Instance?.KitRuntimes?.Find(owner)?.BlocksOrdinaryAuraEnergyGain == true;
        if (_gauge != gauge)
        {
            Restore();
            if (_flash != null) Object.Destroy(_flash.gameObject);
            _flash = null;
            _gauge = gauge;
        }

        if (!active || gauge == null || !gauge.isActiveAndEnabled)
        {
            Restore();
            return;
        }

        if (_flash == null)
        {
            if (_whiteFillMaterial == null)
            {
                var shader = Resources.Load<Shader>("FrenzySpriteFlash");
                if (shader == null) return;
                _whiteFillMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            var overlay = new GameObject("Blood Frenzy Gauge Flash", typeof(RectTransform), typeof(Image));
            _flash = overlay.GetComponent<Image>();
            var rect = _flash.rectTransform;
            rect.SetParent(gauge.rectTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _flash.raycastTarget = false;
            _flash.material = _whiteFillMaterial;
        }

        _flash.sprite = gauge.overrideSprite;
        _flash.preserveAspect = gauge.preserveAspect;
        _flash.type = gauge.type;
        _flash.fillMethod = gauge.fillMethod;
        _flash.fillOrigin = gauge.fillOrigin;
        _flash.fillClockwise = gauge.fillClockwise;
        _flash.fillAmount = gauge.fillAmount;
        _flash.color = new Color(1f, 1f, 1f, Pulse);
        _flash.enabled = true;
    }

    public void Restore()
    {
        if (_flash != null) _flash.enabled = false;
    }
}
