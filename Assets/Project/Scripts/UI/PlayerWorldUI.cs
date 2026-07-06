using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerWorldUI : MonoBehaviour
{
    [Header("World Canvas")]
    [SerializeField] private Transform worldCanvasRoot;
    [SerializeField] private Vector3 textOffset = new Vector3(0f, 2.4f, 0f);

    [Header("Text")]
    [SerializeField] private TMP_Text feedbackTextTemplate;
    [SerializeField] private int maxTextInstances = 10;
    [SerializeField] private float fontSize = 4f;
    [SerializeField] private Color damageColor = new Color(1f, 0.28f, 0.2f, 1f);
    [SerializeField] private Color healColor = new Color(0.35f, 1f, 0.45f, 1f);
    [SerializeField] private Color missColor = new Color(0.85f, 0.9f, 1f, 1f);

    [Header("Motion")]
    [SerializeField] private Vector2 randomRangeX = new Vector2(-0.35f, 0.35f);
    [SerializeField] private float floatDistance = 0.65f;
    [SerializeField] private float popDuration = 0.08f;
    [SerializeField] private float holdDuration = 0.2f;
    [SerializeField] private float fadeDuration = 0.45f;

    private readonly List<TMP_Text> textPool = new List<TMP_Text>();
    private PlayerCharacter player;
    private Camera mainCamera;
    private Vector3 baseLocalScale = Vector3.one;

    private void Awake()
    {
        player = GetComponentInParent<PlayerCharacter>();
        baseLocalScale = new Vector3(
            Mathf.Abs(transform.localScale.x),
            Mathf.Abs(transform.localScale.y),
            Mathf.Abs(transform.localScale.z));
        ResolveWorldCanvas();
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (player == null)
        {
            player = GetComponentInParent<PlayerCharacter>();
        }

        if (player == null)
        {
            return;
        }

        player.OnDamaged += ShowDamage;
        player.OnHealed += ShowHeal;
        player.OnDamageMissed += ShowMissed;
    }

    private void OnDisable()
    {
        if (player == null)
        {
            return;
        }

        player.OnDamaged -= ShowDamage;
        player.OnHealed -= ShowHeal;
        player.OnDamageMissed -= ShowMissed;
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        KeepCanvasFacingForward();
    }

    private void ShowDamage(float amount)
    {
        ShowText(Mathf.CeilToInt(amount).ToString(), damageColor);
    }

    private void ShowHeal(float amount)
    {
        ShowText($"+{Mathf.CeilToInt(amount)}", healColor);
    }

    private void ShowMissed()
    {
        ShowText("Missed", missColor);
    }

    private void ShowText(string value, Color color)
    {
        ResolveWorldCanvas();
        if (worldCanvasRoot == null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        TMP_Text text = GetPooledText();
        if (text == null)
        {
            return;
        }

        Vector3 startPosition = transform.position + textOffset;
        startPosition.x += Random.Range(randomRangeX.x, randomRangeX.y);

        text.text = value;
        text.color = color;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.transform.position = startPosition;
        FaceCamera(text.transform);
        text.gameObject.SetActive(true);
        text.transform.SetAsLastSibling();

        StartCoroutine(TextRoutine(text, startPosition, color));
    }

    private IEnumerator TextRoutine(TMP_Text text, Vector3 startPosition, Color baseColor)
    {
        RectTransform rect = text.rectTransform;
        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            rect.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.2f, t);
            FaceCamera(rect);
            yield return null;
        }

        rect.localScale = Vector3.one;
        yield return new WaitForSeconds(holdDuration);

        elapsed = 0f;
        Vector3 endPosition = startPosition + Vector3.up * floatDistance;
        Color color = baseColor;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            rect.position = Vector3.Lerp(startPosition, endPosition, t);
            color.a = 1f - t;
            text.color = color;
            FaceCamera(rect);
            yield return null;
        }

        text.gameObject.SetActive(false);
    }

    private TMP_Text GetPooledText()
    {
        for (int i = 0; i < textPool.Count; i++)
        {
            if (textPool[i] != null && !textPool[i].gameObject.activeSelf)
            {
                return textPool[i];
            }
        }

        if (textPool.Count >= Mathf.Max(1, maxTextInstances))
        {
            return null;
        }

        if (feedbackTextTemplate != null)
        {
            TMP_Text instance = Instantiate(feedbackTextTemplate, worldCanvasRoot);
            instance.name = "PlayerFeedbackText";
            instance.gameObject.SetActive(false);
            instance.raycastTarget = false;
            textPool.Add(instance);
            return instance;
        }

        GameObject textObject = new GameObject("PlayerFeedbackText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(worldCanvasRoot, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(4f, 1.25f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.gameObject.SetActive(false);
        textPool.Add(text);
        return text;
    }

    private void ResolveWorldCanvas()
    {
        if (worldCanvasRoot != null)
        {
            ResolveTextTemplate();
            return;
        }

        Canvas localCanvas = GetComponentInChildren<Canvas>(true);
        if (localCanvas != null)
        {
            worldCanvasRoot = localCanvas.transform;
        }
        else if (GetComponent<Canvas>() != null)
        {
            worldCanvasRoot = transform;
        }

        ResolveTextTemplate();
    }

    private void ResolveTextTemplate()
    {
        if (feedbackTextTemplate != null || worldCanvasRoot == null)
        {
            return;
        }

        TMP_Text[] texts = worldCanvasRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            feedbackTextTemplate = text;
            feedbackTextTemplate.gameObject.SetActive(false);
            break;
        }
    }

    private void FaceCamera(Transform target)
    {
        if (target == null || mainCamera == null)
        {
            return;
        }

        target.rotation = Quaternion.LookRotation(target.position - mainCamera.transform.position);
    }

    private void KeepCanvasFacingForward()
    {
        if (player != null && transform == player.transform)
        {
            return;
        }

        transform.rotation = Quaternion.identity;

        Transform parent = transform.parent;
        if (parent == null)
        {
            transform.localScale = baseLocalScale;
            return;
        }

        Vector3 parentLossyScale = parent.lossyScale;
        transform.localScale = new Vector3(
            baseLocalScale.x * (parentLossyScale.x < 0f ? -1f : 1f),
            baseLocalScale.y * (parentLossyScale.y < 0f ? -1f : 1f),
            baseLocalScale.z);
    }
}
