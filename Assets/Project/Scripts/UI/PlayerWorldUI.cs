using TMPro;
using UnityEngine;

public class PlayerWorldUI : MonoBehaviour
{
    [Header("World Canvas")]
    [SerializeField] private Transform worldCanvasRoot;
    [SerializeField] private Vector3 textOffset = new Vector3(0f, 2.4f, 0f);

    [Header("Text Template")]
    [SerializeField] private TMP_Text feedbackTextTemplate;

    private PlayerCharacter player;
    private Vector3 baseLocalScale = Vector3.one;

    private void Awake()
    {
        player = GetComponentInParent<PlayerCharacter>();
        baseLocalScale = new Vector3(
            Mathf.Abs(transform.localScale.x),
            Mathf.Abs(transform.localScale.y),
            Mathf.Abs(transform.localScale.z));
        ResolveWorldCanvas();
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
        player.OnShieldGained += ShowShieldGain;
        player.OnShieldAbsorbed += ShowShieldAbsorb;
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
        player.OnShieldGained -= ShowShieldGain;
        player.OnShieldAbsorbed -= ShowShieldAbsorb;
        player.OnDamageMissed -= ShowMissed;
    }

    private void LateUpdate()
    {
        KeepCanvasFacingForward();
    }

    private void ShowDamage(float amount)
    {
        Show(WorldTextType.Damage, amount);
    }

    private void ShowHeal(float amount)
    {
        Show(WorldTextType.Heal, amount, $"+{Mathf.CeilToInt(amount)}");
    }

    private void ShowShieldGain(float amount)
    {
        Show(WorldTextType.Shield, amount, $"+{Mathf.CeilToInt(amount)}");
    }

    private void ShowShieldAbsorb(float amount)
    {
        Show(WorldTextType.Shield, amount, Mathf.CeilToInt(amount).ToString());
    }

    private void ShowMissed()
    {
        Show(WorldTextType.Missed, 0f, "Missed");
    }

    private void Show(WorldTextType type, float amount, string text = null)
    {
        ResolveWorldCanvas();

        WorldTextUtility.Show(new WorldTextRequest
        {
            source = transform,
            canvasRoot = worldCanvasRoot,
            template = feedbackTextTemplate,
            type = type,
            amount = amount,
            text = text,
            damageRoutine = DamageRoutine.Physical,
            impactTier = DamageImpactTier.Low,
            elementType = ElementType.None,
            isCritical = false,
            offset = textOffset
        });
    }

    private void ResolveWorldCanvas()
    {
        if (worldCanvasRoot == null)
        {
            Canvas localCanvas = GetComponentInChildren<Canvas>(true);
            if (localCanvas != null)
            {
                worldCanvasRoot = localCanvas.transform;
            }
            else if (GetComponent<Canvas>() != null)
            {
                worldCanvasRoot = transform;
            }
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
