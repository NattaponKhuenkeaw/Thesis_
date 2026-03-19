using UnityEngine;
using System.Collections;

public class UltimateController : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    public PlayerMovement movement;
    public AnimationHandler animationHandler;
    public PlayerActionController actionController;

    [Header("Spawn Point")]
    public Transform ultimateSpawnPoint;

    // =========================================================
    // Runtime
    // =========================================================
    [Header("Runtime")]
    [SerializeField] private float lastUseTime = -999f;
    [SerializeField] private bool isCasting = false;

    void Awake()
    {
        if (actionController == null)
            actionController = GetComponent<PlayerActionController>();

        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (animationHandler == null)
            animationHandler = GetComponentInChildren<AnimationHandler>();
    }

    public void UseUltimate()
    {
        if (actionController != null && actionController.IsBusy())
            return;

        if (isCasting)
            return;

        if (playerCharacter == null || playerCharacter.characterObject == null)
            return;

        UltimateData ultimateData = playerCharacter.characterObject.ultimateData;
        if (ultimateData == null)
            return;

        float finalCooldown = GetFinalUltimateCooldown(ultimateData.cooldown);

        if (Time.time < lastUseTime + finalCooldown)
            return;

        StartCoroutine(CastUltimateRoutine(ultimateData));
    }

    public bool IsCasting()
    {
        return isCasting;
    }

    public float GetRemainingCooldown()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return 0f;

        UltimateData ultimateData = playerCharacter.characterObject.ultimateData;
        if (ultimateData == null)
            return 0f;

        float finalCooldown = GetFinalUltimateCooldown(ultimateData.cooldown);
        float remain = (lastUseTime + finalCooldown) - Time.time;

        return Mathf.Max(remain, 0f);
    }

    private IEnumerator CastUltimateRoutine(UltimateData ultimateData)
    {
        if (actionController != null)
            actionController.SetState(PlayerActionState.Ultimate);

        isCasting = true;

        if (movement != null)
            movement.CanMove = false;

        if (animationHandler != null)
            animationHandler.TriggerUltimate();

        yield return new WaitForSeconds(ultimateData.castDuration);

        if (ultimateData.ultimatePrefab != null)
        {
            Vector3 spawnPos = GetSpawnPosition(ultimateData.spawnOffset);
            GameObject spawned = Instantiate(ultimateData.ultimatePrefab, spawnPos, Quaternion.identity);

            ElementPayload payload = spawned.GetComponent<ElementPayload>();
            if (payload != null)
            {
                payload.Initialize(
                    ultimateData.elementType,
                    playerCharacter.characterObject.characterId,
                    true
                );
            }

            SkyfallTornado tornado = spawned.GetComponent<SkyfallTornado>();
            if (tornado != null)
            {
                float finalDamage = GetFinalUltimateDamage(ultimateData.damageMultiplier);
                float finalDuration = GetFinalUltimateDuration(ultimateData.activeDuration);
                Vector2 direction = GetFacingDirection();

                tornado.Initialize(finalDamage, finalDuration, ultimateData.hitInterval, direction);
            }
        }
        else
        {
            Debug.LogWarning("UltimateController: ultimatePrefab is missing");
        }

        lastUseTime = Time.time;

        if (movement != null)
            movement.CanMove = true;

        isCasting = false;

        if (actionController != null)
            actionController.SetState(PlayerActionState.Normal);
    }

    private Vector3 GetSpawnPosition(Vector2 offset)
    {
        Transform origin = ultimateSpawnPoint != null ? ultimateSpawnPoint : transform;

        Vector3 finalOffset = new Vector3(offset.x, offset.y, 0f);

        if (movement != null && movement.spriteRenderer != null && movement.spriteRenderer.flipX)
            finalOffset.x = -Mathf.Abs(finalOffset.x);
        else
            finalOffset.x = Mathf.Abs(finalOffset.x);

        return origin.position + finalOffset;
    }

    private Vector2 GetFacingDirection()
    {
        if (movement != null && movement.spriteRenderer != null && movement.spriteRenderer.flipX)
            return Vector2.left;

        return Vector2.right;
    }

    private float GetFinalUltimateDamage(float baseValue)
    {
        float result = baseValue;

        SkillTreeData tree = playerCharacter.characterObject.skillTreeData;
        if (tree == null || tree.nodes == null)
            return result;

        for (int i = 0; i < tree.nodes.Length; i++)
        {
            SkillTreeNode node = tree.nodes[i];
            if (node == null || !node.unlocked)
                continue;

            if (node.effectType == SkillTreeEffectType.UltimateDamageBonus)
            {
                result += node.effectValue;
            }
        }

        return result;
    }

    private float GetFinalUltimateDuration(float baseValue)
    {
        float result = baseValue;

        SkillTreeData tree = playerCharacter.characterObject.skillTreeData;
        if (tree == null || tree.nodes == null)
            return result;

        for (int i = 0; i < tree.nodes.Length; i++)
        {
            SkillTreeNode node = tree.nodes[i];
            if (node == null || !node.unlocked)
                continue;

            if (node.effectType == SkillTreeEffectType.UltimateDurationBonus)
            {
                result += node.effectValue;
            }
        }

        return result;
    }

    private float GetFinalUltimateCooldown(float baseValue)
    {
        return Mathf.Max(baseValue, 0.1f);
    }
}