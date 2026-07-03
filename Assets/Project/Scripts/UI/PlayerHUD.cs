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

    [Header("References")]
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private TeamLoader teamLoader;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Transform runtimeEffectContainer;
    [SerializeField] private RuntimeEffectIconUI runtimeEffectIconPrefab;

    private RectTransform hudRoot;
    private Slider healthSlider;
    private Image healthFillImage;
    private TMP_Text nameText;
    private TMP_Text hpText;
    private PlayerCharacter subscribedPlayer;
    private PlayerRunInventory subscribedEffectInventory;
    private readonly List<RuntimeEffectIconUI> runtimeEffectIcons = new List<RuntimeEffectIconUI>();
    private string lastCharacterKey = string.Empty;

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
        RebindEvents();
        RebindRuntimeEffectEvents();
        RefreshHUD();
    }

    private void OnDisable()
    {
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

        string currentCharacterKey = GetCharacterKey();
        if (currentCharacterKey != lastCharacterKey)
            RefreshHUD();

        RefreshRuntimeEffects();
    }

    private void CacheReferences()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (teamLoader == null)
            teamLoader = Object.FindFirstObjectByType<TeamLoader>();

        if (targetCanvas == null || FindHudRoot(targetCanvas.transform) == null)
            targetCanvas = FindTargetCanvas();

        if (runtimeEffectContainer == null && hudRoot != null)
            runtimeEffectContainer = FindRuntimeEffectContainer(hudRoot);
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
        lastCharacterKey = string.Empty;
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

            if (healthFillImage != null)
                healthFillImage.fillAmount = 0f;

            lastCharacterKey = GetCharacterKey();
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

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.value = normalizedHp;
        }

        if (healthFillImage != null)
            healthFillImage.fillAmount = normalizedHp;

        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";

        lastCharacterKey = GetCharacterKey();
        RefreshRuntimeEffects();
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
            teamLoader = Object.FindFirstObjectByType<TeamLoader>();
        }

        return teamLoader != null ? teamLoader.RunInventory : null;
    }

    private void HandleRuntimeEffectsChanged(CharacterObject changedCharacter)
    {
        if (playerCharacter == null || changedCharacter == playerCharacter.characterObject)
        {
            RefreshRuntimeEffects();
        }
    }

    private void RefreshRuntimeEffects()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
        {
            SetRuntimeEffectIconCount(0);
            return;
        }

        if (runtimeEffectContainer == null)
        {
            runtimeEffectContainer = hudRoot != null
                ? FindRuntimeEffectContainer(hudRoot)
                : targetCanvas != null ? FindRuntimeEffectContainer(targetCanvas.transform) : null;
        }

        if (runtimeEffectContainer == null)
        {
            return;
        }

        RebindRuntimeEffectEvents();
        IReadOnlyList<RuntimeEffectInstance> effects = subscribedEffectInventory != null
            ? subscribedEffectInventory.GetActiveRuntimeEffects(playerCharacter.characterObject)
            : playerCharacter.RuntimeEffects != null ? playerCharacter.RuntimeEffects.ActiveEffects : null;

        int count = effects != null ? effects.Count : 0;
        SetRuntimeEffectIconCount(count);
        for (int i = 0; i < count; i++)
        {
            runtimeEffectIcons[i].Bind(effects[i]);
        }

        playerCharacter.RuntimeEffects?.SyncVisuals(effects);
    }

    private void SetRuntimeEffectIconCount(int count)
    {
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

    private Transform FindRuntimeEffectContainer(Transform parent)
    {
        return FindChildRecursive(parent, "BuffContainer")
            ?? FindChildRecursive(parent, "BuffDebuffContainer")
            ?? FindChildRecursive(parent, "StatusEffectContainer")
            ?? FindChildRecursive(parent, "RuntimeEffectContainer")
            ?? FindChildRecursive(parent, "EffectContainer");
    }

    private string GetCharacterKey()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return string.Empty;

        float maxHp = playerCharacter.Stats != null ? playerCharacter.Stats.maxHP : 0f;
        return playerCharacter.characterObject.characterId + "|" + maxHp;
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
            if (healthSlider != null && healthSlider.fillRect != null)
                healthFillImage = healthSlider.fillRect.GetComponent<Image>();
        }

        Transform heroNameTransform = FindChildRecursive(root, "HeroName");
        if (heroNameTransform != null)
            nameText = heroNameTransform.GetComponent<TMP_Text>();

        Transform hpTextTransform = FindChildRecursive(root, "HP");
        if (hpTextTransform != null)
            hpText = hpTextTransform.GetComponent<TMP_Text>();

        if (nameText == null || hpText == null || (healthSlider == null && healthFillImage == null))
        {
            healthSlider = null;
            healthFillImage = null;
            nameText = null;
            hpText = null;
            hudRoot = null;
        }
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
}
