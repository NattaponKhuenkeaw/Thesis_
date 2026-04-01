using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player HUD that subscribes to PlayerCharacter HP events (no longer uses CharacterRuntime).
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private Canvas targetCanvas;

    private RectTransform hudRoot;
    private Slider healthSlider;
    private Image healthFillImage;
    private TMP_Text nameText;
    private TMP_Text hpText;
    private PlayerCharacter subscribedPlayer;
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
        RefreshHUD();
    }

    private void OnDisable()
    {
        if (subscribedPlayer == null)
            return;

        subscribedPlayer.OnHealthChanged -= HandleHealthChanged;
        subscribedPlayer.OnDied -= HandleDied;
        subscribedPlayer = null;
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
    }

    private void CacheReferences()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (targetCanvas == null)
            targetCanvas = FindTargetCanvas();
    }

    private void EnsureHudReady()
    {
        CacheReferences();

        if (targetCanvas == null)
            return;

        CreateHudIfNeeded();
        RebindEvents();
        RefreshHUD();
    }

    private void RebindEvents()
    {
        if (playerCharacter == subscribedPlayer)
            return;

        if (subscribedPlayer != null)
        {
            subscribedPlayer.OnHealthChanged -= HandleHealthChanged;
            subscribedPlayer.OnDied -= HandleDied;
        }

        subscribedPlayer = playerCharacter;

        if (subscribedPlayer != null)
        {
            subscribedPlayer.OnHealthChanged += HandleHealthChanged;
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
                hpText.text = "HP -- / --";

            if (healthSlider != null)
                healthSlider.value = 0f;

            if (healthFillImage != null)
                healthFillImage.fillAmount = 0f;

            lastCharacterKey = GetCharacterKey();
            return;
        }

        float maxHp = Mathf.Max(1f, playerCharacter.MaxHP);
        float currentHp = Mathf.Clamp(playerCharacter.CurrentHP, 0f, maxHp);
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
            hpText.text = $"HP {Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";

        lastCharacterKey = GetCharacterKey();
    }

    private string GetCharacterKey()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return string.Empty;

        return playerCharacter.characterObject.characterId + "|" + playerCharacter.characterObject.stats.maxHP;
    }

    private Canvas FindTargetCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null)
                continue;

            if (FindChildRecursive(canvas.transform, "PlayerUI") != null)
                return canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        }

        return null;
    }

    private Transform FindExistingHudRoot()
    {
        if (targetCanvas != null)
        {
            Transform playerUi = FindChildRecursive(targetCanvas.transform, "PlayerUI");
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
