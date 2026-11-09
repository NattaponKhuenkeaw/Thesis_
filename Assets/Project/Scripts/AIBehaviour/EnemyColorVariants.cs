using System;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class EnemyColorVariants : MonoBehaviour
{
    [Serializable]
    public sealed class Variant
    {
        public string name;
        public RuntimeAnimatorController controller;
        public Sprite spawnSprite;
        public Sprite[] projectileFrames = Array.Empty<Sprite>();
    }

    [SerializeField] private Animator targetAnimator;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Variant[] variants = Array.Empty<Variant>();
    [SerializeField] private Sprite[] originalProjectileFrames = Array.Empty<Sprite>();

    internal Sprite[] SourceProjectileFrames => originalProjectileFrames.Length > 0
        ? originalProjectileFrames : variants.Length > 0 ? variants[0].projectileFrames : Array.Empty<Sprite>();

    public int SelectedIndex { get; private set; } = -1;
    public int VariantCount => variants.Length;

    private void Awake()
    {
        // One roll per instance; temporary disables must not change a living enemy's color.
        RandomizeAppearance();
    }

    public void RandomizeAppearance()
    {
        if (variants.Length > 0)
            ApplyVariant(UnityEngine.Random.Range(0, variants.Length));
    }

    public bool ApplyVariant(int index)
    {
        if (index < 0 || index >= variants.Length || variants[index] == null || variants[index].controller == null)
            return false;

        SelectedIndex = index;
        if (targetAnimator != null)
            targetAnimator.runtimeAnimatorController = variants[index].controller;
        if (targetRenderer != null && variants[index].spawnSprite != null)
            targetRenderer.sprite = variants[index].spawnSprite;
        return true;
    }

    public Sprite ResolveProjectileSprite(Sprite source)
    {
        if (SelectedIndex < 0 || source == null)
            return source;

        var original = SourceProjectileFrames;
        var selected = variants[SelectedIndex].projectileFrames;
        for (int i = 0; i < original.Length && i < selected.Length; i++)
            if (original[i] == source)
                return selected[i] != null ? selected[i] : source;
        return source;
    }

    public Sprite[] ResolveProjectileAnimation(Sprite[] source)
    {
        if (SelectedIndex < 0 || source == null)
            return source;

        var original = SourceProjectileFrames;
        var selected = variants[SelectedIndex].projectileFrames;
        if (source.Length != original.Length || selected.Length != original.Length)
            return source;
        for (int i = 0; i < source.Length; i++)
            if (source[i] != original[i] || selected[i] == null)
                return source;
        return selected;
    }
}
