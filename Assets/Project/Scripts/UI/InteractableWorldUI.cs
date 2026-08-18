using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class InteractableWorldUI : MonoBehaviour
{
    public enum InteractionType
    {
        None,
        Shop,
        Dialogue,
        SavePoint,
        Custom,
        ShopReroll
    }

    [System.Serializable]
    private class PromptConfig
    {
        public InteractionType interactionType;
        public string keyString;
        public string descriptionString;
        public Vector3 offsetFromPlayer = new Vector3(1f, 1.5f, -0.5f);
        public float floatingSpeed = 2f;
        public float floatingHeight = 0.1f;
        public float uiSmoothTime = 0.15f;
    }

    private class PromptRuntime
    {
        public PromptConfig Config;
        public GameObject Root;
        public TMP_Text KeyText;
        public TMP_Text DescriptionText;
        public Vector3 BaseScale;
        public Vector3 CurrentVelocity;
        public Coroutine ScaleCoroutine;
    }

    [Header("Settings")]
    [SerializeField] private InteractionType interactionType;
    [SerializeField] private string keyString;
    [SerializeField] private string descriptionString;
    [Tooltip("Offset relative to the player to ensure it doesn't block the front view.")]
    [SerializeField] private Vector3 offsetFromPlayer = new Vector3(1f, 1.5f, -0.5f);
    [SerializeField] private float floatingSpeed = 2f;
    [SerializeField] private float floatingHeight = 0.1f;
    [SerializeField] private float uiSmoothTime = 0.15f;

    [Header("World Canvas")]
    [Tooltip("Scene WorldCanvas root. If empty, the first GameObject named WorldCanvas is used.")]
    [SerializeField] private Transform worldCanvasRoot;
    [Tooltip("InteractionText prefab/template. If empty, WorldCanvas/InteractionText is used as the template.")]
    [SerializeField] private GameObject interactionTextPrefab;

    [Header("Legacy UI References")]
    [Tooltip("Backward-compatible reference. If this is a WorldCanvas, it is used as worldCanvasRoot.")]
    [SerializeField] private GameObject uiCanvasObject;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Additional Prompts")]
    [SerializeField] private PromptConfig[] promptConfigs;

    private readonly List<PromptRuntime> prompts = new List<PromptRuntime>();
    private Transform playerTransform;
    private Camera mainCamera;
    private bool isPlayerInRange;

    private void Awake()
    {
        ResolveWorldCanvas();
        BuildPrompts();
    }

    private void Start()
    {
        mainCamera = Camera.main;
        HideTemplate();
        SetPromptActive(false);
    }

    private void Update()
    {
        if (!isPlayerInRange || playerTransform == null)
        {
            return;
        }

        for (int i = 0; i < prompts.Count; i++)
        {
            PromptRuntime prompt = prompts[i];
            if (prompt.Root == null)
            {
                continue;
            }

            Vector3 targetPosition = playerTransform.position
                + (playerTransform.right * prompt.Config.offsetFromPlayer.x)
                + (playerTransform.up * prompt.Config.offsetFromPlayer.y)
                + (playerTransform.forward * prompt.Config.offsetFromPlayer.z);

            float floatOffset = Mathf.Sin(Time.time * prompt.Config.floatingSpeed) * prompt.Config.floatingHeight;
            targetPosition.y += floatOffset;

            prompt.Root.transform.position = Vector3.SmoothDamp(
                prompt.Root.transform.position,
                targetPosition,
                ref prompt.CurrentVelocity,
                prompt.Config.uiSmoothTime);

            if (mainCamera != null)
            {
                prompt.Root.transform.rotation = Quaternion.LookRotation(prompt.Root.transform.position - mainCamera.transform.position);
            }
        }

        HandlePlayerInput();
    }

    private void ResolveWorldCanvas()
    {
        if (worldCanvasRoot == null && uiCanvasObject != null && uiCanvasObject.name == "WorldCanvas")
        {
            worldCanvasRoot = uiCanvasObject.transform;
        }

        if (worldCanvasRoot == null)
        {
            GameObject worldCanvas = GameObject.Find("WorldCanvas");
            if (worldCanvas != null)
            {
                worldCanvasRoot = worldCanvas.transform;
            }
        }

        if (interactionTextPrefab == null && worldCanvasRoot != null)
        {
            Transform template = worldCanvasRoot.Find("InteractionText");
            if (template != null)
            {
                interactionTextPrefab = template.gameObject;
            }
        }
    }

    private void BuildPrompts()
    {
        prompts.Clear();
        AddPrompt(new PromptConfig
        {
            interactionType = interactionType,
            keyString = keyString,
            descriptionString = descriptionString,
            offsetFromPlayer = offsetFromPlayer,
            floatingSpeed = floatingSpeed,
            floatingHeight = floatingHeight,
            uiSmoothTime = uiSmoothTime
        }, preferLegacyReferences: true);

        if (promptConfigs == null)
        {
            return;
        }

        for (int i = 0; i < promptConfigs.Length; i++)
        {
            AddPrompt(promptConfigs[i], preferLegacyReferences: false);
        }
    }

    private void AddPrompt(PromptConfig config, bool preferLegacyReferences)
    {
        if (config == null || string.IsNullOrEmpty(config.keyString))
        {
            return;
        }

        GameObject promptRoot = preferLegacyReferences && uiCanvasObject != null && uiCanvasObject.name != "WorldCanvas"
            ? uiCanvasObject
            : CreatePromptInstance(config);

        if (promptRoot == null)
        {
            return;
        }

        PromptRuntime runtime = new PromptRuntime
        {
            Config = config,
            Root = promptRoot,
            KeyText = preferLegacyReferences && keyText != null ? keyText : FindPromptText(promptRoot.transform, "KeyPress"),
            DescriptionText = preferLegacyReferences && descriptionText != null ? descriptionText : FindPromptText(promptRoot.transform, "DescText"),
            BaseScale = promptRoot.transform.localScale
        };

        prompts.Add(runtime);
    }

    private GameObject CreatePromptInstance(PromptConfig config)
    {
        if (interactionTextPrefab == null || worldCanvasRoot == null)
        {
            return null;
        }

        GameObject instance = Instantiate(interactionTextPrefab);
        instance.transform.SetParent(worldCanvasRoot, false);
        instance.name = $"InteractionText_{config.keyString}";
        return instance;
    }

    private void HideTemplate()
    {
        if (interactionTextPrefab != null && interactionTextPrefab.transform.parent == worldCanvasRoot)
        {
            interactionTextPrefab.SetActive(false);
        }
    }

    private static TMP_Text FindPromptText(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform child = FindChildRecursive(root, childName);
        return child != null ? child.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void HandlePlayerInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        for (int i = 0; i < prompts.Count; i++)
        {
            PromptRuntime prompt = prompts[i];
            try
            {
                KeyControl control = keyboard[prompt.Config.keyString.ToLower()] as KeyControl;
                if (control != null && control.wasPressedThisFrame)
                {
                    OnInteract(prompt.Config);
                }
            }
            catch (System.ArgumentException)
            {
                // Invalid key strings are ignored so one bad prompt does not break nearby prompts.
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || isPlayerInRange)
        {
            return;
        }

        playerTransform = other.transform;
        isPlayerInRange = true;

        for (int i = 0; i < prompts.Count; i++)
        {
            PromptRuntime prompt = prompts[i];
            if (prompt.Root == null)
            {
                continue;
            }

            if (prompt.KeyText != null)
            {
                prompt.KeyText.text = prompt.Config.keyString;
            }

            if (prompt.DescriptionText != null)
            {
                prompt.DescriptionText.text = GetDisplayDescription(prompt.Config);
            }

            prompt.Root.SetActive(true);
            prompt.Root.transform.position = GetPromptPosition(prompt.Config);
            StartScaleAnimation(prompt, 0f, 1f, 0.25f);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        isPlayerInRange = false;

        for (int i = 0; i < prompts.Count; i++)
        {
            PromptRuntime prompt = prompts[i];
            if (prompt.Root != null)
            {
                StartScaleAnimation(prompt, prompt.Root.transform.localScale.x, 0f, 0.15f, () => prompt.Root.SetActive(false));
            }
        }

        playerTransform = null;
    }

    private Vector3 GetPromptPosition(PromptConfig config)
    {
        return playerTransform.position
            + (playerTransform.right * config.offsetFromPlayer.x)
            + (playerTransform.up * config.offsetFromPlayer.y)
            + (playerTransform.forward * config.offsetFromPlayer.z);
    }

    private void StartScaleAnimation(PromptRuntime prompt, float startScale, float endScale, float duration, System.Action onComplete = null)
    {
        if (prompt.ScaleCoroutine != null)
        {
            StopCoroutine(prompt.ScaleCoroutine);
        }

        prompt.ScaleCoroutine = StartCoroutine(ScaleAnimation(prompt, startScale, endScale, duration, onComplete));
    }

    private IEnumerator ScaleAnimation(PromptRuntime prompt, float startScale, float endScale, float duration, System.Action onComplete = null)
    {
        float timer = 0f;
        Vector3 initialScale = prompt.BaseScale * startScale;
        Vector3 targetScale = prompt.BaseScale * endScale;

        prompt.Root.transform.localScale = initialScale;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float curve = 1f - Mathf.Pow(1f - t, 3f);

            prompt.Root.transform.localScale = Vector3.Lerp(initialScale, targetScale, curve);
            yield return null;
        }

        prompt.Root.transform.localScale = targetScale;
        prompt.ScaleCoroutine = null;
        onComplete?.Invoke();
    }

    private void SetPromptActive(bool isActive)
    {
        for (int i = 0; i < prompts.Count; i++)
        {
            if (prompts[i].Root != null)
            {
                prompts[i].Root.SetActive(isActive);
            }
        }
    }

    private void OnInteract(PromptConfig prompt)
    {
        if (Project.UI.InputLockManager.IsLocked)
        {
            return;
        }

        switch (prompt.interactionType)
        {
            case InteractionType.Shop:
                Debug.Log("Triggering Shop UI Opening...");
                FindFirstObjectByType<Project.ShopSystem.UI.ShopUI>(FindObjectsInactive.Include)?.OpenShopUI();
                break;
            case InteractionType.Dialogue:
                Debug.Log("Triggering Dialogue...");
                break;
            case InteractionType.SavePoint:
                Debug.Log("Triggering Save Point...");
                break;
            case InteractionType.Custom:
                Debug.Log("Triggering Custom Interaction...");
                break;
            case InteractionType.ShopReroll:
                Project.ShopSystem.ShopManager shopManager = FindFirstObjectByType<Project.ShopSystem.ShopManager>();
                if (shopManager != null && shopManager.TryRerollOffers())
                {
                    RefreshPromptDescriptions();
                }
                break;
        }
    }

    private void RefreshPromptDescriptions()
    {
        for (int i = 0; i < prompts.Count; i++)
        {
            PromptRuntime prompt = prompts[i];
            if (prompt.DescriptionText != null)
            {
                prompt.DescriptionText.text = GetDisplayDescription(prompt.Config);
            }
        }
    }

    private string GetDisplayDescription(PromptConfig prompt)
    {
        if (prompt.interactionType != InteractionType.ShopReroll)
        {
            return prompt.descriptionString;
        }

        Project.ShopSystem.ShopManager shopManager = FindFirstObjectByType<Project.ShopSystem.ShopManager>();
        return shopManager != null
            ? $"{prompt.descriptionString} +{shopManager.CurrentRerollPriceIncreasePercent:0.#}%"
            : prompt.descriptionString;
    }
}
