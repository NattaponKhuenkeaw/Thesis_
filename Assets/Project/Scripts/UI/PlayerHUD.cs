using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Project.ShopSystem;

/// <summary>
/// Player HUD that subscribes to PlayerCharacter HP and runtime effect events.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    private static readonly string[] HudRootNames = { "PlayerHP_UI", "PlayerUI" };
    private static readonly Dictionary<Transform, PlayerHUD> RuntimeEffectOwners = new Dictionary<Transform, PlayerHUD>();

    private struct RuntimeEffectDisplayEntry
    {
        public RuntimeEffectInstance effect;
        public int stackCount;
    }

    [Header("References")]
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private TeamLoader teamLoader;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Transform runtimeEffectContainer;
    [SerializeField] private RuntimeEffectIconUI runtimeEffectIconPrefab;
    [SerializeField] private Slider shieldSlider;
    [SerializeField] private float healthLerpDelay = 0.25f;
    [SerializeField] private float healthLerpDuration = 0.5f;
    [SerializeField] private Color shieldFillColor = new Color(0.58f, 0.58f, 0.58f, 0.85f);

    private RectTransform hudRoot;
    private Slider healthSlider;
    private Slider healthLerpSlider;
    private GameObject shieldSliderRoot;
    private TMP_Text nameText;
    private TMP_Text hpText;
    private Coroutine healthLerpCoroutine;
    private PlayerCharacter subscribedPlayer;
    private PlayerRunInventory subscribedEffectInventory;
    private readonly List<RuntimeEffectIconUI> runtimeEffectIcons = new List<RuntimeEffectIconUI>();
    private readonly List<RuntimeEffectDisplayEntry> runtimeEffectDisplayEntries = new List<RuntimeEffectDisplayEntry>();
    private readonly Dictionary<string, int> runtimeEffectStackIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
    private CharacterObject lastCharacterObject;
    private float lastCharacterMaxHp = -1f;
    private bool runtimeEffectsDirty = true;
    private bool runtimeEffectContainerSanitized;
    private bool healthDisplayInitialized;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void Start()
    {
        EnsureHudReady();
    }

    private void OnEnable()
    {
        runtimeEffectsDirty = true;
        RebindEvents();
        RebindRuntimeEffectEvents();
        RefreshHUD();
    }

    private void OnDisable()
    {
        ReleaseRuntimeEffectOwnership();
        StopHealthLerp();

        if (subscribedPlayer != null)
        {
            subscribedPlayer.OnHealthChanged -= HandleHealthChanged;
            subscribedPlayer.OnCharacterChanged -= HandleCharacterChanged;
            subscribedPlayer.OnDied -= HandleDied;
            subscribedPlayer = null;
        }

        if (subscribedEffectInventory != null)
        {
            subscribedEffectInventory.OnRuntimeEffectsChanged -= HandleRuntimeEffectsChanged;
            subscribedEffectInventory = null;
        }
    }

    private void LateUpdate()
    {
        if (hudRoot == null || playerCharacter == null || targetCanvas == null)
        {
            if (Time.frameCount % 20 == 0)
                EnsureHudReady();

            return;
        }

        if (HasCharacterIdentityChanged())
        {
            RefreshHUD();
        }

        if (runtimeEffectsDirty)
        {
            RebuildRuntimeEffects();
        }
    }

    private void CacheReferences()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (teamLoader == null)
            teamLoader = UnityEngine.Object.FindFirstObjectByType<TeamLoader>();

        if (targetCanvas == null || FindHudRoot(targetCanvas.transform) == null)
            targetCanvas = FindTargetCanvas();

        if (runtimeEffectContainer == null && hudRoot != null)
        {
            runtimeEffectContainer = FindRuntimeEffectContainer(hudRoot);
            runtimeEffectContainerSanitized = false;
        }

        EnsureShieldSlider();
    }

    private void EnsureHudReady()
    {
        CacheReferences();

        if (targetCanvas == null)
            return;

        CreateHudIfNeeded();
        RebindEvents();
        RebindRuntimeEffectEvents();
        RefreshHUD();
    }

    private void RebindEvents()
    {
        if (playerCharacter == subscribedPlayer)
            return;

        if (subscribedPlayer != null)
        {
            subscribedPlayer.OnHealthChanged -= HandleHealthChanged;
            subscribedPlayer.OnCharacterChanged -= HandleCharacterChanged;
            subscribedPlayer.OnDied -= HandleDied;
        }

        subscribedPlayer = playerCharacter;

        if (subscribedPlayer != null)
        {
            subscribedPlayer.OnHealthChanged += HandleHealthChanged;
            subscribedPlayer.OnCharacterChanged += HandleCharacterChanged;
            subscribedPlayer.OnDied += HandleDied;
        }
    }

    private void CreateHudIfNeeded()
    {
        if (hudRoot != null)
            return;

        Transform existingRoot = FindExistingHudRoot();
        if (existingRoot != null)
            BindExistingHud(existingRoot);
    }

    private void HandleHealthChanged(float currentHp, float maxHp)
    {
        RefreshHUD();
    }

    private void HandleDied()
    {
        RefreshHUD();
    }

    private void HandleCharacterChanged(CharacterObject newCharacter)
    {
        ResetCharacterIdentityCache();
        runtimeEffectsDirty = true;
        EnsureHudReady();
    }

    private void RefreshHUD()
    {
        if (hudRoot == null)
            return;

        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        string displayName = "Player";
        if (playerCharacter != null && playerCharacter.characterObject != null && playerCharacter.characterObject.metaData != null)
        {
            if (!string.IsNullOrWhiteSpace(playerCharacter.characterObject.metaData.displayName))
                displayName = playerCharacter.characterObject.metaData.displayName;
        }

        if (nameText != null)
            nameText.text = displayName;

        if (playerCharacter == null)
        {
            if (hpText != null)
                hpText.text = "-- / --";

            if (healthSlider != null)
                healthSlider.value = 0f;

            if (healthLerpSlider != null)
                healthLerpSlider.value = 0f;

            SetShieldVisual(0f, 1f);
            healthDisplayInitialized = false;
            UpdateCharacterIdentityCache();
            return;
        }

        float maxHp = Mathf.Max(1f, playerCharacter.MaxHP);
        float currentHp = Mathf.Clamp(playerCharacter.CurrentHP, 0f, maxHp);

        if (teamLoader != null && playerCharacter.characterObject != null)
        {
            float trackedMaxHp = teamLoader.GetCharacterMaxHP(playerCharacter.characterObject);
            if (trackedMaxHp > 0f)
            {
                maxHp = trackedMaxHp;
                currentHp = Mathf.Clamp(teamLoader.GetCharacterCurrentHP(playerCharacter.characterObject), 0f, maxHp);
            }
        }

        float normalizedHp = Mathf.Clamp01(currentHp / maxHp);

        ApplyHealthTarget(normalizedHp);

        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";

        UpdateShieldVisual(maxHp);
        UpdateCharacterIdentityCache();
    }

    private void ApplyHealthTarget(float normalizedHp)
    {
        normalizedHp = Mathf.Clamp01(normalizedHp);
        if (!healthDisplayInitialized || !Application.isPlaying)
        {
            healthDisplayInitialized = true;
            SetHealthVisual(normalizedHp, true);
            return;
        }

        SetHealthVisual(normalizedHp, false);
        if (healthLerpSlider != null)
        {
            StopHealthLerp();
            healthLerpCoroutine = StartCoroutine(HealthLerpRoutine(normalizedHp));
        }
    }

    private void SetHealthVisual(float normalizedHp, bool includeLerpSlider)
    {
        normalizedHp = Mathf.Clamp01(normalizedHp);

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.value = normalizedHp;
        }

        if (healthLerpSlider != null)
        {
            healthLerpSlider.minValue = 0f;
            healthLerpSlider.maxValue = 1f;
            if (includeLerpSlider)
            {
                healthLerpSlider.value = normalizedHp;
            }
        }
    }

    private IEnumerator HealthLerpRoutine(float targetValue)
    {
        yield return new WaitForSeconds(healthLerpDelay);

        if (healthLerpSlider == null)
        {
            healthLerpCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        float startValue = healthLerpSlider.value;
        float duration = Mathf.Max(0.01f, healthLerpDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            healthLerpSlider.value = Mathf.Lerp(startValue, targetValue, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        healthLerpSlider.value = targetValue;
        healthLerpCoroutine = null;
    }

    private void StopHealthLerp()
    {
        if (healthLerpCoroutine == null)
        {
            return;
        }

        StopCoroutine(healthLerpCoroutine);
        healthLerpCoroutine = null;
    }

    private void RebindRuntimeEffectEvents()
    {
        PlayerRunInventory inventory = ResolveEffectInventory();
        if (inventory == subscribedEffectInventory)
        {
            return;
        }

        if (subscribedEffectInventory != null)
        {
            subscribedEffectInventory.OnRuntimeEffectsChanged -= HandleRuntimeEffectsChanged;
        }

        subscribedEffectInventory = inventory;
        if (subscribedEffectInventory != null)
        {
            subscribedEffectInventory.OnRuntimeEffectsChanged += HandleRuntimeEffectsChanged;
        }
    }

    private PlayerRunInventory ResolveEffectInventory()
    {
        if (teamLoader == null)
        {
            teamLoader = UnityEngine.Object.FindFirstObjectByType<TeamLoader>();
        }

        return teamLoader != null ? teamLoader.RunInventory : null;
    }

    private void HandleRuntimeEffectsChanged(CharacterObject changedCharacter)
    {
        if (playerCharacter == null || changedCharacter == playerCharacter.characterObject)
        {
            runtimeEffectsDirty = true;
        }
    }

    private void RebuildRuntimeEffects()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
        {
            SetRuntimeEffectIconCount(0);
            runtimeEffectDisplayEntries.Clear();
            runtimeEffectsDirty = false;
            return;
        }

        if (runtimeEffectContainer == null)
        {
            Transform resolvedContainer = hudRoot != null
                ? FindRuntimeEffectContainer(hudRoot)
                : targetCanvas != null ? FindRuntimeEffectContainer(targetCanvas.transform) : null;
            if (resolvedContainer != runtimeEffectContainer)
            {
                runtimeEffectContainer = resolvedContainer;
                runtimeEffectContainerSanitized = false;
            }
        }

        if (runtimeEffectContainer == null)
        {
            return;
        }

        if (!TryClaimRuntimeEffectOwnership())
        {
            return;
        }

        runtimeEffectsDirty = false;
        RebindRuntimeEffectEvents();
        IReadOnlyList<RuntimeEffectInstance> effects = subscribedEffectInventory != null
            ? subscribedEffectInventory.GetActiveRuntimeEffects(playerCharacter.characterObject)
            : playerCharacter.RuntimeEffects != null ? playerCharacter.RuntimeEffects.ActiveEffects : null;

        BuildDisplayEntries(effects);
        UpdateShieldVisual(effects);

        int count = runtimeEffectDisplayEntries.Count;
        SetRuntimeEffectIconCount(count);
        for (int i = 0; i < count; i++)
        {
            RuntimeEffectDisplayEntry entry = runtimeEffectDisplayEntries[i];
            runtimeEffectIcons[i].Bind(entry.effect, entry.stackCount);
        }

        playerCharacter.RuntimeEffects?.SyncVisuals(effects);
    }

    private void BuildDisplayEntries(IReadOnlyList<RuntimeEffectInstance> effects)
    {
        runtimeEffectDisplayEntries.Clear();
        if (effects == null || effects.Count == 0)
        {
            return;
        }

        runtimeEffectStackIndexes.Clear();
        for (int i = 0; i < effects.Count; i++)
        {
            RuntimeEffectInstance effect = effects[i];
            if (effect == null || effect.definition == null)
            {
                continue;
            }

            if (RuntimeEffectRuntimeUtility.GetKind(effect) == RuntimeEffectKind.Effect)
            {
                continue;
            }

            string stackKey = effect.StackKey ?? string.Empty;
            bool canAggregate = RuntimeEffectRuntimeUtility.IsStackable(effect) || !string.IsNullOrEmpty(stackKey);
            if (canAggregate && runtimeEffectStackIndexes.TryGetValue(stackKey, out int existingIndex))
            {
                RuntimeEffectDisplayEntry entry = runtimeEffectDisplayEntries[existingIndex];
                entry.stackCount += Mathf.Max(1, effect.stackCount);
                runtimeEffectDisplayEntries[existingIndex] = entry;
                continue;
            }

            if (canAggregate)
            {
                runtimeEffectStackIndexes.Add(stackKey, runtimeEffectDisplayEntries.Count);
            }

            runtimeEffectDisplayEntries.Add(new RuntimeEffectDisplayEntry
            {
                effect = effect,
                stackCount = Mathf.Max(1, effect.stackCount)
            });
        }
        runtimeEffectStackIndexes.Clear();
    }

    private void SetRuntimeEffectIconCount(int count)
    {
        if (!runtimeEffectContainerSanitized)
        {
            RemoveUntrackedRuntimeEffectIcons();
            runtimeEffectContainerSanitized = true;
        }

        for (int i = runtimeEffectIcons.Count; i < count; i++)
        {
            runtimeEffectIcons.Add(CreateRuntimeEffectIcon());
        }

        for (int i = 0; i < runtimeEffectIcons.Count; i++)
        {
            if (runtimeEffectIcons[i] != null)
            {
                runtimeEffectIcons[i].gameObject.SetActive(i < count);
            }
        }
    }

    private RuntimeEffectIconUI CreateRuntimeEffectIcon()
    {
        if (runtimeEffectIconPrefab != null)
        {
            return Instantiate(runtimeEffectIconPrefab, runtimeEffectContainer);
        }

        GameObject iconObject = new GameObject(
            "RuntimeEffectIcon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RuntimeEffectIconUI));
        iconObject.transform.SetParent(runtimeEffectContainer, false);
        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(28f, 28f);
        return iconObject.GetComponent<RuntimeEffectIconUI>();
    }

    private bool TryClaimRuntimeEffectOwnership()
    {
        if (runtimeEffectContainer == null)
        {
            return false;
        }

        if (RuntimeEffectOwners.TryGetValue(runtimeEffectContainer, out PlayerHUD owner)
            && owner != null
            && owner != this
            && owner.isActiveAndEnabled)
        {
            return false;
        }

        RuntimeEffectOwners[runtimeEffectContainer] = this;
        return true;
    }

    private void ReleaseRuntimeEffectOwnership()
    {
        if (runtimeEffectContainer == null)
        {
            return;
        }

        if (RuntimeEffectOwners.TryGetValue(runtimeEffectContainer, out PlayerHUD owner) && owner == this)
        {
            RuntimeEffectOwners.Remove(runtimeEffectContainer);
        }
    }

    private void RemoveUntrackedRuntimeEffectIcons()
    {
        if (runtimeEffectContainer == null)
        {
            return;
        }

        RuntimeEffectIconUI[] existingIcons = runtimeEffectContainer.GetComponentsInChildren<RuntimeEffectIconUI>(true);
        for (int i = 0; i < existingIcons.Length; i++)
        {
            RuntimeEffectIconUI existingIcon = existingIcons[i];
            if (existingIcon == null || runtimeEffectIcons.Contains(existingIcon))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(existingIcon.gameObject);
            }
            else
            {
                DestroyImmediate(existingIcon.gameObject);
            }
        }
    }

    private Transform FindRuntimeEffectContainer(Transform parent)
    {
        return FindChildRecursive(parent, "BuffContainer")
            ?? FindChildRecursive(parent, "BuffDebuffContainer")
            ?? FindChildRecursive(parent, "StatusEffectContainer")
            ?? FindChildRecursive(parent, "RuntimeEffectContainer")
            ?? FindChildRecursive(parent, "EffectContainer");
    }

    private bool HasCharacterIdentityChanged()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
        {
            return lastCharacterObject != null;
        }

        float maxHp = playerCharacter.Stats != null ? playerCharacter.Stats.maxHP : 0f;
        return lastCharacterObject != playerCharacter.characterObject
            || !Mathf.Approximately(lastCharacterMaxHp, maxHp);
    }

    private void UpdateCharacterIdentityCache(float maxHpOverride = -1f)
    {
        lastCharacterObject = playerCharacter != null ? playerCharacter.characterObject : null;
        lastCharacterMaxHp = maxHpOverride >= 0f
            ? maxHpOverride
            : playerCharacter != null && playerCharacter.Stats != null ? playerCharacter.Stats.maxHP : 0f;
    }

    private void ResetCharacterIdentityCache()
    {
        lastCharacterObject = null;
        lastCharacterMaxHp = -1f;
    }

    private Canvas FindTargetCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null)
                continue;

            if (FindHudRoot(canvas.transform) != null)
                return canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        }

        return null;
    }

    private Transform FindExistingHudRoot()
    {
        if (targetCanvas != null)
        {
            Transform playerUi = FindHudRoot(targetCanvas.transform);
            if (playerUi != null)
                return playerUi;
        }

        return null;
    }

    private void BindExistingHud(Transform root)
    {
        hudRoot = root as RectTransform;
        if (hudRoot == null)
            return;

        Transform hpSlideTransform = FindChildRecursive(root, "HP_Slide");
        if (hpSlideTransform != null)
        {
            healthSlider = hpSlideTransform.GetComponent<Slider>();
        }

        Transform shieldSlideTransform = FindChildRecursive(root, "Shield_Slide")
            ?? FindChildRecursive(root, "ShieldSlider")
            ?? FindChildRecursive(root, "Shield_Slider");
        if (shieldSlideTransform != null)
        {
            shieldSlider = shieldSlideTransform.GetComponent<Slider>();
            shieldSliderRoot = shieldSlideTransform.gameObject;
        }

        healthLerpSlider = FindHealthLerpSlider(root, healthSlider);
        if (healthLerpSlider != null && healthSlider != null)
        {
            healthLerpSlider.minValue = healthSlider.minValue;
            healthLerpSlider.maxValue = healthSlider.maxValue;
        }

        Transform heroNameTransform = FindChildRecursive(root, "HeroName");
        if (heroNameTransform != null)
            nameText = heroNameTransform.GetComponent<TMP_Text>();

        Transform hpTextTransform = FindChildRecursive(root, "HP");
        if (hpTextTransform != null)
            hpText = hpTextTransform.GetComponent<TMP_Text>();

        if (nameText == null || hpText == null || healthSlider == null)
        {
            healthSlider = null;
            healthLerpSlider = null;
            nameText = null;
            hpText = null;
            hudRoot = null;
        }
        else
        {
            EnsureShieldSlider();
        }
    }

    private void EnsureShieldSlider()
    {
        if (shieldSlider != null)
        {
            shieldSliderRoot = shieldSlider.gameObject;
            return;
        }

        if (hudRoot != null)
        {
            Transform shieldSlideTransform = FindChildRecursive(hudRoot, "Shield_Slide")
                ?? FindChildRecursive(hudRoot, "ShieldSlider")
                ?? FindChildRecursive(hudRoot, "Shield_Slider");
            if (shieldSlideTransform != null)
            {
                shieldSlider = shieldSlideTransform.GetComponent<Slider>();
                shieldSliderRoot = shieldSlideTransform.gameObject;
                return;
            }
        }

        if (healthSlider == null || healthSlider.transform.parent == null)
        {
            return;
        }

        shieldSlider = CreateFallbackShieldSlider(healthSlider);
        shieldSliderRoot = shieldSlider != null ? shieldSlider.gameObject : null;
        SetShieldVisual(0f, 1f);
    }

    private Slider CreateFallbackShieldSlider(Slider sourceSlider)
    {
        RectTransform sourceRect = sourceSlider.transform as RectTransform;
        Transform parent = sourceSlider.transform.parent;

        GameObject sliderObject = new GameObject(
            "Shield_Slide",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Slider));
        sliderObject.transform.SetParent(parent, false);

        RectTransform rect = sliderObject.GetComponent<RectTransform>();
        if (sourceRect != null)
        {
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = sourceRect.pivot;
            rect.anchoredPosition = sourceRect.anchoredPosition;
            rect.sizeDelta = sourceRect.sizeDelta;
            rect.localScale = sourceRect.localScale;
            sliderObject.transform.SetSiblingIndex(sourceSlider.transform.GetSiblingIndex() + 1);
        }

        Image backgroundImage = sliderObject.GetComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, 0f);
        backgroundImage.raycastTarget = false;

        GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillArea = fillAreaObject.GetComponent<RectTransform>();
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = shieldFillColor;
        fillImage.raycastTarget = false;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.targetGraphic = null;
        slider.fillRect = fillRect;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        sliderObject.SetActive(false);
        return slider;
    }

    private void UpdateShieldVisual(float maxHpOverride = -1f)
    {
        UpdateShieldVisual(null, maxHpOverride);
    }

    private void UpdateShieldVisual(IReadOnlyList<RuntimeEffectInstance> effects, float maxHpOverride = -1f)
    {
        EnsureShieldSlider();
        if (shieldSlider == null || playerCharacter == null || playerCharacter.characterObject == null)
        {
            SetShieldVisual(0f, 1f);
            return;
        }

        float maxHp = maxHpOverride > 0f ? maxHpOverride : ResolveCurrentMaxHp();
        float shieldAmount = GetActiveShieldAmount(effects);
        SetShieldVisual(shieldAmount, maxHp);
    }

    private void SetShieldVisual(float shieldAmount, float maxHp)
    {
        if (shieldSlider == null)
        {
            return;
        }

        float resolvedMaxHp = Mathf.Max(1f, maxHp);
        float resolvedShield = Mathf.Max(0f, shieldAmount);
        shieldSlider.minValue = 0f;
        shieldSlider.maxValue = resolvedMaxHp;
        shieldSlider.value = Mathf.Min(resolvedShield, resolvedMaxHp);

        GameObject root = shieldSliderRoot != null ? shieldSliderRoot : shieldSlider.gameObject;
        root.SetActive(resolvedShield > 0.01f);
    }

    private float ResolveCurrentMaxHp()
    {
        if (teamLoader != null && playerCharacter != null && playerCharacter.characterObject != null)
        {
            float trackedMaxHp = teamLoader.GetCharacterMaxHP(playerCharacter.characterObject);
            if (trackedMaxHp > 0f)
            {
                return trackedMaxHp;
            }
        }

        return playerCharacter != null ? Mathf.Max(1f, playerCharacter.MaxHP) : 1f;
    }

    private float GetActiveShieldAmount(IReadOnlyList<RuntimeEffectInstance> effects)
    {
        IReadOnlyList<RuntimeEffectInstance> resolvedEffects = effects;
        if (resolvedEffects == null && playerCharacter != null && playerCharacter.characterObject != null)
        {
            RebindRuntimeEffectEvents();
            resolvedEffects = subscribedEffectInventory != null
                ? subscribedEffectInventory.GetActiveRuntimeEffects(playerCharacter.characterObject)
                : playerCharacter.RuntimeEffects != null ? playerCharacter.RuntimeEffects.ActiveEffects : null;
        }

        if (resolvedEffects == null)
        {
            return 0f;
        }

        float total = 0f;
        for (int i = 0; i < resolvedEffects.Count; i++)
        {
            RuntimeEffectInstance effect = resolvedEffects[i];
            if (effect != null && effect.remainingShieldAmount > 0f)
            {
                total += effect.remainingShieldAmount;
            }
        }

        return total;
    }

    private Transform FindHudRoot(Transform parent)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < HudRootNames.Length; i++)
        {
            Transform foundByName = FindChildRecursive(parent, HudRootNames[i]);
            if (foundByName != null)
                return foundByName;
        }

        return null;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        if (parent.name == childName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private Slider FindHealthLerpSlider(Transform root, Slider mainSlider)
    {
        Slider[] sliders = root.GetComponentsInChildren<Slider>(true);
        Slider fallback = null;
        for (int i = 0; i < sliders.Length; i++)
        {
            Slider slider = sliders[i];
            if (slider == null || slider == mainSlider || IsShieldSlider(slider))
            {
                continue;
            }

            if (IsHealthLerpSlider(slider))
            {
                return slider;
            }

            fallback ??= slider;
        }

        return fallback;
    }

    private bool IsHealthLerpSlider(Slider slider)
    {
        if (slider == null)
        {
            return false;
        }

        if (NameLooksLikeHealthLerp(slider.name))
        {
            return true;
        }

        return slider.fillRect != null && NameLooksLikeHealthLerp(slider.fillRect.name);
    }

    private bool IsShieldSlider(Slider slider)
    {
        if (slider == null)
        {
            return false;
        }

        if (slider == shieldSlider)
        {
            return true;
        }

        if (NameLooksLikeShield(slider.name))
        {
            return true;
        }

        Transform current = slider.transform.parent;
        while (current != null && current != hudRoot)
        {
            if (NameLooksLikeShield(current.name))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool NameLooksLikeHealthLerp(string value)
    {
        return !string.IsNullOrEmpty(value)
            && (value.Contains("Lerp")
                || value.Contains("Ghost")
                || value.Contains("Back"));
    }

    private static bool NameLooksLikeShield(string value)
    {
        return !string.IsNullOrEmpty(value)
            && value.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
