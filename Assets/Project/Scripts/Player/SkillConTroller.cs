using UnityEngine;
using System.Collections;

public class SkillController : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    public PlayerMovement movement;
    public AnimationHandler animationHandler;

    [Header("Spawn Point")]
    public Transform skillSpawnPoint;

    // =========================================================
    // Runtime
    // =========================================================
    [Header("Runtime")]
    [SerializeField] private float lastUseTime = -999f;
    [SerializeField] private bool isCasting = false;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (animationHandler == null)
            animationHandler = GetComponentInChildren<AnimationHandler>();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void UseSkill()
    {
        if (isCasting)
            return;

        if (playerCharacter == null || playerCharacter.characterObject == null)
            return;

        SkillData skillData = playerCharacter.characterObject.skillData;
        if (skillData == null)
            return;

        float finalCooldown = GetFinalSkillCooldown(skillData.cooldown);

        if (Time.time < lastUseTime + finalCooldown)
            return;

        StartCoroutine(CastSkillRoutine(skillData));
    }

    public float GetRemainingCooldown()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return 0f;

        SkillData skillData = playerCharacter.characterObject.skillData;
        if (skillData == null)
            return 0f;

        float finalCooldown = GetFinalSkillCooldown(skillData.cooldown);
        float remain = (lastUseTime + finalCooldown) - Time.time;

        return Mathf.Max(remain, 0f);
    }

    public bool IsCasting()
    {
        return isCasting;
    }

    // =========================================================
    // Coroutines
    // =========================================================
    private IEnumerator CastSkillRoutine(SkillData skillData)
    {
        isCasting = true;

        if (movement != null)
            movement.CanMove = false;

        if (animationHandler != null)
            animationHandler.TriggerSkill();

        yield return new WaitForSeconds(skillData.castDuration);

        if (skillData.skillPrefab != null)
        {
            Vector3 spawnPos = GetSpawnPosition(skillData.spawnOffset);
            GameObject spawned = Instantiate(skillData.skillPrefab, spawnPos, Quaternion.identity);

            ElementPayload payload = spawned.GetComponent<ElementPayload>();
            if (payload != null)
            {
                payload.Initialize(
                    skillData.elementType,
                    playerCharacter.characterObject.characterId,
                    true
                );
            }

            GustBreakSkill gustBreak = spawned.GetComponent<GustBreakSkill>();
            if (gustBreak != null)
            {
                float finalDamageMultiplier = GetFinalSkillDamage(skillData.damageMultiplier);
                float finalKnockback = GetFinalSkillKnockback(skillData.knockbackForce);
                Vector2 direction = GetFacingDirection();

                gustBreak.Initialize(finalDamageMultiplier, finalKnockback, direction);
            }
        }
        else
        {
            Debug.LogWarning("SkillController: skillPrefab is missing");
        }

        lastUseTime = Time.time;

        if (movement != null)
            movement.CanMove = true;

        isCasting = false;
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private Vector3 GetSpawnPosition(Vector2 offset)
    {
        Transform origin = skillSpawnPoint != null ? skillSpawnPoint : transform;

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

    private float GetFinalSkillDamage(float baseValue)
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

            if (node.effectType == SkillTreeEffectType.SkillDamageBonus)
            {
                result += node.effectValue;
            }
        }

        return result;
    }

    private float GetFinalSkillKnockback(float baseValue)
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

            if (node.effectType == SkillTreeEffectType.SkillKnockbackBonus)
            {
                result += node.effectValue;
            }
        }

        return result;
    }

    private float GetFinalSkillCooldown(float baseValue)
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

            if (node.effectType == SkillTreeEffectType.SkillCooldownReduction)
            {
                result -= node.effectValue;
            }
        }

        return Mathf.Max(result, 0.1f);
    }
}