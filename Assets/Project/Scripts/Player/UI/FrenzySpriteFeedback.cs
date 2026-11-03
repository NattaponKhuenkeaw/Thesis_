using System;
using UnityEngine;

public sealed class FrenzySpriteFeedback : IDisposable
{
    private SpriteRenderer _source;
    private SpriteRenderer _overlay;
    private Material _material;

    public void Tick(SpriteRenderer source)
    {
        if (_source != source || (_overlay == null && source != null))
        {
            if (_overlay != null) UnityEngine.Object.Destroy(_overlay.gameObject);
            _source = source;
            _overlay = null;
            if (source == null) return;

            if (_material == null)
            {
                Shader shader = Resources.Load<Shader>("FrenzySpriteFlash");
                if (shader == null) return;
                _material = new Material(shader);
            }

            var overlay = new GameObject("Blood Frenzy Flash");
            overlay.transform.SetParent(source.transform, false);
            _overlay = overlay.AddComponent<SpriteRenderer>();
            _overlay.sharedMaterial = _material;
        }

        if (_overlay == null || source == null) return;
        _overlay.sprite = source.sprite;
        _overlay.enabled = source.enabled;
        _overlay.flipX = source.flipX;
        _overlay.flipY = source.flipY;
        _overlay.maskInteraction = source.maskInteraction;
        _overlay.sortingLayerID = source.sortingLayerID;
        _overlay.sortingOrder = source.sortingOrder + 1;
        _material.SetFloat("_FlashOpacity", FrenzyGaugeFeedback.Pulse * source.color.a);
    }

    public void Dispose()
    {
        Tick(null);
        if (_material != null) UnityEngine.Object.Destroy(_material);
    }
}
