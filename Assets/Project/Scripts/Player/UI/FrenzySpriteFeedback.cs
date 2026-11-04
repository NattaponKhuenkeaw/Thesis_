using System;
using UnityEngine;

public sealed class FrenzySpriteFeedback : IDisposable
{
    private SpriteRenderer _source;
    private SpriteRenderer _overlay;
    private Material _material;
    private Material _paletteMaterial;
    private Material _originalMaterial;
    private bool _paletteApplied;

    public void Tick(SpriteRenderer source, bool recolorGoenitz = false, bool flash = true)
    {
        if (_source != source || (_overlay == null && source != null))
        {
            RestorePalette();
            if (_overlay != null) DestroyFeedbackObject(_overlay.gameObject);
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
            overlay.layer = source.gameObject.layer;
            overlay.transform.SetParent(source.transform, false);
            _overlay = overlay.AddComponent<SpriteRenderer>();
            _overlay.sharedMaterial = _material;
        }

        if (_overlay == null || source == null) return;
        if (recolorGoenitz) ApplyPalette();
        else RestorePalette();
        _overlay.sprite = source.sprite;
        _overlay.enabled = source.enabled && flash;
        _overlay.flipX = source.flipX;
        _overlay.flipY = source.flipY;
        _overlay.maskInteraction = source.maskInteraction;
        _overlay.sortingLayerID = source.sortingLayerID;
        _overlay.sortingOrder = source.sortingOrder + 1;
        _material.SetFloat("_FlashOpacity", FrenzyGaugeFeedback.Pulse * source.color.a);
    }

    private void ApplyPalette()
    {
        if (_paletteApplied) return;
        if (_paletteMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("GoenitzFrenzyPalette");
            if (shader == null) return;
            _paletteMaterial = new Material(shader);
        }

        _originalMaterial = _source.sharedMaterial;
        if (_originalMaterial != null) _paletteMaterial.CopyPropertiesFromMaterial(_originalMaterial);
        _source.sharedMaterial = _paletteMaterial;
        _paletteApplied = true;
    }

    private void RestorePalette()
    {
        // Another effect may have taken ownership of the renderer since our last tick.
        if (_paletteApplied && _source != null && _source.sharedMaterial == _paletteMaterial)
            _source.sharedMaterial = _originalMaterial;
        _paletteApplied = false;
        _originalMaterial = null;
    }

    public void Dispose()
    {
        Tick(null);
        if (_material != null) DestroyFeedbackObject(_material);
        if (_paletteMaterial != null) DestroyFeedbackObject(_paletteMaterial);
        _material = null;
        _paletteMaterial = null;
    }

    private static void DestroyFeedbackObject(UnityEngine.Object target)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(target);
        else UnityEngine.Object.DestroyImmediate(target);
    }
}
