using System.Collections.Generic;
using UnityEngine;

public enum AttackCategory
{
    Normal,
    Skill,
    Ultimate
}

[System.Serializable]
public class AttackStepData : ActionFrameData
{
    public const string MotionComponentKey = "motion";
    public const string VfxComponentKey = "vfx";
    public const string HitboxComponentKey = "hitbox";

    private static readonly string[] DefaultComponentOrder =
    {
        MotionComponentKey,
        VfxComponentKey,
        HitboxComponentKey
    };

    [Header("Step Info")]
    public string stepName = "Attack";

    [Header("Editor & Preview")]
    public AnimationClip animationClip;


    [Header("Phase State overrides")]
    [Tooltip("If true, the attack frame progression will stall until the character is grounded (useful for Air Grabs).")]
    public bool stallUntilGrounded = false;
    [Tooltip("If true, the character's vertical position and velocity is locked during the air combo.")]
    public bool lockAirPosition = true;

    [Header("Auto Chain")]
    [Tooltip("If true, automatically chains to the next input or sub-step when recovery/active phase ends, without requiring player input.")]
    public bool autoChainToNext = false;


    // =========================================================
    // Sub Components (New Architecture)
    // =========================================================
    [Header("Sub Components")]
    [SerializeReference] public MotionFrameData motionData;
    [SerializeReference] public VFXFrameData vfxData;
    [SerializeReference] public HitFrameData hitData;
    [HideInInspector] public List<string> componentOrder = new List<string>(DefaultComponentOrder);

    public override void Rescale(float ratio)
    {
        base.Rescale(ratio);

        motionData?.Rescale(ratio);
        vfxData?.Rescale(ratio);
        hitData?.Rescale(ratio);
    }

    public void EnsureComponentOrder()
    {
        componentOrder ??= new List<string>();

        var sanitized = new List<string>(DefaultComponentOrder.Length);
        var seen = new HashSet<string>();

        for (int i = 0; i < componentOrder.Count; i++)
        {
            string key = componentOrder[i];
            if (IsKnownComponentKey(key) && seen.Add(key))
            {
                sanitized.Add(key);
            }
        }

        for (int i = 0; i < DefaultComponentOrder.Length; i++)
        {
            string key = DefaultComponentOrder[i];
            if (seen.Add(key))
            {
                sanitized.Add(key);
            }
        }

        componentOrder.Clear();
        componentOrder.AddRange(sanitized);
    }

    public List<string> GetOrderedComponentKeys(bool includeMissing = false)
    {
        EnsureComponentOrder();

        var ordered = new List<string>(DefaultComponentOrder.Length);
        for (int i = 0; i < componentOrder.Count; i++)
        {
            string key = componentOrder[i];
            if (includeMissing || HasComponent(key))
            {
                ordered.Add(key);
            }
        }

        if (!includeMissing)
        {
            for (int i = 0; i < DefaultComponentOrder.Length; i++)
            {
                string key = DefaultComponentOrder[i];
                if (HasComponent(key) && !ordered.Contains(key))
                {
                    ordered.Add(key);
                }
            }
        }

        return ordered;
    }

    public void MoveComponentKey(string componentKey, int newIndex)
    {
        EnsureComponentOrder();

        List<string> visibleKeys = GetOrderedComponentKeys();
        int fromIndex = visibleKeys.IndexOf(componentKey);
        if (fromIndex < 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(newIndex, 0, visibleKeys.Count - 1);
        if (fromIndex == clampedIndex)
        {
            return;
        }

        visibleKeys.RemoveAt(fromIndex);
        visibleKeys.Insert(clampedIndex, componentKey);

        var hiddenKeys = new List<string>();
        for (int i = 0; i < componentOrder.Count; i++)
        {
            string key = componentOrder[i];
            if (!visibleKeys.Contains(key))
            {
                hiddenKeys.Add(key);
            }
        }

        componentOrder.Clear();
        componentOrder.AddRange(visibleKeys);
        componentOrder.AddRange(hiddenKeys);
    }

    public bool HasComponent(string componentKey)
    {
        return componentKey switch
        {
            MotionComponentKey => motionData != null,
            VfxComponentKey => vfxData != null,
            HitboxComponentKey => hitData != null,
            _ => false
        };
    }

    private static bool IsKnownComponentKey(string componentKey)
    {
        return componentKey == MotionComponentKey
            || componentKey == VfxComponentKey
            || componentKey == HitboxComponentKey;
    }
}
