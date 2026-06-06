using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCharacter))]
public class PlayerSpeedFallbackFeedback : MonoBehaviour
{
    [Header("Threshold")]
    [SerializeField] private float baseSpeedStat = 10f;
    [SerializeField] private float streakThresholdMultiplier = 1.3f;
    [SerializeField] private float minimumHorizontalInput = 0.15f;

    [Header("Fallback Streak")]
    [SerializeField] private Color streakColor = new Color(0.75f, 1f, 1f, 0.45f);
    [SerializeField] private Vector2 streakSize = new Vector2(0.75f, 0.08f);
    [SerializeField] private Vector2 spawnOffset = new Vector2(-0.45f, 0.15f);
    [SerializeField] private float spawnInterval = 0.06f;
    [SerializeField] private float lifetime = 0.18f;
    [SerializeField] private float driftDistance = 0.35f;
    [SerializeField] private int sortingOrderOffset = -1;

    private static Sprite fallbackSprite;
    private PlayerCharacter playerCharacter;
    private PlayerMovement playerMovement;
    private SpriteRenderer playerRenderer;
    private float nextSpawnTime;

    private void Awake()
    {
        playerCharacter = GetComponent<PlayerCharacter>();
        playerMovement = playerCharacter != null ? playerCharacter.Movement : GetComponent<PlayerMovement>();
        playerRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (!ShouldSpawnStreak())
        {
            return;
        }

        if (Time.time < nextSpawnTime)
        {
            return;
        }

        nextSpawnTime = Time.time + Mathf.Max(0.01f, spawnInterval);
        SpawnStreak();
    }

    private bool ShouldSpawnStreak()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null || playerCharacter.Stats == null)
        {
            return false;
        }

        if (playerCharacter.Stats.speed < baseSpeedStat * streakThresholdMultiplier)
        {
            return false;
        }

        playerMovement = playerMovement != null ? playerMovement : playerCharacter.Movement;
        return playerMovement != null && Mathf.Abs(playerMovement.HorizontalInput) >= minimumHorizontalInput;
    }

    private void SpawnStreak()
    {
        float facingSign = playerMovement != null ? Mathf.Sign(playerMovement.FacingDirection) : Mathf.Sign(transform.localScale.x);
        if (Mathf.Approximately(facingSign, 0f))
        {
            facingSign = 1f;
        }

        GameObject streakObject = new GameObject("Fallback_SpeedStreak", typeof(SpriteRenderer));
        Transform streakTransform = streakObject.transform;
        streakTransform.position = transform.position + new Vector3(spawnOffset.x * facingSign, spawnOffset.y, 0f);
        streakTransform.localScale = new Vector3(streakSize.x, streakSize.y, 1f);
        streakTransform.rotation = Quaternion.identity;

        SpriteRenderer streakRenderer = streakObject.GetComponent<SpriteRenderer>();
        streakRenderer.sprite = GetFallbackSprite();
        streakRenderer.color = streakColor;

        if (playerRenderer != null)
        {
            streakRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            streakRenderer.sortingOrder = playerRenderer.sortingOrder + sortingOrderOffset;
        }

        StartCoroutine(FadeStreakRoutine(streakObject, streakRenderer, facingSign));
    }

    private IEnumerator FadeStreakRoutine(GameObject streakObject, SpriteRenderer streakRenderer, float facingSign)
    {
        Vector3 startPosition = streakObject.transform.position;
        Vector3 endPosition = startPosition + new Vector3(-facingSign * driftDistance, 0f, 0f);
        Color startColor = streakRenderer.color;
        float resolvedLifetime = Mathf.Max(0.01f, lifetime);
        float elapsed = 0f;

        while (elapsed < resolvedLifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / resolvedLifetime);
            streakObject.transform.position = Vector3.Lerp(startPosition, endPosition, t);

            Color color = startColor;
            color.a *= 1f - t;
            streakRenderer.color = color;
            yield return null;
        }

        Destroy(streakObject);
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite == null)
        {
            fallbackSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f));
        }

        return fallbackSprite;
    }
}
