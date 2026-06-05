using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilityUIManager : MonoBehaviour
{
    public static AbilityUIManager Instance;

    public enum AbilityType { Skill, Ultimate }

    [Header("References")]
    [SerializeField] private PlayerCharacter playerCharacter;

    [Header("Skill UI")]
    [SerializeField] private RectTransform skillReadyFeedbackRoot;
    public Image skillCooldownFill;
    public TMP_Text skillCooldownText;

    [Header("Ultimate UI")]
    [SerializeField] private RectTransform ultReadyFeedbackRoot;
    public Image ultCooldownFill;
    public TMP_Text ultCooldownText;

    [Header("Fallback Ready Feedback")]
    [SerializeField] private bool playFallbackReadyFeedback = true;
    [SerializeField] private Color readyGlintColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color readyBurstColor = new Color(0.6f, 1f, 0.85f, 0.9f);
    [SerializeField] private float readyPulseScale = 1.25f;
    [SerializeField] private float readyFeedbackDuration = 0.18f;

    private struct CooldownDisplayState
    {
        public float current;
        public float max;
    }

    private readonly Dictionary<string, CooldownDisplayState> skillCooldowns = new Dictionary<string, CooldownDisplayState>();
    private readonly Dictionary<string, CooldownDisplayState> ultimateCooldowns = new Dictionary<string, CooldownDisplayState>();

    private PlayerCharacter subscribedPlayer;
    private string displayedCharacterKey = string.Empty;
    private UIFeedbackDriver feedbackDriver;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        CacheReferences();
        feedbackDriver = UIFeedbackDriver.GetOrAdd(gameObject);
        ResetUI(skillCooldownFill, skillCooldownText);
        ResetUI(ultCooldownFill, ultCooldownText);
    }

    private void Start()
    {
        RebindPlayer();
        RefreshActiveCharacterUI();
    }

    private void OnEnable()
    {
        RebindPlayer();
        RefreshActiveCharacterUI();
    }

    private void OnDisable()
    {
        UnbindPlayer();
    }

    private void Update()
    {
        TickCooldownDictionary(skillCooldowns, AbilityType.Skill);
        TickCooldownDictionary(ultimateCooldowns, AbilityType.Ultimate);

        CacheReferences();
        RebindPlayer();

        string currentCharacterKey = GetCurrentCharacterKey();
        if (currentCharacterKey != displayedCharacterKey)
        {
            displayedCharacterKey = currentCharacterKey;
        }

        RefreshActiveCharacterUI();
    }

    public void TriggerCooldown(AbilityType type, float duration)
    {
        if (duration <= 0f)
        {
            RefreshActiveCharacterUI();
            return;
        }

        string characterKey = GetCurrentCharacterKey();
        if (string.IsNullOrEmpty(characterKey))
            return;

        Dictionary<string, CooldownDisplayState> targetDictionary =
            type == AbilityType.Skill ? skillCooldowns : ultimateCooldowns;

        targetDictionary[characterKey] = new CooldownDisplayState
        {
            current = duration,
            max = duration
        };

        RefreshActiveCharacterUI();
    }

    private void CacheReferences()
    {
        if (playerCharacter == null)
            playerCharacter = FindFirstObjectByType<PlayerCharacter>();
    }

    private void RebindPlayer()
    {
        if (playerCharacter == subscribedPlayer)
            return;

        UnbindPlayer();
        subscribedPlayer = playerCharacter;

        if (subscribedPlayer != null)
            subscribedPlayer.OnCharacterChanged += HandleCharacterChanged;
    }

    private void UnbindPlayer()
    {
        if (subscribedPlayer == null)
            return;

        subscribedPlayer.OnCharacterChanged -= HandleCharacterChanged;
        subscribedPlayer = null;
    }

    private void HandleCharacterChanged(CharacterObject newCharacter)
    {
        displayedCharacterKey = BuildCharacterKey(newCharacter);
        RefreshActiveCharacterUI();
    }

    private void TickCooldownDictionary(Dictionary<string, CooldownDisplayState> cooldowns, AbilityType type)
    {
        if (cooldowns.Count == 0)
            return;

        List<string> keysToClear = null;
        List<string> keys = new List<string>(cooldowns.Keys);

        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            CooldownDisplayState state = cooldowns[key];
            state.current = Mathf.Max(0f, state.current - Time.deltaTime);

            if (state.current <= 0f || state.max <= 0f)
            {
                keysToClear ??= new List<string>();
                keysToClear.Add(key);

                string currentCharacterKey = GetCurrentCharacterKey();
                if (string.Equals(key, currentCharacterKey, System.StringComparison.Ordinal))
                {
                    PlayAbilityReadyFeedback(type);
                }

                continue;
            }

            cooldowns[key] = state;
        }

        if (keysToClear == null)
            return;

        for (int i = 0; i < keysToClear.Count; i++)
        {
            cooldowns.Remove(keysToClear[i]);
        }
    }

    private void RefreshActiveCharacterUI()
    {
        string characterKey = GetCurrentCharacterKey();
        displayedCharacterKey = characterKey;

        RefreshAbilityUI(skillCooldowns, characterKey, skillCooldownFill, skillCooldownText);
        RefreshAbilityUI(ultimateCooldowns, characterKey, ultCooldownFill, ultCooldownText);
    }

    private void RefreshAbilityUI(
        Dictionary<string, CooldownDisplayState> cooldowns,
        string characterKey,
        Image fillImage,
        TMP_Text text)
    {
        if (string.IsNullOrEmpty(characterKey) || !cooldowns.TryGetValue(characterKey, out CooldownDisplayState state))
        {
            ResetUI(fillImage, text);
            return;
        }

        UpdateUI(state.current, state.max, fillImage, text);
    }

    private string GetCurrentCharacterKey()
    {
        CharacterObject currentCharacter = playerCharacter != null ? playerCharacter.characterObject : null;
        return BuildCharacterKey(currentCharacter);
    }

    private string BuildCharacterKey(CharacterObject character)
    {
        if (character == null)
            return string.Empty;

        return character.GetInstanceID().ToString();
    }

    private void UpdateUI(float current, float max, Image fillImage, TMP_Text text)
    {
        if (fillImage == null || text == null)
            return;

        if (current <= 0f || max <= 0f)
        {
            ResetUI(fillImage, text);
            return;
        }

        fillImage.gameObject.SetActive(true);
        text.gameObject.SetActive(true);
        fillImage.fillAmount = Mathf.Clamp01(current / max);
        text.text = Mathf.Ceil(current).ToString();
    }

    private void ResetUI(Image fillImage, TMP_Text text)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
            fillImage.gameObject.SetActive(false);
        }

        if (text != null)
        {
            text.gameObject.SetActive(false);
            text.text = string.Empty;
        }
    }

    private void PlayAbilityReadyFeedback(AbilityType type)
    {
        if (!playFallbackReadyFeedback)
        {
            return;
        }

        Image targetFill = type == AbilityType.Skill ? skillCooldownFill : ultCooldownFill;
        RectTransform targetRect = type == AbilityType.Skill ? skillReadyFeedbackRoot : ultReadyFeedbackRoot;
        if (targetRect == null && targetFill != null)
        {
            Transform feedbackParent = targetFill.transform.parent != null
                ? targetFill.transform.parent
                : targetFill.transform;
            targetRect = feedbackParent as RectTransform;
        }

        if (targetRect == null)
        {
            return;
        }

        feedbackDriver = feedbackDriver != null ? feedbackDriver : UIFeedbackDriver.GetOrAdd(gameObject);
        if (feedbackDriver == null)
        {
            return;
        }

        feedbackDriver.PlayPulse(targetRect, readyPulseScale, readyFeedbackDuration);
        feedbackDriver.PlayGlint(targetRect, readyGlintColor, readyFeedbackDuration);
        feedbackDriver.PlayRadialBurst(targetRect, readyBurstColor, 8, 36f, readyFeedbackDuration);
    }
}
